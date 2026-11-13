using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunSequenceRemovalTests(MainWindow window)
    {
        var dock = (DockingManager)window.FindName("WorkspaceDock");
        var initial = (string)Call(window, "SerializeDockLayout")!;
        try
        {
            Test("Sequence is absent from every preset and workspace menu", () =>
            {
                foreach (var preset in new[] { "principal", "compacta", "proyeccion" })
                {
                    Call(window, "ApplyWorkspacePreset", preset, false); window.UpdateLayout();
                    var panels = dock.Layout.Descendents().OfType<LayoutAnchorable>().ToArray();
                    Check(panels.Length == 8 && panels.All(p => p.ContentId != "sequence"), "Preset still contains Sequence");
                    Check(window.FindName("SequenceList") is null && window.FindName("SequencePanel") is null, "Retired Sequence controls still exist");
                    Check(!((Button)window.FindName("WorkspaceMenuButton")).ContextMenu.Items.OfType<MenuItem>().Any(p => (string?)p.Tag == "sequence"), "Menu still exposes Sequence");
                }
            });
            Test("Older layouts drop docked hidden and floating Sequence without changing retained panels", () =>
            {
                Call(window, "RestoreDockLayout", initial);
                var controls = dock.Layout.Descendents().OfType<LayoutAnchorable>().Where(p => p.ContentId != "sequence").ToDictionary(p => p.ContentId, p => p.Content);
                foreach (var placement in new[] { "docked", "hidden", "floating" })
                {
                    var document = XDocument.Parse(initial);
                    document.Descendants("LayoutAnchorable").Where(p => (string?)p.Attribute("ContentId") == "sequence").Remove();
                    var retired = new XElement("LayoutAnchorable", new XAttribute("ContentId", "sequence"), new XAttribute("Title", "Secuencia"));
                    if (placement == "hidden") document.Root!.Element("Hidden")!.Add(retired);
                    else if (placement == "floating") document.Root!.Element("FloatingWindows")!.Add(new XElement("LayoutAnchorableFloatingWindow", new XElement("LayoutAnchorablePaneGroup", new XElement("LayoutAnchorablePane", retired))));
                    else document.Descendants("LayoutAnchorable").Single(p => (string?)p.Attribute("ContentId") == "order").Parent!.Add(retired);
                    Call(window, "RestoreDockLayout", document.ToString()); window.UpdateLayout();
                    Check(dock.Layout.Descendents().OfType<LayoutAnchorable>().Count() == 8, "Retired Sequence was not migrated");
                    Check(!((string)Call(window, "SerializeDockLayout")!).Contains("ContentId=\"sequence\""), "Saved layout reintroduced Sequence");
                    foreach (var pane in dock.Layout.Descendents().OfType<LayoutAnchorable>()) Check(ReferenceEquals(controls[pane.ContentId], pane.Content), "Migration replaced a retained panel");
                }
            });
        }
        finally { Call(window, "RestoreDockLayout", initial); }
    }
}
