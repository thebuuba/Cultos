using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Culto.Core;

namespace Culto.Desktop.Dialogs;

internal sealed class TransitionTimeline : FrameworkElement
{
    private TransitionOptions _options = new();
    public void Update(TransitionOptions options) { _options = options.Normalize(); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var visual = _options.Kind == "Cut" ? 0 : _options.DurationMilliseconds;
        var total = Math.Max(100, Math.Max(visual, Math.Max(_options.AudioFadeOutMilliseconds, _options.AudioFadeInMilliseconds)));
        var left = 102d; var width = Math.Max(1, ActualWidth - left - 4);
        void Label(string text, double x, double y, Brush brush, double size = 11) => dc.DrawText(new FormattedText(text,
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        var rows = new[] { ("Visual", visual, TransitionEditorUi.Lime), ("Audio sale", _options.AudioFadeOutMilliseconds, TransitionEditorUi.Violet), ("Audio entra", _options.AudioFadeInMilliseconds, TransitionEditorUi.Cyan) };
        for (var i = 0; i < rows.Length; i++)
        {
            var (label, duration, color) = rows[i]; var y = 8 + i * 36d;
            Label(label, 0, y + 6, TransitionEditorUi.Muted, 12);
            dc.DrawRoundedRectangle(TransitionEditorUi.Brush("#2D2D2D"), null, new Rect(left, y, width, 28), 14, 14);
            var length = width * duration / total;
            if (length > 0)
            {
                var c = ((SolidColorBrush)color).Color;
                Brush fill = i == 0 ? color : new LinearGradientBrush(i == 1 ? c : Color.FromArgb(80, c.R, c.G, c.B), i == 1 ? Color.FromArgb(80, c.R, c.G, c.B) : c, 0);
                dc.DrawRoundedRectangle(fill, null, new Rect(left + 3, y + 4, Math.Max(1, length - 6), 20), 10, 10);
            }
            Label($"{duration / 1000d:0.00}s", left + Math.Max(6, length - 48), y + 7, duration > 150 ? Brushes.Black : TransitionEditorUi.Muted, 10);
        }
        var step = total <= 2000 ? 500 : 1000;
        for (var tick = 0; tick <= total; tick += step) Label($"{tick / 1000d:0.0}s", left + width * tick / total - 9, 120, TransitionEditorUi.Muted, 10);
        if (total % step > step * .35) Label($"{total / 1000d:0.00}s", left + width - 30, 120, TransitionEditorUi.Muted, 10);
    }
}
