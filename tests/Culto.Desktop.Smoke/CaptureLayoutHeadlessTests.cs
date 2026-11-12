using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

internal static partial class Program
{
    // Creates disconnected editor controls only. No window is shown and no
    // native enumeration, browser, capture worker or dispatcher loop runs.
    private static int RunCaptureLayoutHeadlessTests()
    {
        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var target = new WindowCaptureTarget(new WindowCaptureOptions { WindowHandle = 90, ProcessId = 1234,
            ProcessStartTimeUtcTicks = 500, WindowTitle = "Video nuevo - YouTube - Google Chrome",
            WindowClass = "Chrome_WidgetWin_1", Executable = "chrome.exe" }, false, 1920, 1080);
        void Select(WindowCaptureDialog editor, WindowCaptureTarget selected)
        {
            SetField(editor, "_refreshing", true);
            var list = Field<ListBox>(editor, "_windows"); list.ItemsSource = new[] { selected }; list.SelectedItem = selected;
            Call(editor, "ConfigureSelection");
        }
        void Close(WindowCaptureDialog editor)
        {
            Field<DispatcherTimer>(editor, "_debounce").Stop(); Call(editor, "StopPreview"); editor.Close();
        }
        Test("Reopening a crop editor preserves its layout while rebinding a restarted window", () =>
        {
            var options = target.Options with { WindowHandle = 1, ProcessStartTimeUtcTicks = 100,
                CropLeft = 320, CropTop = 140, CropRight = 20, CropBottom = 10, Transform = new(.1, .1, .8, .8),
                ControlAudio = false, CaptureCursor = true };
            var editor = new WindowCaptureDialog(new Cue { Kind = CueKind.WindowCapture, Title = "YouTube", Capture = options });
            try
            {
                Select(editor, target);
                var draft = Field<WindowCaptureCanvas>(editor, "_captureCanvas").Options;
                Check(draft.WindowHandle == target.Options.WindowHandle && draft.ProcessStartTimeUtcTicks == 500,
                    "Reopened draft kept its old HWND, so refreshing would reset the crop");
                Check(WindowCaptureLayout.From(draft) == WindowCaptureLayout.From(options), "Reopening lost crop/position");
                object?[] arguments = [null];
                Check((bool)Call(editor, "ReadOptions", arguments)! && arguments[0] is WindowCaptureOptions saved
                    && saved.WindowHandle == 90 && saved.CaptureCursor && !saved.ControlAudio
                    && WindowCaptureLayout.From(saved) == WindowCaptureLayout.From(options), "Editor lost saved crop or audio/cursor intent");
                Check(Field<object?>(editor, "_client") is null, "Disconnected editor unexpectedly started capture");
            }
            finally { Close(editor); }
        });
        Test("A fixed YouTube layout initializes each chosen YouTube window and leaves other apps alone", () =>
        {
            var layout = new WindowCaptureLayout(320, 140, 20, 10, new(0, .1, 1, .8));
            var editor = new WindowCaptureDialog(youTubeLayout: layout);
            try
            {
                Select(editor, target);
                Check(WindowCaptureLayout.From(Field<WindowCaptureCanvas>(editor, "_captureCanvas").Options) == layout, "New YouTube window opened without its layout");
                Select(editor, target with { Options = target.Options with { WindowHandle = 91, WindowTitle = "Otro video - YouTube" } });
                Check(WindowCaptureLayout.From(Field<WindowCaptureCanvas>(editor, "_captureCanvas").Options) == layout, "Changing video/window reset the shared layout");
                Select(editor, target with { Options = target.Options with { Executable = "slides.exe", WindowTitle = "Diapositivas", WindowClass = "OwnedSynthetic" } });
                Check(WindowCaptureLayout.From(Field<WindowCaptureCanvas>(editor, "_captureCanvas").Options) == new WindowCaptureLayout(), "YouTube layout cropped another application's window");
            }
            finally { Close(editor); }
        });
        app.Shutdown();
        Console.WriteLine($"Capture layout headless: {failures} failure(s)");
        return failures == 0 ? 0 : 1;
    }
}
