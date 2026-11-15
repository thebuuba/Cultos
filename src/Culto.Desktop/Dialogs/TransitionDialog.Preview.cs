using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;
using static Culto.Desktop.Dialogs.TransitionEditorUi;

namespace Culto.Desktop.Dialogs;

public sealed partial class TransitionDialog
{
    private ProjectionSurface? _transitionPreview;
    private string? _previewFolder;
    private bool _previewPlaying;

    private void BuildTransitionPreview(Panel parent)
    {
        var first = PreviewImage("BIENVENIDOS", "Un nuevo comienzo", Color.FromRgb(37, 76, 73));
        var second = PreviewImage("ALABANZA", "Cantemos juntos", Color.FromRgb(73, 53, 102));
        var host = new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(16), ClipToBounds = true,
            Child = new Image { Source = first, Stretch = Stretch.Uniform } };
        parent.Children.Add(new AspectRatioBox { Child = host });
        var status = Text("Prueba el efecto con imágenes de muestra, sin cambiar la salida del culto.", 12, Muted);
        status.Margin = new Thickness(0, 10, 0, 10);
        var play = Button("▶  Probar transición", "PreviewTransition", () => { });
        play.Background = Brushes.WhiteSmoke; play.Foreground = Brush("#191919"); play.Height = 38; parent.Children.Add(play); parent.Children.Add(status);
        play.Click += async (_, _) =>
        {
            if (_previewPlaying) return;
            _previewPlaying = true; play.IsEnabled = false;
            try
            {
                if (_transitionPreview is null)
                {
                    _transitionPreview = new ProjectionSurface { Volume = 0 };
                    _transitionPreview.PlaybackError += message => status.Text = message;
                    host.Child = _transitionPreview;
                }
                _previewFolder ??= Path.Combine(Path.GetTempPath(), "CultoTransitionPreview-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_previewFolder);
                Cue Sample(string name, BitmapSource bitmap)
                {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var path = Path.Combine(_previewFolder!, name + ".png"); using (var stream = File.Create(path)) encoder.Save(stream);
                    return new Cue { Kind = CueKind.Image, Title = name, FilePath = path };
                }
                var from = Sample("Escena A", first); var to = Sample("Escena B", second);
                _transitionPreview.Transitions = Options with { AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
                _transitionPreview.ShowCue(from); UpdateLayout(); await Task.Delay(80);
                if (!IsVisible) return;
                status.Text = "Reproduciendo vista previa…";
                _transitionPreview.TransitionTo(to);
                var deadline = DateTime.UtcNow.AddSeconds(35);
                while (_transitionPreview.IsTransitioning && IsVisible && DateTime.UtcNow < deadline) await Task.Delay(25);
                if (status.Text == "Reproduciendo vista previa…") status.Text = _transitionPreview.IsTransitioning ? "El motor no terminó la vista previa." : "Vista previa terminada. Puedes volver a probarla.";
            }
            catch (Exception error) { status.Text = "No se pudo mostrar la vista previa: " + error.Message; }
            finally { _previewPlaying = false; play.IsEnabled = true; }
        };
        Closed += (_, _) =>
        {
            _transitionPreview?.Dispose();
            if (_previewFolder is null) return;
            try
            {
                foreach (var name in new[] { "Escena A.png", "Escena B.png" }) File.Delete(Path.Combine(_previewFolder, name));
                Directory.Delete(_previewFolder);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };
    }
    private static BitmapSource PreviewImage(string kicker, string title, Color color)
    {
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(color, Color.FromRgb(13, 20, 29), 90), null, new Rect(0, 0, 960, 540));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), null, new Point(760, 120), 75, 75);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(65, 132, 174, 166)), null, Geometry.Parse("M0,430 L190,190 400,410 630,200 960,455 960,540 0,540 Z"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(110, 13, 28, 33)), null, Geometry.Parse("M0,470 L300,330 530,480 810,280 960,400 960,540 0,540 Z"));
            void Label(string text, double size, Brush brush, double y)
            {
                var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, 1);
                dc.DrawText(formatted, new Point((960 - formatted.Width) / 2, y));
            }
            Label(kicker, 18, Lime, 210); Label(title, 48, Brushes.White, 247);
        }
        var bitmap = new RenderTargetBitmap(960, 540, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing); bitmap.Freeze(); return bitmap;
    }
}
