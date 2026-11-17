using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Culto.Desktop.Services;

internal static class SeekSliderDrag
{
    internal static void Attach(Slider slider)
    {
        slider.AddHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler((_, e) =>
        {
            // A press on the track captures the Slider instead of its Thumb.
            // Keep following it outside the track until the button is released.
            if (!slider.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
            MoveCapturedPointer(slider, e.GetPosition(slider));
            e.Handled = true;
        }), true);
    }

    internal static void MoveCapturedPointer(Slider slider, Point point)
    {
        if (!slider.IsMouseCaptured || slider.Template?.FindName("PART_Track", slider) is not Track track) return;
        var local = slider.TranslatePoint(point, track);
        // Use the track's distance mapping from an endpoint. ValueFromPoint
        // depends on the previous thumb layout and jumps on rapid mouse moves.
        var value = (track.IsDirectionReversed ? slider.Maximum : slider.Minimum)
            + track.ValueFromDistance(local.X - track.Thumb.ActualWidth / 2, 0);
        if (double.IsFinite(value)) slider.SetCurrentValue(Slider.ValueProperty, Math.Clamp(value, slider.Minimum, slider.Maximum));
    }
}
