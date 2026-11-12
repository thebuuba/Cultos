using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private const int BrowserCapturePaintStyle = 0x20; // WS_EX_TRANSPARENT, without WS_EX_LAYERED.
    private nint _captureCompatibilityWindow;
    private bool _captureHadPaintStyle, _captureCompatibilityWarning;

    private static bool IsChromiumCapture(Cue? cue) => cue?.Kind == CueKind.WindowCapture
        && cue.Capture is { } source && (source.WindowClass?.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal) == true
            || string.Equals(Path.GetFileName(source.Executable), "chrome.exe", StringComparison.OrdinalIgnoreCase));

    private void UpdateBrowserCaptureCompatibility()
    {
        if (!_ready) return;
        // Live changes before the compositor: keep outgoing Chrome rendering
        // throughout preparation and its final dissolve, including retained audio.
        var needed = !_closing && (IsChromiumCapture(_preview?.DisplayedCue)
            || _output is not null && (IsChromiumCapture(_session.Live) || IsChromiumCapture(_output.DisplayedCue)
                || IsChromiumCapture(_output.RetainedMediaCue) || _output.IsTransitioning && _captureCompatibilityWindow != 0));
        if (!needed && _captureCompatibilityWindow == 0) return;
        var handle = needed ? new WindowInteropHelper(this).Handle : _captureCompatibilityWindow;
        if (handle == 0) return;
        var style = BrowserGetWindowStyle(handle, -20);
        // Layered windows would become click-through. The ordinary WPF console
        // is not layered; do not combine this workaround with per-pixel windows.
        if (needed && (style & 0x80000) != 0) return;
        if (needed && _captureCompatibilityWindow == 0)
        {
            _captureHadPaintStyle = (style & BrowserCapturePaintStyle) != 0;
            _captureCompatibilityWindow = handle;
        }
        // Chromium excludes WS_EX_TRANSPARENT from its occlusion calculation.
        // Without WS_EX_LAYERED this is a paint-order hint, not click-through
        // or visual transparency (Microsoft DWM best practices / window features).
        // It preserves the normal frame, taskbar, input and opaque HDMI pixels.
        var next = needed || _captureHadPaintStyle ? style | BrowserCapturePaintStyle : style & ~BrowserCapturePaintStyle;
        if (next != style)
        {
            Marshal.SetLastPInvokeError(0);
            if (BrowserSetWindowStyle(handle, -20, next) == 0 && Marshal.GetLastPInvokeError() != 0)
            {
                if (!_captureCompatibilityWarning && !_closing)
                    SetStatus("No se pudo activar la compatibilidad de captura del navegador: " + new Win32Exception().Message, true);
                _captureCompatibilityWarning = true;
                return;
            }
            // Our native window's state really changed. Publish that change so
            // Chromium recalculates even if it cached occlusion before capture.
            BrowserNotifyWinEvent(0x800A, handle, 0, 0); // EVENT_OBJECT_STATECHANGE, OBJID_WINDOW.
        }
        if (!needed) { _captureCompatibilityWindow = 0; _captureCompatibilityWarning = false; }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int BrowserGetWindowStyle(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int BrowserSetWindowStyle(nint window, int index, int value);
    [DllImport("user32.dll", EntryPoint = "NotifyWinEvent")]
    private static extern void BrowserNotifyWinEvent(uint eventId, nint window, int objectId, int childId);
}
