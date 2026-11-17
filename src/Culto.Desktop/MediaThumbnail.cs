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
    public static readonly DependencyProperty ShowDurationProperty = DependencyProperty.Register(nameof(ShowDuration), typeof(bool), typeof(MediaThumbnail),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(MediaThumbnail),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsRender), value => value is double radius && double.IsFinite(radius) && radius >= 0);
    private static readonly Typeface IconFace = new("Segoe MDL2 Assets");
    private static readonly Typeface LabelFace = new("Segoe UI");
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(36, 178, 126));
    private static readonly Geometry BibleCover = (Geometry)Geometry.Parse("M 4,1 H 20 V 23 H 5 C 2.8,23 1,21.2 1,19 V 4 C 1,2.3 2.3,1 4,1 Z").GetAsFrozen();
    private static readonly Geometry BibleDetails = (Geometry)Geometry.Parse("M 8,8 H 16 M 12,4 V 14 M 4,19 H 18").GetAsFrozen();
    private static readonly Brush BibleBlue = new SolidColorBrush(Color.FromRgb(146, 192, 255));
    private static readonly Pen BibleDetailPen = new(Brushes.Black, 1.5);
    private BitmapSource? _image;
    private TimeSpan? _duration;
    private CancellationTokenSource? _request;
    private int _generation;
    public Cue? Cue { get => (Cue?)GetValue(CueProperty); set => SetValue(CueProperty, value); }
    public bool ShowDuration { get => (bool)GetValue(ShowDurationProperty); set => SetValue(ShowDurationProperty, value); }
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

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
        thumbnail.Cancel(); thumbnail._image = null; thumbnail._duration = null;
        thumbnail.Load();
    }

    private void Cancel()
    {
        _generation++;
        _request?.Cancel(); _request?.Dispose(); _request = null;
    }

    private async void Load()
    {
        if (!IsLoaded || !IsVisible || _image is not null || _request is not null || Cue is not { } cue) return;
        if (cue.YouTubeUrl is null && (cue.Kind is not (CueKind.Video or CueKind.Image) || cue.FilePath is null)) return;
        var request = _request = new CancellationTokenSource();
        var generation = ++_generation;
        try
        {
            if (cue.YouTubeUrl is { } address)
            {
                var image = await YouTubeVideoMetadata.ReadThumbnailAsync(address, request.Token);
                if (generation == _generation && IsLoaded && IsVisible) { _image = image; InvalidateVisual(); }
                return;
            }
            var details = await MediaThumbnailService.GetDetailsAsync(cue.FilePath!, cue.Kind, request.Token);
            if (generation != _generation || !IsLoaded || !IsVisible) return;
            _image = details?.Image; _duration = details?.Duration; InvalidateVisual();
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
        var radius = ShowDuration ? Math.Min(3, CornerRadius) : CornerRadius;
        drawing.PushClip(new RectangleGeometry(bounds, radius, radius));
        drawing.DrawRectangle(Brushes.Black, null, bounds);
        if (_image is { PixelWidth: > 0, PixelHeight: > 0 } image)
        {
            var scale = Math.Min(ActualWidth / image.PixelWidth, ActualHeight / image.PixelHeight);
            var width = image.PixelWidth * scale; var height = image.PixelHeight * scale;
            drawing.DrawImage(image, new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height));
        }
        else if (Cue?.Kind == CueKind.Bible)
        {
            var scale = Math.Min(1.2, Math.Min(ActualWidth, ActualHeight) / 34);
            drawing.PushTransform(new TranslateTransform((ActualWidth - 21 * scale) / 2, (ActualHeight - 24 * scale) / 2));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.DrawGeometry(BibleBlue, null, BibleCover);
            drawing.DrawGeometry(null, BibleDetailPen, BibleDetails);
            drawing.Pop();
            drawing.Pop();
        }
        else
        {
            var icon = Cue?.Kind switch { CueKind.Video => "\uE714", CueKind.Image => "\uEB9F", CueKind.Audio => "\uE8D6", _ => "\uE8A5" };
            var size = Math.Min(30, Math.Max(12, ActualHeight * .30));
            var text = new FormattedText(icon, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                IconFace, size, Accent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawing.DrawText(text, new Point((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2));
        }
        if (ShowDuration && _duration is { } duration)
        {
            var label = duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"mm\:ss");
            var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                LabelFace, 9, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var y = Math.Max(0, ActualHeight - text.Height - 1);
            drawing.DrawRectangle(new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), null,
                new Rect(Math.Max(0, ActualWidth - text.Width - 4), y, Math.Min(ActualWidth, text.Width + 4), text.Height + 1));
            drawing.DrawText(text, new Point(Math.Max(0, ActualWidth - text.Width - 2), y));
        }
        drawing.Pop();
    }
}
