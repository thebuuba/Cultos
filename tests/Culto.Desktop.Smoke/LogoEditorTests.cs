using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;

internal static partial class Program
{
    private static void RunLogoEditorTests(MainWindow owner, string firstPath, string secondPath)
    {
        bool? Edit(LogoSettingsDialog dialog, Action<LogoSettingsDialog> action)
        {
            Exception? failure = null;
            dialog.Owner = owner;
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try { action(dialog); }
                catch (Exception error) { failure = error; }
                finally { if (dialog.IsVisible) dialog.DialogResult = false; }
            }));
            var result = dialog.ShowDialog();
            if (failure is not null) throw failure;
            return result;
        }
        void Press(LogoSettingsDialog dialog, string buttonName) => Descendants(dialog).OfType<Button>()
            .Single(button => button.Name == buttonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Test("Logo editor cancellation preserves input list, starting path and timing options", () =>
        {
            var images = new List<string> { firstPath, secondPath };
            var options = new LogoPlaybackOptions { IntervalSeconds = 12, TransitionMilliseconds = 450 };
            var editor = new LogoSettingsDialog(images, firstPath, options);
            var result = Edit(editor, dialog =>
            {
                Field<ListBox>(dialog, "_gallery").SelectedIndex = 1;
                Press(dialog, "MoveLogoImageUp");
                Press(dialog, "RemoveLogoImage");
                Field<TextBox>(dialog, "_interval").Text = "25";
                Field<TextBox>(dialog, "_transition").Text = "900";
                Press(dialog, "CancelLogoSettings");
            });
            Check(result == false, "Cancel did not reject the editor draft");
            Check(images.SequenceEqual(new[] { firstPath, secondPath }) && options.IntervalSeconds == 12 && options.TransitionMilliseconds == 450,
                "Editing a cancelled draft mutated caller state");
        });

        Test("Logo editor snapshots remain independent and saving retains order and selected starting image", () =>
        {
            var images = new List<string> { firstPath, secondPath };
            var options = new LogoPlaybackOptions();
            var editor = new LogoSettingsDialog(images, secondPath, options);
            images.Clear(); options.IntervalSeconds = 120;
            var exposedImages = editor.Images; exposedImages.Clear();
            var exposedOptions = editor.Options; exposedOptions.TransitionMilliseconds = 2000;
            Check(editor.Images.Count == 2 && editor.Options.IntervalSeconds == 10 && editor.Options.TransitionMilliseconds == 300,
                "Editor shares a mutable list or options record");
            var result = Edit(editor, dialog =>
            {
                Check(dialog.SelectedPath == secondPath, "Saved starting image was not selected");
                Press(dialog, "MoveLogoImageUp");
                Check(dialog.SelectedPath == secondPath && dialog.IsVisible, "Reordering changed the starting image or closed the editor");
                Field<TextBox>(dialog, "_interval").Text = "2.5";
                Field<TextBox>(dialog, "_transition").Text = "175";
                Press(dialog, "SaveLogoSettings");
            });
            Check(result == true && editor.Images.SequenceEqual(new[] { secondPath, firstPath }) && editor.SelectedPath == secondPath,
                "Saving lost image order or starting selection");
            Check(editor.Options.IntervalSeconds == 2.5 && editor.Options.TransitionMilliseconds == 175,
                "Saving lost independent dwell and transition durations");
        });

        Test("Logo editor rejects invalid timing and permits an empty image list", () =>
        {
            var editor = new LogoSettingsDialog([firstPath], firstPath, new LogoPlaybackOptions());
            var result = Edit(editor, dialog =>
            {
                Field<TextBox>(dialog, "_interval").Text = "NaN";
                Press(dialog, "SaveLogoSettings");
                Check(dialog.IsVisible && Field<TextBlock>(dialog, "_status").Visibility == Visibility.Visible,
                    "Nonfinite dwell was accepted");
                Field<TextBox>(dialog, "_interval").Text = "3600";
                Field<TextBox>(dialog, "_transition").Text = "99";
                Press(dialog, "SaveLogoSettings");
                Check(dialog.IsVisible, "Transition below its minimum was accepted");
                Field<TextBox>(dialog, "_transition").Text = "3000";
                Press(dialog, "RemoveLogoImage");
                Check(dialog.SelectedPath is null && dialog.Images.Count == 0, "Removing the final image retained a stale selection");
                Press(dialog, "SaveLogoSettings");
            });
            Check(result == true && editor.Options.IntervalSeconds == 3600 && editor.Options.TransitionMilliseconds == 3000,
                "Valid boundary durations or empty playlist were rejected");
        });
    }
}
