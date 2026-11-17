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
            Test("Grouped panel menu sits in the tab header and operates on the selected panel", () =>
            {
                Call(window, "ActivateWorkspacePanel", "bible"); window.UpdateLayout(); PumpFor(80);
                var pane = Descendants(dock).OfType<AvalonDock.Controls.LayoutAnchorablePaneControl>()
                    .Single(item => ReferenceEquals(item.Model, Panel("bible").Parent));
                var button = Descendants(pane).OfType<Button>().SingleOrDefault(item => item.Name == "WorkspacePanelMenu");
                Check(button is { IsVisible: true }, "Grouped panel has no menu in its tab header");
                var title = Descendants(pane).OfType<AvalonDock.Controls.AnchorablePaneTitle>().First(item => item.Model == Panel("bible"));
                Check(!title.IsVisible, "Grouped panel repeats its selected tab name below the header");
                Check(BoundsWithin(button!, pane).Bottom <= BoundsWithin((FrameworkElement)Panel("bible").Content, pane).Top,
                    "Panel menu is below the tab header");
                Check(!Descendants(title).OfType<Button>().Any(item => item.IsVisible), "Caption duplicates the header menu");
                button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var menu = title.ContextMenu;
                Check(menu?.IsOpen == true, "Header menu did not open the selected panel's actions");
                var close = menu!.Items.OfType<MenuItem>().Single(item => (string)item.Header == "Cerrar panel");
                menu.IsOpen = false; close.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check(Panel("bible").IsHidden && !Panel("sources").IsHidden, "Header menu closed a different panel");
                Visible("bible", true); Call(window, "RestoreDockLayout", initial); window.UpdateLayout();
            });
            Test("Panel caption has one menu button with working float dock and close actions", () =>
            {
                var title = Descendants(dock).OfType<AvalonDock.Controls.AnchorablePaneTitle>().First(t => t.IsVisible && t.Model?.ContentId == "scenes");
                var buttons = Descendants(title).OfType<Button>().ToArray();
                Check(buttons.Length == 1 && System.Windows.Automation.AutomationProperties.GetName(buttons[0]) == "Opciones del panel",
                    "Caption still has native close buttons instead of one panel menu");
                buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var menu = title.ContextMenu;
                Check(menu.IsOpen, "Caption button did not open panel menu");
                MenuItem Item(string name) => menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == name);
                var floating = Item("Desacoplar");
                menu.IsOpen = false;
                floating.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); window.UpdateLayout(); PumpFor(100);
                Check(Panel("scenes").IsFloating, "Detach menu did not float the panel");
                Panel("scenes").Dock(); dock.UpdateLayout(); PumpFor(100);
                Check(!Panel("scenes").IsFloating, "Panel could not dock again");
                menu.IsOpen = true;
                var close = Item("Cerrar panel"); menu.IsOpen = false;
                close.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Check(Panel("scenes").IsHidden, "Close menu did not hide panel");
                Visible("scenes", true); window.UpdateLayout();
                Check(!Panel("scenes").IsHidden, "Closed panel could not be recovered");
            });
            Test("Source panels can hide and return while joined monitors stay fixed", () =>
            {
                foreach (var id in original.Keys)
                {
                    Visible(id, false); window.UpdateLayout();
                    Check(Panel(id).IsHidden == (id != "preview"), "Unexpected panel visibility: " + id);
                    Visible(id, true); window.UpdateLayout();
                    Check(!Panel(id).IsHidden && ReferenceEquals(Panel(id).Content, original[id]), "Showing recreated or lost " + id);
                }
            });
            Test("Hidden panels persist through serialization and window resizing", () =>
            {
                Visible("text", false); Visible("youtube", false);
                Call(window, "SaveDockLayout"); var saved = (string)Call(window, "SerializeDockLayout")!;
                Call(window, "RestoreDockLayout", initial); Call(window, "RestoreDockLayout", saved); window.UpdateLayout();
                Check(Panel("text").IsHidden && Panel("youtube").IsHidden, "Restore forced hidden panels open");
                var width = window.Width; window.Width = 1250; PumpFor(80); window.Width = width; PumpFor(80);
                Check(Panel("text").IsHidden && Panel("youtube").IsHidden, "Resizing reset visibility");
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
                Check(original.Keys.All(id => Panel(id).IsHidden == (id != "preview")), "Source panels did not hide or fixed monitors disappeared");
                var saved = (string)Call(window, "SerializeDockLayout")!;
                Call(window, "RestoreDockLayout", saved); window.UpdateLayout();
                Call(window, "ShowAllWorkspacePanelsClick", window, new RoutedEventArgs()); window.UpdateLayout();
                Check(original.Keys.All(id => !Panel(id).IsHidden), "Show all did not recover every panel");
                foreach (var entry in original) Check(ReferenceEquals(Panel(entry.Key).Content, entry.Value), "Recovery replaced content");
            });
            Test("Attempting to hide fixed monitors preserves both pictures and live playback", () =>
            {
                Call(window, "RestoreDockLayout", initial);
                ((Slider)window.FindName("VolumeSlider")).Value = 0; ((CheckBox)window.FindName("LoopCheck")).IsChecked = true;
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Video, Title = "Prueba", FilePath = System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
                Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output"); var session = Field<PresentationSession>(window, "_session");
                Until(() => output.IsPlaying && !output.IsTransitioning, "Video fixture did not start");
                var cue = session.Live;
                Visible("preview", false); PumpFor(100);
                Check(!Panel("preview").IsHidden, "Fixed monitor panel could be hidden");
                Check(output.IsPlaying && ReferenceEquals(cue, session.Live), "Hiding output stopped or changed the live video");
                Visible("preview", true); window.UpdateLayout(); PumpFor(100);
                Check(output.IsPlaying && ((FrameworkElement)window.FindName("LiveMonitor")).IsVisible
                    && ((FrameworkElement)window.FindName("PreviewViewport")).IsVisible, "Both monitor pictures did not return while playing");
                Call(window, "CloseOutput");
            });
        }
        finally { Call(window, "RestoreDockLayout", initial); window.UpdateLayout(); }
    }
}
