using System.IO;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunDockingTests(MainWindow window, string directory)
    {
        var manager = (DockingManager)window.FindName("WorkspaceDock");
        var monitor = (OutputMonitor)window.FindName("LiveMonitor");
        var session = Field<PresentationSession>(window, "_session");
        var initial = (string)Call(window, "SerializeDockLayout")!;
        LayoutAnchorable Panel(string id) => manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == id);
        Test("Dock separators resize content before release and cancellation restores the initial sizes", () =>
        {
            var preview = Descendants(manager).OfType<AvalonDock.Controls.LayoutAnchorablePaneControl>().Single(p => ReferenceEquals(p.Model, Panel("preview").Parent));
            var live = session.Live; var prepared = session.Prepared;
            var splitter = Descendants(manager).OfType<AvalonDock.Controls.LayoutGridResizerControl>()
                .First(s => s.Parent is Grid grid && grid.ColumnDefinitions.Count >= 3 && grid.IsAncestorOf(preview) && !grid.IsAncestorOf((FrameworkElement)window.FindName("SourcesPanel"))
                    && grid.Children.OfType<FrameworkElement>().Any(child => child.IsAncestorOf(preview) && Grid.GetColumn(child) - 1 == Grid.GetColumn(s)));
            var before = preview.ActualWidth;
            try
            {
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragStartedEvent });
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(35, 0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
                window.UpdateLayout();
                Check(preview.ActualWidth < before - 20, "Panel waits until mouse release to resize");
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Resizing changed prepared/live content");
            }
            finally
            {
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(35, 0, true) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragCompletedEvent });
                window.UpdateLayout();
            }
            Check(Math.Abs(preview.ActualWidth - before) < 2, "Cancelled resize did not restore sizes");
        });
        Test("Vertical live resizing follows successive moves respects limits and persists on release", () =>
        {
            var preview = Descendants(manager).OfType<AvalonDock.Controls.LayoutAnchorablePaneControl>().Single(p => ReferenceEquals(p.Model, Panel("preview").Parent));
            var splitter = Descendants(manager).OfType<AvalonDock.Controls.LayoutGridResizerControl>()
                .First(s => s.Parent is Grid grid && grid.RowDefinitions.Count == 3 && grid.IsAncestorOf(preview));
            var before = preview.ActualHeight;
            try
            {
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragStartedEvent });
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0, 35) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
                window.UpdateLayout(); var expanded = preview.ActualHeight;
                Check(expanded > before + 20, "Vertical layout waits for release");
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0, -15) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
                window.UpdateLayout(); Check(preview.ActualHeight < expanded - 8, "Second mouse movement was not applied");
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0, -10000) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
                window.UpdateLayout(); Check(((FrameworkElement)window.FindName("PreviewViewport")).ActualHeight >= 80, "Live resize bypassed minimum monitor height");
                splitter.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(0, -10000, false) { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragCompletedEvent });
                window.UpdateLayout(); var committed = preview.ActualHeight;
                var saved = (string)Call(window, "SerializeDockLayout")!;
                Call(window, "RestoreDockLayout", initial); Call(window, "RestoreDockLayout", saved); window.UpdateLayout();
                Check(Math.Abs(preview.ActualHeight - committed) < 2, "Final resize was not persisted");
            }
            finally { Call(window, "RestoreDockLayout", initial); }
        });
        Test("Output monitor shares the local compositor and disconnects on close", () =>
        {
            Call(window, "PrepareCue", new Cue { Title = "Salida", Body = "Salida real" }, null);
            Call(window, "ProjectPrepared"); window.UpdateLayout();
            var output = Field<ProjectionSurface>(window, "_output");
            Check(ReferenceEquals(Field<System.Windows.Media.VisualBrush?>(monitor, "_localImage")?.Visual, output), "Monitor does not share the local compositor");
            Call(window, "PrepareCue", new Cue { Title = "Preparado", Body = "Vista previa independiente" }, null);
            Check(session.Live?.Body == "Salida real" && ReferenceEquals(Field<System.Windows.Media.VisualBrush?>(monitor, "_localImage")?.Visual, output), "Preparing changed or disconnected output");
            Call(window, "ToggleBlackout"); Check(session.IsBlackout && ReferenceEquals(Field<System.Windows.Media.VisualBrush?>(monitor, "_localImage")?.Visual, output), "Blackout detached monitor");
            Call(window, "ToggleBlackout");
            Call(window, "CloseOutput"); Check(Field<System.Windows.Media.VisualBrush?>(monitor, "_localImage") is null, "Closed output left a stale local source");
            Call(window, "OpenOutput", Culto.Desktop.Services.DisplayService.GetDisplays().First(), true);
            Call(window, "ProjectPrepared"); Check(ReferenceEquals(Field<System.Windows.Media.VisualBrush?>(monitor, "_localImage")?.Visual, Field<ProjectionSurface>(window, "_output")), "Reopened output did not reconnect to the compositor");
        });
        Test("All ten panels can float regroup and restore without replacing controls or session", () =>
        {
            var live = session.Live; var prepared = session.Prepared;
            var original = manager.Layout.Descendents().OfType<LayoutAnchorable>().ToDictionary(p => p.ContentId, p => p.Content);
            try
            {
                Check(original.Count == 10, "Missing dockable workspace panels");
                foreach (var id in original.Keys.Where(id => id != "preview"))
                {
                    var pane = Panel(id);
                    pane.Float(); window.UpdateLayout(); PumpFor(100);
                    Check(pane.IsFloating && PresentationSource.FromVisual((FrameworkElement)pane.Content) != PresentationSource.FromVisual(window), id + " did not float into a separate native window");
                    if (id == "preview") Until(() => { monitor.Refresh(); return monitor.IsConnected; }, $"Output mirror failed after floating: loaded={monitor.IsLoaded}, visible={monitor.IsVisible}, sourceVisible={Field<Window?>(monitor, "_source")?.IsVisible}, root={PresentationSource.FromVisual(monitor)?.RootVisual?.GetType().Name}");
                    pane.Dock(); window.UpdateLayout(); PumpFor(60);
                    Check(!pane.IsFloating, id + " did not dock back");
                }
                var order = Panel("order"); var target = (LayoutAnchorablePane)Panel("media").Parent;
                ((LayoutAnchorablePane)order.Parent).Children.Remove(order); target.Children.Add(order); order.IsSelected = true;
                Check(ReferenceEquals(order.Parent, Panel("media").Parent), "Panel grouping failed");
                Call(window, "SaveDockLayout");
                var saved = File.ReadAllText(Path.Combine(directory, "paneles-v3.xml"));
                Call(window, "RestoreDockLayout", initial);
                Call(window, "RestoreDockLayout", saved);
                Check(ReferenceEquals(Panel("order").Parent, Panel("media").Parent), "Saved grouping was lost");
                foreach (var entry in original) Check(ReferenceEquals(Panel(entry.Key).Content, entry.Value), "Restoring recreated " + entry.Key);
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Moving/restoring changed presentation session");
            }
            finally { Call(window, "RestoreDockLayout", initial); }
        });
        Test("Legacy five-panel layout migrates media and removes the retired sequence panel", () =>
        {
            try
            {
                var document = System.Xml.Linq.XDocument.Parse(initial);
                document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "scenes").Parent!.Remove();
                foreach (var id in new[] { "media", "bible", "text", "youtube", "playlists", "powerpoint" }) document.Descendants().Single(e => (string?)e.Attribute("ContentId") == id).Remove();
                var order = document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "order");
                order.Parent!.SetAttributeValue("DockWidth", "1.25*");
                order.Parent.Add(new System.Xml.Linq.XElement("LayoutAnchorable", new System.Xml.Linq.XAttribute("Title", "Retired"), new System.Xml.Linq.XAttribute("ContentId", "sequence")));
                order.Parent.Add(new System.Xml.Linq.XElement("LayoutAnchorable", new System.Xml.Linq.XAttribute("Title", "Old output"), new System.Xml.Linq.XAttribute("ContentId", "output")));
                Check(document.Descendants().Attributes("ContentId").Count() == 5, "Legacy fixture is not a complete five-panel layout");
                Call(window, "RestoreDockLayout", document.ToString());
                Check(manager.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 10, "Migration lost a panel or retained Sequence");
                Check(ReferenceEquals(Panel("media").Parent, Panel("order").Parent), "Migration reset the old media group");
                Check(((LayoutAnchorablePane)Panel("order").Parent).DockWidth == new GridLength(1.25, GridUnitType.Star), "Migration reset saved width");
            }
            finally { Call(window, "RestoreDockLayout", initial); }
        });
        Test("Invalid or incomplete layout is rejected before replacing current panels", () =>
        {
            var before = manager.Layout;
            foreach (var invalid in new[] { "<invalid", "<LayoutRoot />", initial.Replace("ContentId=\"preview\"", "ContentId=\"unknown\"") })
            {
                var rejected = false;
                try { Call(window, "RestoreDockLayout", invalid); } catch { rejected = true; }
                Check(rejected && ReferenceEquals(before, manager.Layout), "Invalid layout altered the workspace");
            }
        });
        Test("Floating layouts survive restore and offscreen titles return to a visible screen", () =>
        {
            try
            {
                Panel("youtube").Float(); window.UpdateLayout(); PumpFor(100);
                var document = System.Xml.Linq.XDocument.Parse((string)Call(window, "SerializeDockLayout")!);
                foreach (var element in document.Descendants().Where(e => e.Attribute("FloatingLeft") is not null))
                { element.SetAttributeValue("FloatingLeft", "-100000"); element.SetAttributeValue("FloatingTop", "-100000"); }
                Call(window, "RestoreDockLayout", document.ToString()); window.UpdateLayout(); PumpFor(100);
                Check(Panel("youtube").IsFloating, "Floating panel was not restored");
                var floating = manager.FloatingWindows.Single(w => w.Model.Descendents().Contains(Panel("youtube")));
                Check(floating.Left > -10000 && floating.Top > -10000, "Floating title remained offscreen");
                Check(!Panel("preview").IsFloating, "Restoring a floating source moved the fixed monitors");
            }
            finally { Call(window, "RestoreDockLayout", initial); }
        });
    }
}
