using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public sealed class MediaThumbnail : FrameworkElement
{
    public static readonly DependencyProperty CueProperty = DependencyProperty.Register(nameof(Cue), typeof(Cue), typeof(MediaThumbnail),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, CueChanged));
    private static readonly Typeface IconFace = new("Segoe MDL2 Assets");
    private static readonly Typeface LabelFace = new("Segoe UI");
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(187, 167, 255));
    private BitmapSource? _image;
    private CancellationTokenSource? _request;
    private int _generation;
    public Cue? Cue { get => (Cue?)GetValue(CueProperty); set => SetValue(CueProperty, value); }

    public MediaThumbnail()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => Load();
        Unloaded += (_, _) => Cancel();
        IsVisibleChanged += (_, _) => { if (IsVisible) Load(); else Cancel(); };
    }

    private static void CueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var thumbnail = (MediaThumbnail)sender;
        thumbnail.Cancel(); thumbnail._image = null;
        thumbnail.Load();
    }

    private void Cancel()
    {
        _generation++;
        _request?.Cancel(); _request?.Dispose(); _request = null;
    }

    private async void Load()
    {
        if (!IsLoaded || !IsVisible || _image is not null || _request is not null
            || Cue is not { Kind: CueKind.Video or CueKind.Image, FilePath: { } path } cue) return;
        var request = _request = new CancellationTokenSource();
        var generation = ++_generation;
        try
        {
            var image = await MediaThumbnailService.GetAsync(path, cue.Kind, request.Token);
            if (generation != _generation || !IsLoaded || !IsVisible) return;
            _image = image; InvalidateVisual();
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_request, request)) { _request = null; request.Dispose(); }
        }
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        drawing.PushClip(new RectangleGeometry(bounds, 7, 7));
        drawing.DrawRectangle(Brushes.Black, null, bounds);
        if (_image is { PixelWidth: > 0, PixelHeight: > 0 } image)
        {
            var scale = Math.Min(ActualWidth / image.PixelWidth, ActualHeight / image.PixelHeight);
            var width = image.PixelWidth * scale; var height = image.PixelHeight * scale;
            drawing.DrawImage(image, new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height));
        }
        else
        {
            var icon = Cue?.Kind switch { CueKind.Video => "\uE714", CueKind.Image => "\uEB9F", CueKind.Audio => "\uE8D6", _ => "\uE8A5" };
            var size = Math.Min(30, Math.Max(12, ActualHeight * .30));
            var text = new FormattedText(icon, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                IconFace, size, Accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawing.DrawText(text, new Point((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2 - (ActualHeight >= 64 ? 9 : 0)));
            if (ActualHeight >= 64 && ActualWidth >= 64)
            {
                var title = Cue?.Title ?? "";
                var label = new FormattedText(title, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    LabelFace, 11, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                { MaxTextWidth = Math.Max(1, ActualWidth - 16), MaxTextHeight = 16, TextAlignment = TextAlignment.Center,
                    Trimming = TextTrimming.CharacterEllipsis };
                drawing.DrawText(label, new Point(8, ActualHeight - 24));
            }
        }
        drawing.Pop();
    }
}
