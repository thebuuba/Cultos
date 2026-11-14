using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunHymnPlaylistTests(MainWindow window)
    {
        Test("Right-clicking a hymn video appends that hymn to the saved queue without starting playback", () =>
        {
            var workspace = Field<WorkspaceData>(window, "_workspace");
            var session = Field<PresentationSession>(window, "_session");
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv");
            var selected = new Cue { Title = "Himno 36", Kind = CueKind.Video, HymnNumber = 36, FilePath = path };
            var clicked = new Cue { Title = "Himno 37", Kind = CueKind.Video, HymnNumber = 37, FilePath = path };
            workspace.Library.Add(selected); workspace.Library.Add(clicked);
            var queue = new SavedSet { Name = "Himnos para el culto" };
            workspace.VideoCollections.Add(queue); Call(window, "RefreshVideoLists", queue);
            var library = (ListBox)window.FindName("LibraryList");
            ((ListBox)window.FindName("LibraryFilter")).SelectedIndex = 1;
            Call(window, "RefreshLibrary"); Call(window, "ActivateWorkspacePanel", "sources");
            library.SelectedItem = selected;
            window.UpdateLayout(); library.UpdateLayout();
            var row = (ListBoxItem)library.ItemContainerGenerator.ContainerFromItem(clicked);
            Check(row is not null, "Hymn search did not render the target video");
            var live = session.Live;
            ContextMenu? opened = null;
            EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
                new RoutedEventHandler((sender, _) => opened = (ContextMenu)sender));
            row!.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
            row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
                { RoutedEvent = Mouse.PreviewMouseUpEvent });
            PumpFor(100);
            Check(opened is not null && opened.IsOpen, "Himnario right-click has no playlist menu");
            var add = opened!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == "Añadir a lista de reproducción");
            Check(add.IsEnabled, "Hymn videos are excluded from playlists");
            var prepared = session.Prepared;
            add.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); opened.IsOpen = false;
            var copy = queue.Items.Single();
            Check(copy.Id != clicked.Id && copy.Title == clicked.Title && copy.HymnNumber == 37 && copy.FilePath == path,
                "Queue received the selected hymn instead of the clicked one, or lost its number");
            Check(ReferenceEquals(prepared, session.Prepared) && ReferenceEquals(live, session.Live), "Adding a hymn changed playback or preview");
            var saved = new WorkspaceStore().Load().VideoCollections.Single(s => s.Id == queue.Id).Items.Single();
            Check(saved.HymnNumber == 37 && saved.Id == copy.Id, "Hymn queue entry was not persisted");
        });
    }
}
