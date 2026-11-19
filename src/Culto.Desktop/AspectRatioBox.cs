using System.Windows;
using System.Windows.Controls;

namespace Culto.Desktop;

// Arrange the real native video host; a Viewbox transform cannot resize an HWND correctly.
public sealed class AspectRatioBox : Decorator
{
    public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register(
        nameof(AspectRatio), typeof(double), typeof(AspectRatioBox),
        new FrameworkPropertyMetadata(16d / 9, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is double ratio && double.IsFinite(ratio) && ratio > 0);

    public double AspectRatio { get => (double)GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    private Size Fit(Size size)
    {
        var width = double.IsFinite(size.Width) ? Math.Max(0, size.Width) : 480;
        var height = double.IsFinite(size.Height) ? Math.Max(0, size.Height) : width / AspectRatio;
        width = Math.Min(width, height * AspectRatio);
        return new Size(width, width / AspectRatio);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var fitted = Fit(constraint);
        Child?.Measure(fitted);
        return fitted;
    }

    protected override Size ArrangeOverride(Size size)
    {
        var fitted = Fit(size);
        Child?.Arrange(new Rect((size.Width - fitted.Width) / 2, (size.Height - fitted.Height) / 2, fitted.Width, fitted.Height));
        return size;
    }
}
