using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private const int BrowserCapturePaintStyle = 0x20; // WS_EX_TRANSPARENT, without WS_EX_LAYERED.
    private nint _captureCompatibilityWindow;
    private bool _captureHadPaintStyle, _captureCompatibilityWarning;
    private readonly DispatcherTimer _browserCaptureResizeTimer=new(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(250)};
    private (nint Window, BrowserCaptureBounds Bounds)? _captureForeground;

    private void BrowserCaptureActivated()
    {
        BrowserCaptureLayoutChanged();
        if (_closing || _captureCompatibilityWindow == 0 || !IsActive || Topmost) return;
        // An opaque application left between the console and Chrome still makes
        // Chromium suspend rendering. Keep the selected browser behind us without
        // activating, resizing, restoring or making it a topmost window.
        foreach (var cue in new[] { _preview?.DisplayedCue, _output?.DisplayedCue, _output?.RetainedMediaCue })
        {
            if (!IsChromiumCapture(cue) || cue?.Capture is not { } source
                || !WindowCaptureCatalog.WindowStillExists(source)) continue;
            var browser = (nint)source.WindowHandle;
            if (BrowserIsIconic(browser) || (BrowserGetWindowStyle(browser, -20) & 8) != 0) continue;
            BrowserSetWindowPos(browser, _captureCompatibilityWindow, 0, 0, 0, 0, 0x0213);
        }
    }

    private void BrowserCaptureLayoutChanged()
    {
        UpdateBrowserCaptureCompatibility();
        if(_closing || _captureCompatibilityWindow==0)return;
        _browserCaptureResizeTimer.Stop();_browserCaptureResizeTimer.Start();
    }

    private void RefreshBrowserCaptureAfterResize(object? sender,EventArgs e)
    {
        _browserCaptureResizeTimer.Stop();
        if(_closing || _captureCompatibilityWindow==0)return;
        _output?.RefreshWindowCapture();_preview?.RefreshWindowCapture();
        RefreshThumbnails(YouTubeList);

        static void RefreshThumbnails(DependencyObject parent)
        {
            if (parent is WindowCaptureThumbnail thumbnail) { thumbnail.RefreshWindowCapture(); return; }
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
                RefreshThumbnails(VisualTreeHelper.GetChild(parent, index));
        }
    }

    private static bool IsChromiumCapture(Cue? cue) => cue?.Kind == CueKind.WindowCapture
        && cue.Capture is { } source && (source.WindowClass?.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal) == true
            || string.Equals(Path.GetFileName(source.Executable), "chrome.exe", StringComparison.OrdinalIgnoreCase));

    private void UpdateBrowserCaptureCompatibility()
    {
        if (!_ready) return;
        // Live changes before the compositor: keep outgoing Chrome rendering
        // throughout preparation and its final dissolve, including retained audio.
        var needed = !_closing && (YouTubePanel.IsVisible && YouTubeList.Items.OfType<YouTubeSource>().Any(source => source.IsAvailable)
            || IsChromiumCapture(_preview?.DisplayedCue)
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
        if (!needed)
        {
            _browserCaptureResizeTimer.Stop();
            _captureCompatibilityWindow = 0; _captureCompatibilityWarning = false;
            _captureForeground = null;
        }
        else
        {
            // Also detect entering/leaving fullscreen after focus was already lost.
            var foreground = BrowserGetForegroundWindow();
            if (foreground != 0 && BrowserGetWindowRect(foreground, out var bounds))
            {
                var current = (foreground, bounds);
                if (!_captureForeground.Equals(current))
                {
                    _captureForeground = current;
                    _browserCaptureResizeTimer.Stop(); _browserCaptureResizeTimer.Start();
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BrowserCaptureBounds
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern nint BrowserGetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BrowserGetWindowRect(nint window, out BrowserCaptureBounds bounds);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int BrowserGetWindowStyle(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int BrowserSetWindowStyle(nint window, int index, int value);
    [DllImport("user32.dll", EntryPoint = "NotifyWinEvent")]
    private static extern void BrowserNotifyWinEvent(uint eventId, nint window, int objectId, int childId);
    [DllImport("user32.dll", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BrowserIsIconic(nint window);
    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BrowserSetWindowPos(nint window, nint after,
        int x, int y, int width, int height, uint flags);
}
