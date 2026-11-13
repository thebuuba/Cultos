using System.Windows;
using System.Windows.Controls;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunPanelVisibilityTests(MainWindow window)
    {
        var dock = (DockingManager)window.FindName("WorkspaceDock");
        var initial = (string)Call(window, "SerializeDockLayout")!;
        LayoutAnchorable Panel(string id) => dock.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == id);
        var original = dock.Layout.Descendents().OfType<LayoutAnchorable>().ToDictionary(p => p.ContentId, p => p.Content);
        void Visible(string id, bool value) => Call(window, "SetWorkspacePanelVisible", id, value);
        try
        {
            Test("Every panel can hide and return with the same content instance", () =>
            {
                foreach (var id in original.Keys)
                {
                    Visible(id, false); window.UpdateLayout();
                    Check(Panel(id).IsHidden, "Panel did not hide: " + id);
                    Visible(id, true); window.UpdateLayout();
                    Check(!Panel(id).IsHidden && ReferenceEquals(Panel(id).Content, original[id]), "Showing recreated or lost " + id);
                }
            });
            Test("Hidden panels persist through serialization and window resizing", () =>
            {
                Visible("sequence", false); Visible("youtube", false);
                Call(window, "SaveDockLayout"); var saved = (string)Call(window, "SerializeDockLayout")!;
                Call(window, "RestoreDockLayout", initial); Call(window, "RestoreDockLayout", saved); window.UpdateLayout();
                Check(Panel("sequence").IsHidden && Panel("youtube").IsHidden, "Restore forced hidden panels open");
                var width = window.Width; window.Width = 1250; PumpFor(80); window.Width = width; PumpFor(80);
                Check(Panel("sequence").IsHidden && Panel("youtube").IsHidden, "Resizing reset visibility");
                foreach (var entry in original) Check(ReferenceEquals(Panel(entry.Key).Content, entry.Value), "Restoring replaced panel content");
            });
            Test("Workspace checks distinguish inactive tabs from hidden panels", () =>
            {
                Call(window, "WorkspacePanelsMenuOpened", ((Button)window.FindName("WorkspaceMenuButton")).ContextMenu, new RoutedEventArgs());
                var menu = ((Button)window.FindName("WorkspaceMenuButton")).ContextMenu;
                MenuItem Item(string id) => menu.Items.OfType<MenuItem>().Single(item => (string?)item.Tag == id);
                Check(!Item("youtube").IsChecked && Item("bible").IsChecked, "Visibility checks report inactive tabs as hidden");
                Item("youtube").IsChecked = true;
                Call(window, "ShowWorkspacePanelClick", Item("youtube"), new RoutedEventArgs());
                Check(!Panel("youtube").IsHidden && Panel("youtube").IsSelected, "Checking a hidden panel did not show it");
            });
            Test("Hiding all panels is recoverable without resetting the workspace", () =>
            {
                foreach (var id in original.Keys) Visible(id, false);
                Check(original.Keys.All(id => Panel(id).IsHidden), "Not all panels hid");
                var saved = (string)Call(window, "SerializeDockLayout")!;
                Call(window, "RestoreDockLayout", saved); window.UpdateLayout();
                Call(window, "ShowAllWorkspacePanelsClick", window, new RoutedEventArgs()); window.UpdateLayout();
                Check(original.Keys.All(id => !Panel(id).IsHidden), "Show all did not recover every panel");
                foreach (var entry in original) Check(ReferenceEquals(Panel(entry.Key).Content, entry.Value), "Recovery replaced content");
            });
            Test("Hiding the output panel preserves live video playback and restores its monitor", () =>
            {
                Call(window, "RestoreDockLayout", initial);
                ((Slider)window.FindName("VolumeSlider")).Value = 0; ((CheckBox)window.FindName("LoopCheck")).IsChecked = true;
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Video, Title = "Prueba", FilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
                Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output"); var session = Field<PresentationSession>(window, "_session");
                Until(() => output.IsPlaying && !output.IsTransitioning, "Video fixture did not start");
                var cue = session.Live;
                Visible("output", false); PumpFor(100);
                Check(output.IsPlaying && ReferenceEquals(cue, session.Live), "Hiding output stopped or changed the live video");
                Visible("output", true); window.UpdateLayout(); PumpFor(100);
                Check(output.IsPlaying && ((FrameworkElement)window.FindName("LiveMonitor")).IsVisible, "Output did not return while playing");
                Call(window, "CloseOutput");
            });
        }
        finally { Call(window, "RestoreDockLayout", initial); window.UpdateLayout(); }
    }
}
