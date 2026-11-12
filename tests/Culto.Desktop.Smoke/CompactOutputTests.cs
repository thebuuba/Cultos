using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunCompactOutputTests(MainWindow window)
    {
        Test("Narrow output panel fits the video and wraps all playback controls", () =>
        {
            var initial = (string)Call(window, "SerializeDockLayout")!;
            var width = window.Width;
            var session = Field<PresentationSession>(window, "_session");
            try
            {
                var doc = XDocument.Parse(initial);
                // Build this diagnostic layout by content ID, independent of which
                // tabs currently share a pane or which tab is first in that group.
                var panes = doc.Descendants("LayoutAnchorable").ToDictionary(p => (string)p.Attribute("ContentId")!,
                    p => new XElement("LayoutAnchorablePane", new XElement(p)));
                doc.Descendants("LayoutAnchorable").Remove();
                doc.Root!.Element("FloatingWindows")?.RemoveNodes();
                doc.Root.Element("Hidden")?.RemoveNodes();
                panes["sequence"].Add(panes["media"].Element("LayoutAnchorable"));
                foreach (var entry in panes.Values) entry.SetAttributeValue("DockWidth", "*");
                panes["order"].SetAttributeValue("DockMinWidth", "390");
                panes["output"].SetAttributeValue("DockMinWidth", "360");
                var root = doc.Root!.Element("RootPanel")!; root.RemoveNodes(); root.SetAttributeValue("Orientation", "Vertical");
                root.Add(new XElement("LayoutPanel", new XAttribute("Orientation", "Horizontal"), panes["sources"], panes["order"], panes["preview"], panes["output"]), panes["sequence"]);
                window.Width = 1280;
                Call(window, "RestoreDockLayout", doc.ToString()); window.UpdateLayout();
                var panel = (FrameworkElement)window.FindName("LivePanel");
                var viewport = (FrameworkElement)window.FindName("OutputViewport");
                CheckWithin(viewport, window, new Rect(window.RenderSize), "Output in restored four-column workspace");
                Call(window, "PrepareCue", new Cue { Kind = CueKind.Video, Title = "Video compacto", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }, null);
                Call(window, "ProjectPrepared");
                var live = session.Live; var prepared = session.Prepared;
                var manager = (DockingManager)window.FindName("WorkspaceDock");
                var pane = (LayoutAnchorablePane)manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == "output").Parent;
                foreach (var size in new[] { 240d, 280d, 420d, 240d })
                {
                    window.Width = 1600; pane.DockWidth = new GridLength(size); window.UpdateLayout();
                    ((ScrollViewer)window.FindName("LiveScroll")).ScrollToTop(); window.UpdateLayout(); PumpFor(60);
                    Check(panel.ActualWidth <= size + 3, "Panel refuses the requested compact width");
                    CheckWithin(viewport, panel, new Rect(panel.RenderSize), "Video viewport");
                    Check(Math.Abs(viewport.ActualWidth / viewport.ActualHeight - 16d / 9) < .04, "Compact video lost 16:9 aspect ratio");
                    foreach (var control in Descendants(panel).OfType<FrameworkElement>().Where(e => e is Button or Slider or CheckBox && e.IsVisible))
                    {
                        control.BringIntoView(); window.UpdateLayout(); PumpFor(20);
                        CheckWithin(control, panel, new Rect(panel.RenderSize), "Output control " + (control.Name.Length > 0 ? control.Name : control.GetType().Name));
                    }
                    Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Resizing replaced the presentation");
                }
            }
            finally { window.Width = width; Call(window, "RestoreDockLayout", initial); }
        });
        Test("Small floating output keeps its picture and backdrop actions inside the panel", () =>
        {
            var initial = (string)Call(window, "SerializeDockLayout")!;
            try
            {
                var manager = (DockingManager)window.FindName("WorkspaceDock");
                var output = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == "output");
                var panel = (FrameworkElement)window.FindName("LivePanel");
                output.Float(); window.UpdateLayout(); PumpFor(80);
                var floating = manager.FloatingWindows.Single(host => host.Model.Descendents().Contains(output));
                floating.Width = 240; floating.Height = 260;
                floating.UpdateLayout(); PumpFor(80);
                var client = (FrameworkElement)PresentationSource.FromVisual(panel).RootVisual;
                Console.WriteLine($"COMPACT Floating host {floating.ActualWidth}x{floating.ActualHeight}; output panel {panel.ActualWidth}x{panel.ActualHeight}");
                CheckWithin(panel, client, new Rect(client.RenderSize), "Small floating output panel");
                foreach (var name in new[] { "OutputViewport", "ChurchBackgroundButton", "BlackoutButton" })
                    CheckWithin((FrameworkElement)window.FindName(name), panel, new Rect(panel.RenderSize), name);
                Check(((FrameworkElement)window.FindName("OutputViewport")).ActualHeight >= 80, "Small floating output picture is too small");
                var scroll = (ScrollViewer)window.FindName("LiveScroll");
                foreach (var control in Descendants(scroll).OfType<FrameworkElement>()
                    .Where(control => control.IsVisible && control is Button or Slider or CheckBox))
                {
                    control.BringIntoView(); floating.UpdateLayout(); PumpFor(20);
                    CheckWithin(control, scroll, new Rect(scroll.RenderSize), "Small output control " + control.Name);
                }
            }
            finally { Call(window, "RestoreDockLayout", initial); }
        });
    }
}
