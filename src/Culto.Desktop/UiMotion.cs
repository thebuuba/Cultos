using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Culto.Desktop;

internal static class UiMotion
{
    // Final values are committed immediately. Motion never owns application state.
    public static void Fade(UIElement element, double opacity)
    {
        var from = element.Opacity;
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = opacity;
        if (SystemParameters.ClientAreaAnimation)
            element.BeginAnimation(UIElement.OpacityProperty, Transition(from, opacity, 130), HandoffBehavior.SnapshotAndReplace);
    }

    public static void Reveal(FrameworkElement content)
    {
        content.BeginAnimation(UIElement.OpacityProperty, null);
        content.Opacity = 1;
        var movement = content.RenderTransform as TranslateTransform ?? new TranslateTransform();
        content.RenderTransform = movement;
        movement.BeginAnimation(TranslateTransform.YProperty, null);
        movement.Y = 0;
        if (!SystemParameters.ClientAreaAnimation) return;
        content.BeginAnimation(UIElement.OpacityProperty, Transition(.72, 1, 180), HandoffBehavior.SnapshotAndReplace);
        movement.BeginAnimation(TranslateTransform.YProperty, Transition(5, 0, 180), HandoffBehavior.SnapshotAndReplace);
    }

    private static DoubleAnimation Transition(double from, double to, int milliseconds) => new(from, to, TimeSpan.FromMilliseconds(milliseconds))
    {
        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };
}
