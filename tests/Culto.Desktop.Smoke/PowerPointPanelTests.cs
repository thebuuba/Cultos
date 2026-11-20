using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunPowerPointPanelTests(MainWindow window, string directory)
    {
        Test("PowerPoint has its own source tab next to video lists and restores older workspaces", () =>
        {
            var manager = (DockingManager)window.FindName("WorkspaceDock");
            var panel = manager.Layout.Descendents().OfType<LayoutAnchorable>().SingleOrDefault(p => p.ContentId == "powerpoint");
            Check(panel is not null, "PowerPoint has no dedicated tab");
            var tabs = ((LayoutAnchorablePane)panel!.Parent).Children.Select(p => p.ContentId).ToList();
            Check(tabs.IndexOf("powerpoint") == tabs.IndexOf("playlists") + 1, "PowerPoint is not beside video lists");
            Call(window, "ActivateWorkspacePanel", "powerpoint"); window.UpdateLayout(); PumpFor(100);
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-card-compact.png"));
            var card = (Border)window.FindName("PowerPointPanel");
            panel.Content = null;
            var cardHost = new Window { Owner = window, Content = card, Height = 650, SizeToContent = SizeToContent.Width,
                ShowInTaskbar = false, Title = "PowerPoint · verificación del panel" };
            cardHost.Show();
            try
            {
            foreach (var width in new[] { 280d, 780d })
            {
                card.Width = width; cardHost.UpdateLayout();
                var viewport = new Rect(card.RenderSize);
                foreach (var name in new[] { "PowerPointProjectButton", "PowerPointAddToPlanButton", "PowerPointPreviousButton",
                    "PowerPointNextButton", "PowerPointPauseButton", "PowerPointRestartButton", "PowerPointModeratorButton", "PowerPointUsbOptions" })
                    CheckWithin((FrameworkElement)window.FindName(name), card, viewport, name + " at " + width);
                var image = new RenderTargetBitmap((int)card.ActualWidth, (int)card.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(card);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var capture = File.Create(Path.Combine(Environment.CurrentDirectory, ".tools", $"powerpoint-card-{width}.png"));
                encoder.Save(capture);
            }
            }
            finally
            {
                cardHost.Content = null; card.Width = double.NaN;
                panel.Content = card; cardHost.Close(); window.UpdateLayout();
            }
            ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("PowerPointUsbOptions")).IsChecked = true;
            window.UpdateLayout();
            foreach (var name in new[] { "PowerPointFiles", "PowerPointProjectButton", "PowerPointPreviousButton", "PowerPointNextButton",
                "PowerPointPauseButton", "PowerPointRestartButton", "PowerPointPositionText", "PowerPointUsbCheck", "PowerPointUsbKeys" })
                Check(window.FindName(name) is FrameworkElement { IsVisible: true }, "Card control is missing or hidden: " + name);
            Check(!((Button)window.FindName("PowerPointNextButton")).IsEnabled, "Empty card can advance unrelated content");
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-card.png"));
            ((System.Windows.Controls.Primitives.ToggleButton)window.FindName("PowerPointUsbOptions")).IsChecked = false;
            var legacy = XDocument.Parse((string)Call(window, "SerializeDockLayout")!);
            legacy.Descendants("LayoutAnchorable").Single(p => (string?)p.Attribute("ContentId") == "powerpoint").Remove();
            Call(window, "RestoreDockLayout", legacy.ToString());
            Check(manager.Layout.Descendents().OfType<LayoutAnchorable>().Count(p => p.ContentId == "powerpoint") == 1,
                "Saved older workspace loses the new PowerPoint card");
            var files = (ListBox)window.FindName("PowerPointFiles");
            Check(window.FindName("PowerPointSearch") is TextBox && window.FindName("PowerPointSlides") is ListBox,
                "PowerPoint browser is missing its search field or slide thumbnails");
            var path = Path.Combine(directory, "Imported presentation.pptx"); File.WriteAllText(path, "Import-only fixture");
            Wait((Task)Call(window, "ImportMediaAsync", (object)new[] { path })!);
            Call(window, "ActivateWorkspacePanel", "powerpoint"); window.UpdateLayout();
            var library = Field<Culto.Desktop.Services.WorkspaceData>(window, "_workspace").Library;
            var imported = library.Single(c => c.Kind == CueKind.PowerPoint && c.FilePath == path);
            Check(files.Items.Count == 1, "PowerPoint browser did not list its saved presentation");
            Call(window, "PrepareCue", imported, null);
            Check(files.Items.OfType<Cue>().Contains(imported), "PowerPoint browser lost the selected presentation");
            var previousOutput = Field<Culto.Desktop.Projection.ProjectionSurface?>(window, "_output");
            var previousLive = Field<PresentationSession>(window, "_session").Live?.Id;
            files.SelectedItem = files.Items.OfType<Cue>().Single(c => c.FilePath == path);
            files.UpdateLayout();
            var selectedRow = (ListBoxItem)files.ItemContainerGenerator.ContainerFromItem(files.SelectedItem);
            var selectionFrame = (Border)selectedRow.Template.FindName("ItemBorder", selectedRow);
            Check(selectionFrame.BorderThickness == new Thickness(0), "PowerPoint selection still draws an outline");
            Check(Field<PresentationSession>(window, "_session").Prepared?.FilePath == path, "Card selection does not prepare PowerPoint");
            Check(ReferenceEquals(Field<Culto.Desktop.Projection.ProjectionSurface?>(window, "_output"), previousOutput)
                && Field<PresentationSession>(window, "_session").Live?.Id == previousLive, "Selecting PowerPoint changed output automatically");
            var secondPath = Path.Combine(directory, "Second presentation.pptx");
            File.WriteAllText(secondPath, "Another import-only fixture");
            var second = new Cue { Kind = CueKind.PowerPoint, Title = "Second presentation", FilePath = secondPath };
            library.Add(second);
            Call(window, "PrepareCue", second, null);
            Check(files.Items.Count == 2 && ReferenceEquals(files.SelectedItem, second), "PowerPoint browser did not retain both files and select the current one");
            var search = (TextBox)window.FindName("PowerPointSearch");
            search.Text = "Second";
            Check(files.Items.Count == 1 && ReferenceEquals(files.Items[0], second), "Searching presentations did not filter by title");
            search.Clear();
            Call(window, "RefreshPowerPointFiles");
            Check(files.Items.Count == 2 && ReferenceEquals(files.SelectedItem, second), "Refreshing changed the library or current selection");
            Check(library.Contains(imported) && File.Exists(path) && File.Exists(secondPath), "Changing the current presentation removed saved data or original files");
            window.UpdateLayout(); PumpFor(80);
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-current-no-outline.png"));
        });
        RunPowerPointBrowserNativeTests(window, directory);
    }
}
