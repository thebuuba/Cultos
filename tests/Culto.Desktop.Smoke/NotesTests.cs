using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunNotesTests(MainWindow window)
    {
        Call(window, "ActivateWorkspacePanel", "text"); window.UpdateLayout();
        Test("Notes replaces the projection text editor and Bible omits add buttons", () =>
        {
            Check(window.FindName("NotesList") is ListBox && window.FindName("NotesBodyBox") is TextBox, "Notes list and editor are missing");
            Check(window.FindName("TextTitleBox") is null && window.FindName("TextBodyBox") is null, "Legacy projection text editor remains");
            Check(window.FindName("AddSelectedBibleButton") is null && window.FindName("AddBiblePassageButton") is null, "Bible add buttons remain");
            Check(window.FindName("ClearOrderMenuItem") is MenuItem { Header: "Eliminar todo" }, "Order menu has no clear-all action");
            var filters = ((ListBox)window.FindName("LibraryFilter")).Items.OfType<ListBoxItem>().Select(i => i.Content.ToString()).ToArray();
            Check(filters.SequenceEqual(new[] { "Todo", "Himnos", "Audio" }), "Hymnal still contains removed filters");
        });
        if (window.FindName("NotesList") is not ListBox list) return;
        var data = Field<WorkspaceData>(window, "_workspace");
        var store = Field<WorkspaceStore>(window, "_store");
        IList Notes(WorkspaceData value) => (IList)value.GetType().GetProperty("Notes")!.GetValue(value)!;
        string Body(object note) => (string)note.GetType().GetProperty("Body")!.GetValue(note)!;
        var libraryCount = data.Library.Count;
        var plan = Field<System.Collections.ObjectModel.ObservableCollection<Cue>>(window, "_plan");
        var originalOrder = plan.ToArray();
        Test("Notes autosaves multiple documents without adding content to the library or order", () =>
        {
            Call(window, "NewNoteClick", window, new RoutedEventArgs());
            ((TextBox)window.FindName("NotesTitleBox")).Text = "Preparación del culto";
            ((TextBox)window.FindName("NotesBodyBox")).Text = "Oración inicial\n  Mantener esta sangría.\nCanción y predicación.";
            foreach (var name in new[] { "NotesTitleBox", "NotesBodyBox" })
                Check(ThemeContrast(((TextBox)window.FindName(name)).Foreground, (System.Windows.Media.Brush)window.FindResource("PanelBrush")) >= 4.5,
                    "Note editor text is unreadable: " + name);
            Until(() => Notes(store.Load()).Cast<object>().Any(n => Body(n).Contains("Mantener esta sangría.")), "Note was not automatically written to local storage");
            Call(window, "NotesBackClick", window, new RoutedEventArgs());
            Call(window, "NewNoteClick", window, new RoutedEventArgs());
            ((TextBox)window.FindName("NotesTitleBox")).Text = "Reunión del sábado";
            ((TextBox)window.FindName("NotesBodyBox")).Text = "Confirmar los horarios.";
            Call(window, "NotesBackClick", window, new RoutedEventArgs());
            Check(Notes(store.Load()).Count == 2 && list.Items.Count == 2, "Reopening storage lost a note");
            var reopened = new MainWindow { Owner = window, WindowState = WindowState.Normal, Width = 1200, Height = 680, ShowInTaskbar = false };
            try
            {
                reopened.Show(); reopened.UpdateLayout(); PumpFor(100);
                Call(reopened, "ActivateWorkspacePanel", "text"); reopened.UpdateLayout();
                var savedList = (ListBox)reopened.FindName("NotesList");
                Check(savedList.Items.Count == 2, "A new program window did not restore saved notes");
                savedList.SelectedItem = savedList.Items.Cast<object>().Single(note => Body(note).Contains("sangría"));
                Check(((TextBox)reopened.FindName("NotesBodyBox")).Text.Contains("\n  Mantener"), "A reopened note lost its body");
            }
            finally
            {
                Call(reopened, "DisposePresenterHotkeys"); Call(reopened, "DisposeLibraryFolders"); Call(reopened, "DisposeYouTube");
                Field<IDisposable>(reopened, "_preview").Dispose();
                Field<System.Windows.Threading.DispatcherTimer>(reopened, "_timer").Stop();
                SetField(reopened, "_closing", true); reopened.Close(); window.Activate();
            }
            var capture = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "notes");
            System.IO.Directory.CreateDirectory(capture);
            window.UpdateLayout(); CaptureDesignWindow(window, System.IO.Path.Combine(capture, "list.png"));
            Check(data.Library.Count == libraryCount && plan.Count == originalOrder.Length, "Private notes changed public content");
            ((TextBox)window.FindName("NotesSearchBox")).Text = "sangría";
            Check(list.Items.Count == 1, "Notes search did not match the saved body");
            list.SelectedItem = list.Items[0];
            Check(((TextBox)window.FindName("NotesBodyBox")).Text.Contains("\n  Mantener"), "Opening a note lost whitespace");
            window.UpdateLayout(); CaptureDesignWindow(window, System.IO.Path.Combine(capture, "editor.png"));
        });
        Test("Deleting a note persists and clearing the order keeps notes and live output", () =>
        {
            Call(window, "DeleteCurrentNote");
            Check(Notes(store.Load()).Count == 1, "Deleted note returned from local storage");
            var cue = new Cue { Kind = CueKind.Text, Title = "En programa", Body = "La salida continúa" };
            Call(window, "PrepareCue", cue, null); Call(window, "ProjectPrepared");
            var session = Field<PresentationSession>(window, "_session");
            var live = session.Live;
            var output = Field<object>(window, "_output");
            Call(window, "ClearOrder");
            Check(plan.Count == 0 && store.Load().Plan.Items.Count == 0, "Clear all did not persist an empty order");
            Check(live?.Id == cue.Id && ReferenceEquals(session.Live, live) && ReferenceEquals(output, Field<object>(window, "_output")), "Clearing the order changed the live output");
            Check(Notes(store.Load()).Count == 1 && data.Library.Count == libraryCount, "Clear all removed notes or library content");
            Call(window, "CloseOutput");
            foreach (var item in originalOrder) plan.Add(item);
            Call(window, "SaveWorkspace");
        });
    }
}
