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
                var panes = doc.Descendants("LayoutAnchorablePane").ToDictionary(p => (string)p.Element("LayoutAnchorable")!.Attribute("ContentId")!, p => new XElement(p));
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
    }
}
