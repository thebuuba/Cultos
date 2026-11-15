using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;

namespace Culto.Desktop.Dialogs;

public sealed partial class TransitionDialog
{
    private ProjectionSurface? _transitionPreview;
    private string? _previewFolder;
    private bool _previewPlaying;

    private void BuildTransitionPreview(Panel parent)
    {
        var status = new TextBlock { Text = "La vista previa usa dos imágenes de muestra. La proyección del culto no cambia.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 6) }; parent.Children.Add(status);
        var host = new Border { Height = 180, Background = Brushes.Black, CornerRadius = new CornerRadius(10), ClipToBounds = true };
        parent.Children.Add(host);
        var play = DialogUi.Button("Probar transición", () => { }); play.Name = "PreviewTransition"; parent.Children.Add(play);
        play.Click += async (_, _) =>
        {
            if (_previewPlaying) return;
            _previewPlaying = true; play.IsEnabled = false;
            try
            {
                if (_transitionPreview is null)
                {
                    _transitionPreview = new ProjectionSurface { Volume = 0, Width = 320, Height = 180, HorizontalAlignment = HorizontalAlignment.Center };
                    _transitionPreview.PlaybackError += message => status.Text = message;
                    host.Child = _transitionPreview;
                }
                _previewFolder ??= Path.Combine(Path.GetTempPath(), "CultoTransitionPreview-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_previewFolder);
                Cue Sample(string name, Color color)
                {
                    var drawing = new DrawingVisual();
                    using (var dc = drawing.RenderOpen())
                    {
                        dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 640, 360));
                        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), null, new Rect(60, 55, 520, 250), 20, 20);
                        dc.DrawText(new FormattedText(name, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"), 52, Brushes.White, 1), new Point(185, 142));
                    }
                    var bitmap = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var path = Path.Combine(_previewFolder!, name + ".png"); using (var stream = File.Create(path)) encoder.Save(stream);
                    return new Cue { Kind = CueKind.Image, Title = name, FilePath = path };
                }
                var from = Sample("Escena A", Color.FromRgb(24, 105, 79)); var to = Sample("Escena B", Color.FromRgb(53, 78, 150));
                _transitionPreview.Transitions = Options with { AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
                _transitionPreview.ShowCue(from); UpdateLayout(); await Task.Delay(80);
                status.Text = "Reproduciendo vista previa…";
                _transitionPreview.TransitionTo(to);
                var deadline = DateTime.UtcNow.AddSeconds(35);
                while (_transitionPreview.IsTransitioning && IsVisible && DateTime.UtcNow < deadline) await Task.Delay(25);
                if (status.Text == "Reproduciendo vista previa…") status.Text = _transitionPreview.IsTransitioning ? "El motor no terminó la vista previa." : "Vista previa terminada.";
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
}
