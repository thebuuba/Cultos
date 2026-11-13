using System.Windows;
using System.Windows.Controls;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Desktop;
internal static partial class Program
{
    private static void RunFixedMonitorTests(MainWindow window)
    {
        Test("Monitors are one fixed content with transition between them", () =>
        {
            Call(window, "ApplyWorkspacePreset", "principal", false); window.UpdateLayout();
            var dock = (DockingManager)window.FindName("WorkspaceDock");
            var panels = dock.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
            Check(!panels.Any(p => p.ContentId == "output"), "Output is still an independent dock panel");
            var card = (FrameworkElement)window.FindName("ProductionPanel");
            var preview = (FrameworkElement)window.FindName("PreviewPanel");
            var output = (FrameworkElement)window.FindName("LivePanel");
            var transition = (FrameworkElement)window.FindName("TransitionCenter");
            Check(card.IsAncestorOf(preview) && card.IsAncestorOf(output), "Monitors have different content parents");
            Check(BoundsWithin(preview,card).Right <= BoundsWithin(transition,card).Left + 1 && BoundsWithin(transition,card).Right <= BoundsWithin(output,card).Left + 1, "Transition is not between the monitors");
            var model = panels.Single(p=>p.ContentId=="preview");
            Check(!model.CanHide && !model.CanFloat, "Monitor card remains movable or closable");
            Check((bool)Call(window,"PlaceWorkspacePanel","preview","order","bottom")! == false, "Monitor card moved");
            var old = System.Xml.Linq.XDocument.Parse((string)Call(window,"SerializeDockLayout")!);
            var order = old.Descendants("LayoutAnchorable").Single(e=>(string?)e.Attribute("ContentId")=="order");
            order.Parent!.Add(new System.Xml.Linq.XElement("LayoutAnchorable", new System.Xml.Linq.XAttribute("ContentId","output"), new System.Xml.Linq.XAttribute("Title","En pantalla")));
            Call(window,"RestoreDockLayout",old.ToString()); window.UpdateLayout();
            Check(!dock.Layout.Descendents().OfType<LayoutAnchorable>().Any(p=>p.ContentId=="output"),"Saved layout separated the monitors again");
            Check(card.IsAncestorOf(output) && card.IsAncestorOf(preview),"Migration replaced the combined card");
            CaptureDesignWindow(window, System.IO.Path.Combine(Environment.CurrentDirectory,".tools","fixed-monitors.png"));
        });
        RunPlayerControlsTests(window);
    }
}
