using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Culto.Desktop.Dialogs;

// Progress over time is independent of Move's spatial Curve parameter.
internal sealed class MoveEasingGraph : FrameworkElement
{
    public Brush Accent { get; init; } = Brushes.YellowGreen;
    public int Mode { get; set; } = 3;
    public int Function { get; set; } = 2;
    public MoveEasingGraph() => AutomationProperties.SetName(this, "Gráfica de progreso de la transición a lo largo del tiempo");

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), null, bounds, 10, 10);
        if (ActualWidth < 24 || ActualHeight < 36) return;
        var plot = new Rect(12, 18, ActualWidth - 24, ActualHeight - 36);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(55, 55, 55)), 1);
        for (var i = 0; i <= 4; i++)
        {
            var fraction = i / 4d;
            dc.DrawLine(grid, new Point(plot.Left + plot.Width * fraction, plot.Top), new Point(plot.Left + plot.Width * fraction, plot.Bottom));
            dc.DrawLine(grid, new Point(plot.Left, plot.Top + plot.Height * fraction), new Point(plot.Right, plot.Top + plot.Height * fraction));
        }
        var diagonal = new Pen(new SolidColorBrush(Color.FromRgb(102, 102, 102)), 1) { DashStyle = DashStyles.Dash };
        dc.DrawLine(diagonal, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Top));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(plot.Left, plot.Bottom), false, false);
            for (var i = 1; i <= 120; i++)
            {
                var t = i / 120d;
                context.LineTo(new Point(plot.Left + plot.Width * t, plot.Bottom - plot.Height * Ease(t)), true, false);
            }
        }
        geometry.Freeze(); dc.PushClip(new RectangleGeometry(bounds)); dc.DrawGeometry(null, new Pen(Accent, 2.5), geometry);
        dc.DrawEllipse(Accent, null, new Point(plot.Left, plot.Bottom), 3, 3); dc.DrawEllipse(Accent, null, new Point(plot.Right, plot.Top), 3, 3); dc.Pop();
    }
    private double Ease(double t) => Mode switch
    {
        0 => t, 1 => EaseIn(t), 2 => 1 - EaseIn(1 - t),
        _ => t < .5 ? EaseIn(t * 2) / 2 : 1 - EaseIn(2 - t * 2) / 2
    };
    private double EaseIn(double t) => Function switch
    {
        1 => t * t, 2 => t * t * t, 3 => Math.Pow(t, 4), 4 => Math.Pow(t, 5),
        5 => 1 - Math.Cos(t * Math.PI / 2), 6 => 1 - Math.Sqrt(Math.Max(0, 1 - t * t)),
        7 => t == 0 ? 0 : Math.Pow(2, 10 * t - 10),
        8 => t is 0 or 1 ? t : -Math.Pow(2, 10 * t - 10) * Math.Sin((t * 10 - 10.75) * (2 * Math.PI / 3)),
        9 => 1 - BounceOut(1 - t), 10 => 2.70158 * t * t * t - 1.70158 * t * t, _ => t
    };
    private static double BounceOut(double t)
    {
        if (t < 1 / 2.75) return 7.5625 * t * t;
        if (t < 2 / 2.75) { t -= 1.5 / 2.75; return 7.5625 * t * t + .75; }
        if (t < 2.5 / 2.75) { t -= 2.25 / 2.75; return 7.5625 * t * t + .9375; }
        t -= 2.625 / 2.75; return 7.5625 * t * t + .984375;
    }
}
