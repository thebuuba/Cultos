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
            ((Expander)window.FindName("PowerPointUsbOptions")).IsExpanded = true;
            window.UpdateLayout();
            foreach (var name in new[] { "PowerPointFiles", "PowerPointProjectButton", "PowerPointPreviousButton", "PowerPointNextButton",
                "PowerPointPauseButton", "PowerPointRestartButton", "PowerPointPositionText", "PowerPointUsbCheck", "PowerPointUsbKeys" })
                Check(window.FindName(name) is FrameworkElement { IsVisible: true }, "Card control is missing or hidden: " + name);
            Check(!((Button)window.FindName("PowerPointNextButton")).IsEnabled, "Empty card can advance unrelated content");
            CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "powerpoint-card.png"));
            var legacy = XDocument.Parse((string)Call(window, "SerializeDockLayout")!);
            legacy.Descendants("LayoutAnchorable").Single(p => (string?)p.Attribute("ContentId") == "powerpoint").Remove();
            Call(window, "RestoreDockLayout", legacy.ToString());
            Check(manager.Layout.Descendents().OfType<LayoutAnchorable>().Count(p => p.ContentId == "powerpoint") == 1,
                "Saved older workspace loses the new PowerPoint card");
            var files = (ListBox)window.FindName("PowerPointFiles");
            var path = Path.Combine(directory, "Imported presentation.pptx"); File.WriteAllText(path, "Import-only fixture");
            Wait((Task)Call(window, "ImportMediaAsync", (object)new[] { path })!);
            Call(window, "ActivateWorkspacePanel", "powerpoint"); window.UpdateLayout();
            Check(files.Items.OfType<Cue>().Any(c => c.Kind == CueKind.PowerPoint && c.FilePath == path), "Imported PowerPoint missing from its card");
            var previousOutput = Field<Culto.Desktop.Projection.ProjectionSurface?>(window, "_output");
            var previousLive = Field<PresentationSession>(window, "_session").Live?.Id;
            files.SelectedItem = files.Items.OfType<Cue>().Single(c => c.FilePath == path);
            Check(Field<PresentationSession>(window, "_session").Prepared?.FilePath == path, "Card selection does not prepare PowerPoint");
            Check(ReferenceEquals(Field<Culto.Desktop.Projection.ProjectionSurface?>(window, "_output"), previousOutput)
                && Field<PresentationSession>(window, "_session").Live?.Id == previousLive, "Selecting PowerPoint changed output automatically");
        });
    }
}
