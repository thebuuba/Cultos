using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunVisualThemeTests(MainWindow window)
    {
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        Call(window, "DisposeYouTube");
        var workspace = Field<WorkspaceData>(window, "_workspace");
        workspace.Library.AddRange(new[]
        {
            new Cue { Kind = CueKind.Video, HymnNumber = 1, Title = "001 Cantad alegres al Señor", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") },
            new Cue { Kind = CueKind.Video, HymnNumber = 3, Title = "003 Unidos en espíritu", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") },
            new Cue { Kind = CueKind.Video, HymnNumber = 14, Title = "014 Engrandecido sea Dios", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") },
            new Cue { Kind = CueKind.Audio, Title = "Música de bienvenida", FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }
        });
        Call(window, "RefreshLibrary");
        Call(window, "PrepareCue", (Cue)((ListBox)window.FindName("PlanList")).Items[0], null);
        Call(window, "ProjectPrepared");
        PumpFor(450);
        Call(window, "PrepareCue", new Cue { Kind = CueKind.Bible, Title = "Lectura del culto", Reference = "Lectura bíblica", Version = "Vista de prueba", Body = "Preparamos juntos el orden del culto." }, null);
        var folder = Environment.GetEnvironmentVariable("CULTO_DESIGN_CAPTURE_DIR") ?? Path.Combine(Environment.CurrentDirectory, ".tools", "visual-theme");
        Directory.CreateDirectory(folder);
        foreach (var size in new[] { new Size(1480, 900), new Size(1280, 680), new Size(1200, 680) })
        {
            window.Width = size.Width; window.Height = size.Height;
            window.UpdateLayout(); PumpFor(180);
            if (size.Height == 680)
                Test($"Compact {size.Width} window keeps order and media actions visible with a usable output picture", () =>
                {
                    var client = (FrameworkElement)window.Content;
                    foreach (var name in new[] { "AddOrderButton", "AddMediaToPlanButton" })
                        CheckWithin((FrameworkElement)window.FindName(name), client, new Rect(client.RenderSize), name);
                    Check(((FrameworkElement)window.FindName("OutputViewport")).ActualHeight >= 80, "Compact output picture is smaller than 80 DIP");
                });
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(folder, $"console-{(int)size.Width}x{(int)size.Height}.png");
            using (var stream = File.Create(path)) encoder.Save(stream);
            Console.WriteLine("CAPTURE " + path);
        }
        window.Width = 1480; window.Height = 900; window.UpdateLayout();
        RunThemeTests(window);
        Test("Primary controls and live order text retain readable contrast", () =>
        {
            var project = (Button)window.FindName("ProjectButton");
            Check(ThemeContrast(project.Foreground, project.Background) >= 4.5, "Primary action text lacks contrast");
            foreach (var label in Descendants(project).OfType<TextBlock>())
                Check(ThemeContrast(label.Foreground, project.Background, label.Opacity) >= 4.5, "Primary action label lacks contrast: " + label.Text);
            var list = (ListBox)window.FindName("PlanList");
            list.ScrollIntoView(list.Items[0]); list.UpdateLayout();
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            row.ApplyTemplate();
            var surface = (Border)row.Template.FindName("Row", row);
            Check(surface.Background is SolidColorBrush brush && brush.Color.A == 255, "The live row has no visible state surface");
            foreach (var label in Descendants(row).OfType<TextBlock>().Where(label => label.IsVisible && label.Text.Length > 0))
                Check(ThemeContrast(label.Foreground, surface.Background) >= 4.5, "Live order text lacks contrast: " + label.Text);
            var muted = (Brush)window.FindResource("MutedBrush");
            var raised = (Brush)window.FindResource("RaisedBrush");
            Check(ThemeContrast(muted, raised) >= 4.5, "Metadata is unreadable on raised controls");
        });
    }
    private static double ThemeContrast(Brush foreground, Brush background, double opacity = 1)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte channel)
            {
                var value = channel / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        var ink = ((SolidColorBrush)foreground).Color;
        var paper = ((SolidColorBrush)background).Color;
        var alpha = opacity * foreground.Opacity * ink.A / 255d;
        var effective = Color.FromRgb((byte)Math.Round(ink.R * alpha + paper.R * (1 - alpha)),
            (byte)Math.Round(ink.G * alpha + paper.G * (1 - alpha)), (byte)Math.Round(ink.B * alpha + paper.B * (1 - alpha)));
        var first = Luminance(effective); var second = Luminance(paper);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

}
