using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunWindowCapturePickerTests(MainWindow owner)
    {
        bool? Edit(WindowCaptureDialog dialog, Action<WindowCaptureDialog> action)
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
        void Press(WindowCaptureDialog dialog, string name) => Descendants(dialog).OfType<Button>()
            .Single(button => button.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Test("Capture catalog excludes this console and its own dialogs", () =>
        {
            var windows = WindowCaptureCatalog.List();
            long consoleHandle = new WindowInteropHelper(owner).Handle.ToInt64();
            Check(windows.All(target => target.Options.ProcessId != Environment.ProcessId && target.Options.WindowHandle != consoleHandle),
                "Capture catalog exposed its own console and could create a recursive projection");
            Check(windows.All(target => target.Options.WindowHandle != 0 && target.Options.ProcessId > 0
                && !string.IsNullOrWhiteSpace(target.Options.WindowTitle) && !string.IsNullOrWhiteSpace(target.Options.Executable)),
                "Capture catalog contains a target without a stable identity");
        });

        Test("Opening and cancelling capture picker never selects or starts an arbitrary window", () =>
        {
            var dialog = new WindowCaptureDialog();
            var result = Edit(dialog, editor =>
            {
                Check(Field<ListBox>(editor, "_windows").SelectedItem is null, "Picker selected a window without an operator choice");
                Check(Field<object?>(editor, "_client") is null, "Picker started a native capture before selection");
                Check(!Field<Button>(editor, "_add").IsEnabled, "Picker can save without receiving an initial frame");
                Press(editor, "AddWindowCapture");
                Check(editor.SelectedCue is null && editor.IsVisible, "No-selection Add created a capture");
                Press(editor, "CancelWindowCapture");
            });
            Check(result == false && dialog.SelectedCue is null, "Cancel returned a capture cue");
        });

        Test("Capture crop validation preserves a visible window area and audio intent", () =>
        {
            var dialog = new WindowCaptureDialog();
            Edit(dialog, editor =>
            {
                var fake = new WindowCaptureTarget(new WindowCaptureOptions
                {
                    WindowHandle = 1, ProcessId = int.MaxValue, WindowTitle = "Synthetic missing window " + Guid.NewGuid(),
                    WindowClass = "SyntheticWindow", Executable = "CultoCaptureSynthetic.exe"
                }, false, 1280, 720);
                // Supply a synthetic row without firing selection/native capture. This test
                // validates the editor draft only and never targets any user application.
                SetField(editor, "_refreshing", true);
                var list = Field<ListBox>(editor, "_windows"); list.ItemsSource = new[] { fake }; list.SelectedItem = fake;
                SetField(editor, "_refreshing", false);
                var canvas = Field<WindowCaptureCanvas>(editor, "_captureCanvas");
                canvas.SetSource(1280, 720, fake.Options);
                bool Read(out WindowCaptureOptions? options)
                {
                    object?[] arguments = [null];
                    bool valid = (bool)Call(editor, "ReadOptions", arguments)!;
                    options = arguments[0] as WindowCaptureOptions;
                    return valid;
                }
                canvas.SetDraft(fake.Options with { CropLeft = -1 });
                Check(!Read(out _), "Negative crop was accepted");
                canvas.SetDraft(fake.Options with { CropLeft = 1280 });
                Check(!Read(out _), "A crop removing the entire window was accepted");
                canvas.SetDraft(fake.Options with { CropLeft = 40, CropTop = 120, CropRight = 20, CropBottom = 10,
                    Transform = new(.1, .2, .5, .4) });
                Field<CheckBox>(editor, "_audio").IsChecked = false;
                Field<CheckBox>(editor, "_cursor").IsChecked = true;
                Check(Read(out var options) && options is { CropLeft: 40, CropTop: 120, CropRight: 20, CropBottom: 10, ControlAudio: false, CaptureCursor: true },
                    "Valid crop or explicit audio/cursor intent was lost");
                Check(options!.Transform == new WindowCaptureTransform(.1, .2, .5, .4), "Visual transform was not included in the capture draft");
                Check(Field<object?>(editor, "_client") is null, "Draft validation unexpectedly started capture");
                Press(editor, "CancelWindowCapture");
            });
        });

        Test("Capture canvas shows live crop handles, fills/reset and resizes without launching another worker", () =>
        {
            var dialog = new WindowCaptureDialog();
            Edit(dialog, editor =>
            {
                var canvas = Field<WindowCaptureCanvas>(editor, "_captureCanvas");
                var pixels = new byte[640 * 360 * 4];
                for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 255; pixels[i + 3] = 255; }
                canvas.SetSource(1920, 1080, new WindowCaptureOptions());
                canvas.Frame = System.Windows.Media.Imaging.BitmapSource.Create(640, 360, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null, pixels, 640 * 4);
                canvas.SetDraft(CaptureGeometry.Drag(canvas.Options, 1920, 1080, CaptureHandle.Left, 300, 0, crop: true));
                canvas.ApplyShortcut(System.Windows.Input.Key.F);
                Check(canvas.Options.CropLeft == 300 && canvas.Options.Transform is not null, "Fit lost crop or transform");
                canvas.ApplyShortcut(System.Windows.Input.Key.S);
                Check(canvas.Options.Transform == new WindowCaptureTransform(0, 0, 1, 1), "Stretch did not fill output");
                canvas.ApplyShortcut(System.Windows.Input.Key.R);
                Check(canvas.Options.CropLeft == 0 && canvas.Options.Transform is null, "Reset did not restore original source");
                editor.Width = 760; editor.Height = 540; editor.UpdateLayout();
                var root = (System.Windows.Media.Visual)editor.Content;
                var bounds = new Rect(((FrameworkElement)editor.Content).RenderSize);
                CheckWithin(canvas, root, bounds, "Capture canvas in compact dialog");
                Check(Field<object?>(editor, "_client") is null && canvas.Frame is not null, "Transforming restarted or cleared the capture");
                Check(!Descendants(editor).OfType<TextBox>().Any(box => box.Name.StartsWith("CaptureCrop")), "Pixel entry fields remain in the main editor");
                Press(editor, "CancelWindowCapture");
            });
        });

        Test("Unavailable saved capture never silently rebinds to another browser window", () =>
        {
            var missing = new WindowCaptureOptions
            {
                WindowHandle = 1, ProcessId = int.MaxValue, WindowTitle = "Nonexistent capture " + Guid.NewGuid(),
                WindowClass = "Chrome_WidgetWin_1", Executable = "chrome.exe"
            };
            bool rejected = false;
            try { WindowCaptureCatalog.Resolve(missing); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "An unavailable capture was substituted by an unrelated browser window");
        });
    }
}
