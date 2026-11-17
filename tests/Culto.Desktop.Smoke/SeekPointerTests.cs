using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Culto.Desktop;
using Culto.Desktop.Dialogs;

internal static partial class Program
{
    private static void RunSeekPointerTests(MainWindow window)
    {
        void Verify(Slider slider, Func<bool> scrubbing, Action settle)
        {
            slider.Focus();
            slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Handled = true
            });
            try {
                Check(slider.IsMouseCaptured && scrubbing(), "Slider did not capture a track press");
                slider.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, slider, window) {
                    RoutedEvent = Keyboard.LostKeyboardFocusEvent
                });
                Check(scrubbing(), "Focus change released an active pointer drag");
                var behavior = typeof(MainWindow).Assembly.GetType("Culto.Desktop.Services.SeekSliderDrag");
                Check(behavior is not null, "Captured track clicks have no pointer movement behavior");
                var move = behavior!.GetMethod("MoveCapturedPointer", BindingFlags.Static | BindingFlags.NonPublic)!;
                move.Invoke(null, [slider, new Point(slider.ActualWidth * .8, -100)]);
                Check(slider.IsMouseCaptured && scrubbing() && slider.Value > .7, "Leaving vertically stopped horizontal seeking");
                var selected = slider.Value;
                settle();
                Check(Math.Abs(slider.Value - selected) < .001, "Playback tick overwrote the held position");
                move.Invoke(null, [slider, new Point(-100, 100)]);
                Check(slider.Value == slider.Minimum && slider.IsMouseCaptured, "Dragging outside the left end lost capture or did not clamp");
                move.Invoke(null, [slider, new Point(slider.ActualWidth + 100, -100)]);
                Check(slider.Value == slider.Maximum && slider.IsMouseCaptured, "Dragging outside the right end lost capture or did not clamp");
            }
            finally {
                slider.Value = .4;
                slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Handled = true
                });
            }
            Check(!slider.IsMouseCaptured && !scrubbing(), "Release left a stuck drag");
            var thumb = ((Track)slider.Template.FindName("PART_Track", slider)).Thumb;
            var center = thumb.TranslatePoint(new Point(thumb.ActualWidth / 2, thumb.ActualHeight / 2), slider);
            var hit = slider.InputHitTest(new Point(center.X + 9, center.Y)) as DependencyObject;
            var hitThumb = false;
            while (hit is not null) { if (ReferenceEquals(hit, thumb)) { hitThumb = true; break; } hit = VisualTreeHelper.GetParent(hit); }
            Check(hitThumb, "The thumb still has a narrow hit area");
        }
        Test("Inline seek keeps capture outside the track and releases only on mouse up", () => {
            Verify((Slider)window.FindName("PositionSlider"), () => Field<bool>(window, "_outputScrubbing"),
                () => Call(window, "TimerTick", null, EventArgs.Empty));
        });
        Test("Floating seek has the same robust pointer capture and larger thumb hit area", () => {
            var player = new OutputPlayerWindow();
            var state = new OutputPlayerState("Prueba", true, true, true, false, 2, 20, 0, false);
            try {
                player.Show(); player.Update(state); player.UpdateLayout();
                Verify(Field<Slider>(player, "_position"), () => Field<bool>(player, "_scrubbing"), () => player.Update(state));
            }
            finally { player.Close(); window.Activate(); }
        });
    }
}
