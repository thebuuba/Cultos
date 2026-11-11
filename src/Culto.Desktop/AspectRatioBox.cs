using System.Windows;
using System.Windows.Controls;

namespace Culto.Desktop;

// Arrange the real native video host; a Viewbox transform cannot resize an HWND correctly.
public sealed class AspectRatioBox : Decorator
{
    private static Size Fit(Size size)
    {
        var width = double.IsFinite(size.Width) ? Math.Max(0, size.Width) : 480;
        var height = double.IsFinite(size.Height) ? Math.Max(0, size.Height) : width * 9 / 16;
        width = Math.Min(width, height * 16 / 9);
        return new Size(width, width * 9 / 16);
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
