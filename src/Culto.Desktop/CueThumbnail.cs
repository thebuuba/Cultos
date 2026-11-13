using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;

namespace Culto.Desktop;

// Text is a miniature of the cue itself. Video/audio use a cover rather than decoding extra players.
public sealed class CueThumbnail : FrameworkElement
{
    public static readonly DependencyProperty CueProperty = DependencyProperty.Register(nameof(Cue), typeof(Cue), typeof(CueThumbnail),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    private static readonly System.Threading.SemaphoreSlim ImageWork = new(2);
    private static readonly Typeface Face = new("Segoe UI");
    private static readonly Brush Back = new SolidColorBrush(Color.FromRgb(16, 23, 34));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(36, 178, 126));
    private static readonly Geometry Play = Geometry.Parse("M 57,20 L 76,31 L 57,42 Z");
    private ImageSource? _image;
    private int _generation;
    public Cue? Cue { get => (Cue?)GetValue(CueProperty); set => SetValue(CueProperty, value); }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var thumbnail = (CueThumbnail)sender;
        thumbnail._image = null;
        var generation = ++thumbnail._generation;
        if (e.NewValue is Cue { Kind: CueKind.Image, FilePath: { } path })
            _ = thumbnail.LoadImage(path, generation);
    }

    private async Task LoadImage(string path, int generation)
    {
        try
        {
            await ImageWork.WaitAsync();
            BitmapImage? bitmap;
            try
            {
                if (generation != _generation) return;
                bitmap = await Task.Run(() =>
                {
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var image = new BitmapImage();
                    image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 256;
                    image.StreamSource = stream; image.EndInit(); image.Freeze();
                    return image;
                });
            }
            finally { ImageWork.Release(); }
            if (generation == _generation) { _image = bitmap; InvalidateVisual(); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException)
        { /* A missing/invalid image keeps its title cover; projection reports file errors separately. */ }
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        drawing.DrawRectangle(Back, null, new Rect(RenderSize));
        var fittedScale = Math.Min(ActualWidth / 128, ActualHeight / 72);
        drawing.PushTransform(new TranslateTransform((ActualWidth - 128 * fittedScale) / 2, (ActualHeight - 72 * fittedScale) / 2));
        drawing.PushTransform(new ScaleTransform(fittedScale, fittedScale));
        drawing.PushClip(new RectangleGeometry(new Rect(0, 0, 128, 72), 5, 5));
        drawing.DrawRectangle(Back, null, new Rect(0, 0, 128, 72));
        if (_image is { Width: > 0, Height: > 0 } image)
        {
            var scale = Math.Min(128 / image.Width, 72 / image.Height);
            drawing.DrawImage(image, new Rect((128 - image.Width * scale) / 2, (72 - image.Height * scale) / 2, image.Width * scale, image.Height * scale));
        }
        else if (Cue is { } cue)
        {
            var media = cue.Kind is CueKind.Video or CueKind.Audio;
            if (media) drawing.DrawGeometry(null, new Pen(Accent, 1.5), Play);
            var message = media || string.IsNullOrWhiteSpace(cue.Body) ? cue.Title : cue.Body;
            var words = new FormattedText(message, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face,
                media ? 8 : 9, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = 110, MaxTextHeight = media ? 21 : 51, TextAlignment = TextAlignment.Center, Trimming = TextTrimming.CharacterEllipsis };
            drawing.DrawText(words, new Point(9, media ? 46 : Math.Max(8, (72 - words.Height) / 2)));
        }
        drawing.Pop(); drawing.Pop(); drawing.Pop();
    }
}
