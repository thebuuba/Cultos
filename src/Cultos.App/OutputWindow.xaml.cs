using Cultos.Core;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace Cultos.App;

public partial class OutputWindow : Window
{
    public event EventHandler<string>? MediaError;
    public event EventHandler? MediaEnded;

    public OutputWindow() => InitializeComponent();

    public void Render(PresentationSnapshot snapshot, ContentType? type = null)
    {
        OutputVideo.Stop();
        OutputVideo.Source = null;
        OutputVideo.Visibility = Visibility.Collapsed;
        OutputImage.Source = null;
        OutputImage.Visibility = Visibility.Collapsed;
        OutputText.Visibility = Visibility.Visible;

        Root.Background = snapshot.State == PresentationState.Black
            ? MediaBrushes.Black
            : new SolidColorBrush(MediaColor.FromRgb(21, 25, 21));

        if (snapshot.State == PresentationState.Black)
        {
            OutputText.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            if (type == ContentType.Image && File.Exists(snapshot.MediaPath))
            {
                OutputText.Visibility = Visibility.Collapsed;
                OutputImage.Source = new BitmapImage(new Uri(snapshot.MediaPath!));
                OutputImage.Visibility = Visibility.Visible;
                return;
            }

            if (type == ContentType.Video && File.Exists(snapshot.MediaPath))
            {
                OutputText.Visibility = Visibility.Collapsed;
                OutputVideo.Source = new Uri(snapshot.MediaPath!);
                OutputVideo.Visibility = Visibility.Visible;
                OutputVideo.Play();
                return;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo renderizar multimedia en la salida", ex);
            MediaError?.Invoke(this, "No se pudo abrir el archivo multimedia.");
        }

        OutputText.Text = snapshot.State switch
        {
            PresentationState.Empty => "",
            PresentationState.Logo => "◇\nIglesia local",
            _ => snapshot.Content
        };
    }

    public void SetVolume(double volume) => OutputVideo.Volume = Math.Clamp(volume, 0, 1);
    public void PlayMedia() => OutputVideo.Play();
    public void PauseMedia() => OutputVideo.Pause();
    public void StopMedia() => OutputVideo.Stop();

    private void OutputVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        AppLogger.Error("Error de reproducción en pantalla externa", e.ErrorException);
        OutputVideo.Stop();
        OutputVideo.Visibility = Visibility.Collapsed;
        OutputText.Visibility = Visibility.Visible;
        OutputText.Text = "No se pudo reproducir este video";
        MediaError?.Invoke(this, e.ErrorException?.Message ?? "Formato o códec no compatible.");
    }

    private void OutputVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        MediaEnded?.Invoke(this, EventArgs.Empty);
    }
}
