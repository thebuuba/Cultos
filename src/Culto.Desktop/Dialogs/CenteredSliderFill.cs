using System.Windows;
using System.Windows.Media;

namespace Culto.Desktop.Dialogs;

// Only paints the center-to-value portion; Slider's native Track still owns input.
public sealed class CenteredSliderFill : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(CenteredSliderFill), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(CenteredSliderFill), new FrameworkPropertyMetadata(-2d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(CenteredSliderFill), new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(CenteredSliderFill), new FrameworkPropertyMetadata(Brushes.YellowGreen, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 20 || Maximum <= Minimum) return;
        var width = ActualWidth - 20;
        var center = 10 + width * Math.Clamp(-Minimum / (Maximum - Minimum), 0, 1);
        var thumb = 10 + width * Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);
        dc.DrawRoundedRectangle(Fill, null, new Rect(Math.Min(center, thumb), (ActualHeight - 8) / 2, Math.Abs(thumb - center), 8), 4, 4);
    }
}
