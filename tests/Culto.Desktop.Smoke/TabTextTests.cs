using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunTabTextTests(MainWindow window)
    {
        var dock = (DockingManager)window.FindName("WorkspaceDock");
        Test("Selected source tabs have distinct colors and new panels retain unused colors", () =>
        {
            var colors = new HashSet<Color>();
            var assigned = new Dictionary<string, Color>();
            foreach (var id in new[] { "sources", "bible", "youtube", "media", "powerpoint", "text", "playlists" })
            {
                Call(window, "ActivateWorkspacePanel", id); window.UpdateLayout(); PumpFor(40);
                var native = Descendants(dock).OfType<LayoutAnchorableTabItem>().Single(t => t.Model?.ContentId == id);
                var outer = Descendants(dock).OfType<TabItem>().Single(t => t.IsAncestorOf(native));
                var background = ((Border)outer.Template.FindName("TabSurface", outer)).Background;
                Check(background is SolidColorBrush brush && colors.Add(brush.Color), "Tabs repeat a selection color: " + id);
                foreach (var label in Descendants(native).OfType<TextBlock>())
                    Check(ThemeContrast(label.Foreground, background) >= 4.5, "Selection color makes the label unreadable: " + id);
            }
            for (var i = 0; i < 20; i++)
            {
                var id = "new-panel-" + i;
                var color = ((SolidColorBrush)Call(window, "GetPanelTabColor", id)!).Color;
                assigned[id] = color;
                Check(colors.Add(color), "New panel reused a reserved or previously assigned color");
                Check(((SolidColorBrush)Call(window, "GetPanelTabColor", id)!).Color == color, "Panel changed color on refresh");
            }
            var data = Field<WorkspaceData>(window, "_workspace");
            var path = System.IO.Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "tab-colors.json");
            JsonFileStore.Save(path, data);
            var restored = JsonFileStore.Load<WorkspaceData>(path)!;
            Check(assigned.Keys.All(id => restored.PanelTabColors[id] == data.PanelTabColors[id]), "Saved workspace lost panel colors");
            Field<Dictionary<string, SolidColorBrush>>(window, "_panelTabBrushes").Clear();
            foreach (var (id, color) in assigned)
                Check(((SolidColorBrush)Call(window, "GetPanelTabColor", id)!).Color == color, "Rebuilding tabs changed an assigned color");
            var pane = dock.Layout.Descendents().OfType<LayoutAnchorablePane>().Single(p => p.Children.Any(c => c.ContentId == "sources"));
            var added = new LayoutAnchorable { ContentId = "new-panel-in-strip", Title = "Panel nuevo", Content = new Border() };
            try
            {
                pane.Children.Add(added); added.IsSelected = true; window.UpdateLayout(); PumpFor(60);
                var native = Descendants(dock).OfType<LayoutAnchorableTabItem>().Single(t => t.Model == added);
                var outer = Descendants(dock).OfType<TabItem>().Single(t => t.IsAncestorOf(native));
                var surface = (Border)outer.Template.FindName("TabSurface", outer);
                Check(surface.Background is SolidColorBrush color && colors.Add(color.Color), "A newly grouped panel did not get an unused selection color");
            }
            finally { pane.Children.Remove(added); window.UpdateLayout(); }
        });
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
