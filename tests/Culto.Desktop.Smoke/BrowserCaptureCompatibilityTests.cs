using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunBrowserCaptureCompatibilityTests(MainWindow window)
    {
        var session = Field<PresentationSession>(window, "_session");
        var handle = new WindowInteropHelper(window).Handle;
        var originalStyle = BrowserGetWindowLong(handle, -20);
        BrowserDwmGetWindowAttribute(handle, 1, out var originalFrame, sizeof(int));
        var originalState = window.WindowState;
        var original = session.Live;
        Check((bool)Call(window, "EnsureOutput")!, "Local output is available for compatibility checks");
        // Chromium ignores non-Chromium popup windows, complex regions and non-opaque layers.
        // Inspect only the owned test console, never a user's browser.
        var originalWindowStyle = BrowserGetWindowLong(handle, -16);
        bool IsOpaqueCover() => (BrowserGetWindowLong(handle, -16) & 0x80000000L) == 0
            && (BrowserGetWindowLong(handle, -20) & 0x20) == 0
            && ((BrowserGetWindowLong(handle, -20) & 0x80000) == 0
                || BrowserGetLayeredWindowAttributes(handle, out _, out var alpha, out var flags) && (flags & 1) == 0 && ((flags & 2) == 0 || alpha == 255))
            && BrowserGetWindowRgnBox(handle, out _) != 3;
        try
        {
            Test("A maximized console cannot occlude its live Chromium capture", () =>
            {
                session.RestoreOutput(new Cue { Kind = CueKind.WindowCapture, Capture = new WindowCaptureOptions
                    { Executable = "chrome.exe", WindowClass = "Chrome_WidgetWin_1" } }, false, false);
                Call(window, "UpdateSessionLabels");
                window.WindowState = WindowState.Maximized; PumpFor(100);
                Check(!IsOpaqueCover(), "Maximized console is an opaque cover that suspends the captured Chromium video");
                window.WindowState = WindowState.Normal; PumpFor(100);
                Check(!IsOpaqueCover(), "Restoring the console reintroduced browser capture occlusion");
                Check(window.IsEnabled && window.IsHitTestVisible && window.Opacity == 1,
                    "Capture compatibility disabled input or altered projected visual opacity");
                Check((BrowserGetWindowLong(handle, -20) & ~0x20) == (originalStyle & ~0x20), "Capture compatibility changed unrelated native console styles");
                Check((BrowserGetWindowLong(handle, -16) & 0x00CF0000) == (originalWindowStyle & 0x00CF0000), "Capture compatibility removed caption or resize/min/max controls");
                var point = window.PointToScreen(new Point(window.ActualWidth / 2, window.ActualHeight / 2));
                Check(BrowserGetAncestor(BrowserWindowFromPoint(new BrowserTestPoint { X = (int)point.X, Y = (int)point.Y }), 2) == handle,
                    "Capture compatibility passes mouse hits to another window");
                BrowserDwmGetWindowAttribute(handle, 1, out var currentFrame, sizeof(int));
                Check(currentFrame == originalFrame, "Capture compatibility disabled the themed DWM window frame");
            });
            Test("Leaving browser capture restores the console style without affecting ordinary content", () =>
            {
                session.RestoreOutput(new Cue { Kind = CueKind.Text, Body = "Texto" }, false, false);
                Call(window, "UpdateSessionLabels");
                Check(BrowserGetWindowLong(handle, -20) == originalStyle && IsOpaqueCover(), "Leaving browser capture did not restore original native styles");
                Check(BrowserGetWindowLong(handle, -16) == originalWindowStyle, "Leaving browser capture did not restore its window type");
                session.RestoreOutput(new Cue { Kind = CueKind.WindowCapture, Capture = new WindowCaptureOptions
                    { Executable = "synthetic.exe", WindowClass = "OwnedTestSource" } }, false, false);
                Call(window, "UpdateSessionLabels");
                Check(BrowserGetWindowLong(handle, -20) == originalStyle, "Ordinary window capture changed console opacity");
            });
            Test("Capture compatibility survives a live transition and releases after its last outgoing frame", () =>
            {
                var output = Field<Culto.Desktop.Projection.ProjectionSurface>(window, "_output");
                output.ShowCue(new Cue { Kind = CueKind.Text, Body = "Antes" });
                session.RestoreOutput(new Cue { Kind = CueKind.WindowCapture, Capture = new WindowCaptureOptions
                    { Executable = "chrome.exe", WindowClass = "Chrome_WidgetWin_1" } }, false, false);
                Check(!IsOpaqueCover(), "Browser capture did not enable compatibility before the transition");
                output.Transitions = new TransitionOptions { Kind = "Dissolve", DurationMilliseconds = 600,
                    AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
                var next = new Cue { Kind = CueKind.Text, Body = "DespuÃ©s" };
                output.TransitionTo(next);
                session.RestoreOutput(next, false, false);
                Check(output.IsTransitioning && !IsOpaqueCover(), "New session removed capture compatibility during the outgoing dissolve");
                Until(() => !output.IsTransitioning, "Test transition did not finish");
                Call(window, "UpdateSessionLabels");
                Check(IsOpaqueCover(), "Compatibility remained after the last outgoing frame");
            });
        }
        finally
        {
            session.RestoreOutput(original, false, false); Call(window, "UpdateSessionLabels");
            window.WindowState = originalState;
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint BrowserGetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "GetLayeredWindowAttributes")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BrowserGetLayeredWindowAttributes(nint window, out uint color, out byte alpha, out uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct BrowserTestBounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowRgnBox")]
    private static extern int BrowserGetWindowRgnBox(nint window, out BrowserTestBounds bounds);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int BrowserDwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [StructLayout(LayoutKind.Sequential)]
    private struct BrowserTestPoint { public int X, Y; }
    [DllImport("user32.dll", EntryPoint = "WindowFromPoint")]
    private static extern nint BrowserWindowFromPoint(BrowserTestPoint point);
    [DllImport("user32.dll", EntryPoint = "GetAncestor")]
    private static extern nint BrowserGetAncestor(nint window, uint flags);
}
