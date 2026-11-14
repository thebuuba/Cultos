using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Controls;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunTabTextTests(MainWindow window)
    {
        var dock = (DockingManager)window.FindName("WorkspaceDock");
        Test("Source tab labels are not rounded off and retain readable text size", () =>
        {
            Call(window, "ApplyWorkspacePreset", "principal", false);
            window.UpdateLayout(); PumpFor(100);
            var tabs = Descendants(dock).OfType<LayoutAnchorableTabItem>().Where(t => t.IsVisible).ToArray();
            Check(tabs.Length >= 7, "Missing source tabs");
            foreach (var tab in tabs)
            {
                var labels = Descendants(tab).OfType<TextBlock>().Where(t => t.IsVisible).ToArray();
                Check(tab.Clip is not RectangleGeometry { RadiusX: > 0 }, "Rounded tab clip cuts into its label");
                Check(Descendants(dock).OfType<TabItem>().Any(outer => outer.ActualHeight >= 34 && outer.IsAncestorOf(tab)), "Source tabs did not receive header sizing");
                Check(labels.All(label => label.FontSize >= 13), "Native theme overrides the readable tab font size");
                Check(labels.All(label => label.FontFamily.Equals(window.FindResource("ConsoleFont"))), "Tab labels lost the console font");
                var outer = Descendants(dock).OfType<TabItem>().Single(item => item.IsAncestorOf(tab));
                if (outer.IsSelected)
                {
                    var surface = (Border)outer.Template.FindName("TabSurface", outer);
                    Check(labels.All(label => ThemeContrast(label.Foreground, surface.Background) >= 4.5),
                        "Selected source tab text lacks contrast on its pastel surface");
                }
            }
            var folder = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "visual-theme");
            System.IO.Directory.CreateDirectory(folder);
            CaptureDesignWindow(window, System.IO.Path.Combine(folder, "tab-text.png"));
        });
    }
}
