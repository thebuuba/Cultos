using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunWorkspaceGeometryTests(MainWindow window)
    {
        window.Width = 1900; window.Height = 1000;
        Call(window, "ApplyWorkspacePreset", "principal", false); window.UpdateLayout(); PumpFor(80);
        var manager = (DockingManager)window.FindName("WorkspaceDock");
        LayoutAnchorablePaneControl Pane(string id) => Descendants(manager).OfType<LayoutAnchorablePaneControl>()
            .Single(pane => pane.Model is LayoutAnchorablePane model && model.Children.Any(content => content.ContentId == id));
        Test("Workspace panels have a narrow consistent gap", () =>
        {
            var scenes = BoundsWithin(Pane("scenes"), manager);
            var monitors = BoundsWithin(Pane("preview"), manager);
            var order = BoundsWithin(Pane("order"), manager);
            Check(monitors.Left - scenes.Right is >= 3 and <= 5, $"Horizontal panel gap is {monitors.Left - scenes.Right}");
            Check(order.Top - scenes.Bottom is >= 3 and <= 5, $"Vertical panel gap is {order.Top - scenes.Bottom}");
        });
        Test("Workspace corners are subtly rounded and the monitor card fills its panel", () =>
        {
            foreach (var id in new[] { "scenes", "order" })
                Check(Pane(id).Clip is RectangleGeometry { RadiusX: 8, RadiusY: 8 }, id + " does not have the workspace corner radius");
            var production = (Border)window.FindName("ProductionPanel");
            var source = (Border)window.FindName("SourcesPanel");
            Check(source.CornerRadius.BottomLeft == 8 && source.CornerRadius.BottomRight == 8, "Tabbed source card retains square bottom corners");
            var caption = Descendants(Pane("sources")).OfType<AnchorablePaneTitle>().Single();
            var captionFrame = (Border)VisualTreeHelper.GetChild(caption, 0);
            Check(captionFrame.CornerRadius.TopLeft == 8 && captionFrame.CornerRadius.TopRight == 8, "Tabbed source card retains square top corners");
            Check(production.CornerRadius == new CornerRadius(8), "Monitor card retains large rounded corners");
            var monitors = BoundsWithin(production, manager);
            var order = BoundsWithin(Pane("order"), manager);
            Check(order.Top - monitors.Bottom is >= 3 and <= 5, "Monitor card leaves a large empty gap above the order");
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "workspace-geometry.png"));
        });
    }
}
