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
        OutputWeb.Visibility = Visibility.Collapsed;
        OutputImage.Source = null;
        OutputImage.Visibility = Visibility.Collapsed;
        OutputText.Visibility = Visibility.Visible;

        Root.Background = MediaBrushes.Black;
        PresentationSurface.Background = snapshot.State == PresentationState.Black
            ? MediaBrushes.Black
            : new SolidColorBrush(MediaColor.FromRgb(21, 25, 21));
        OutputBackgroundImage.Visibility = snapshot.State == PresentationState.Black
            ? Visibility.Collapsed
            : Visibility.Visible;

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

            if (type == ContentType.Audio && File.Exists(snapshot.MediaPath))
            {
                OutputVideo.Source = new Uri(snapshot.MediaPath!);
                OutputVideo.Visibility = Visibility.Visible;
                OutputText.Visibility = Visibility.Visible;
                OutputText.Text = "♫\n" + snapshot.Title;
                OutputVideo.Play();
                return;
            }

            if (type == ContentType.Web && Uri.TryCreate(snapshot.Content, UriKind.Absolute, out var webUri))
            {
                OutputText.Visibility = Visibility.Collapsed;
                OutputWeb.Visibility = Visibility.Visible;
                UpdateWebSurfaceSize();
                OutputWeb.Source = webUri;
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
            PresentationState.Logo => "◇\n" + (string.IsNullOrWhiteSpace(snapshot.Content) ? "Iglesia local" : snapshot.Content),
            _ => snapshot.Content
        };
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateWebSurfaceSize();

    private void UpdateWebSurfaceSize()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        const double ratio = 16d / 9d;
        var availableRatio = ActualWidth / ActualHeight;

        if (availableRatio > ratio)
        {
            OutputWeb.Height = ActualHeight;
            OutputWeb.Width = ActualHeight * ratio;
        }
        else
        {
            OutputWeb.Width = ActualWidth;
            OutputWeb.Height = ActualWidth / ratio;
        }
    }

    public void SetDefaultBackground(string? path)
    {
        OutputBackgroundImage.Source = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            OutputBackgroundImage.Source = image;
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo cargar el fondo predeterminado en la salida externa", ex);
        }
    }

    public void SetVolume(double volume) => OutputVideo.Volume = Math.Clamp(volume, 0, 1);
    public void SetPosition(TimeSpan position)
    {
        if (OutputVideo.Source is not null) OutputVideo.Position = position;
    }
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
