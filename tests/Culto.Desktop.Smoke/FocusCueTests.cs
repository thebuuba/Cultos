using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Desktop;
using Culto.Desktop.Dialogs;

internal static partial class Program
{
    private static void RunFocusCueTests(MainWindow window)
    {
        static void MouseInput(UIElement control) => control.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseDownEvent, Handled = true });
        static void KeyboardInput(UIElement control) => control.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(control), 0, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Handled = true });
        void Verify(Button button)
        {
            button.ApplyTemplate();
            MouseInput(button);
            Check(button.Focus(), $"{button.Name} could not receive mouse focus (visible={button.IsVisible}, enabled={button.IsEnabled})");
            Check(button.IsKeyboardFocused, "Mouse action discarded focus and shortcuts");
            Check(!KeyboardFocusCue.GetIsVisible(button), "Mouse action retained the keyboard cue");
            var cue = (FrameworkElement)button.Template.FindName("ButtonFocus", button);
            Check(cue.Opacity == 0, "Mouse action retained the pale green circle");
            KeyboardInput(button);
            Check(KeyboardFocusCue.GetIsVisible(button) && cue.Opacity == 1, "Keyboard navigation has no visible focus cue");
            MouseInput(button);
            Check(!KeyboardFocusCue.GetIsVisible(button) && cue.Opacity == 0, "Clicking the keyboard-focused button kept its highlight");
        }

        Test("Console fullscreen and ordinary buttons distinguish clicks from keyboard focus", () =>
        {
            window.Activate();
            foreach (var name in new[] { "PreviewFullscreenButton", "ProgramFullscreenButton" })
                Verify((Button)window.FindName(name));
        });
        var player = new OutputPlayerWindow();
        try
        {
            player.Show(); player.Activate(); player.UpdateLayout();
            player.Update(new OutputPlayerState("Focus test", true, true, false, false, 0, 10, 50, false));
            Test("Enlarged player mute and fullscreen do not retain a clicked highlight", () =>
            {
                Verify((Button)player.FindName("PlayerMute"));
                Verify((Button)player.FindName("PlayerFullscreen"));
            });
            Test("Keyboard focus moves between controls and clears the previous cue", () =>
            {
                var first = (Button)player.FindName("PlayerMute");
                var second = (Button)player.FindName("PlayerFullscreen");
                first.Focus(); KeyboardInput(first); second.Focus();
                Check(!KeyboardFocusCue.GetIsVisible(first) && KeyboardFocusCue.GetIsVisible(second), "Previous focus cue remained visible");
                Keyboard.ClearFocus();
                Check(!KeyboardFocusCue.GetIsVisible(second), "Lost keyboard focus kept its cue");
            });
        }
        finally { player.Close(); }
    }
}
