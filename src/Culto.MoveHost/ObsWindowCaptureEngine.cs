using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Culto.Core;

namespace Culto.MoveHost;

internal sealed class ObsWindowCaptureEngine : IDisposable
{
    private readonly ObsRuntime _runtime;
    private readonly ObsNative.RenderCallback _render;
    private readonly int _width, _height;
    private readonly object _gate = new();
    private WindowCaptureOptions? _source;
    private nint _capture;
    private Exception? _failure;
    private ulong _firstRenderedAt;
    private long _retryCaptureAt;
    private bool _attached, _disposed;

    public ObsWindowCaptureEngine(string runtimePath, int width, int height)
    {
        _width = width; _height = height;
        _runtime = new ObsRuntime(runtimePath, width, height, captureOnly: true);
        _render = Render;
    }

    public async Task RunAsync(ObsCaptureCommand command, ObsMoveProtocol protocol, CancellationToken token)
    {
        _source = command.Source;
        ValidateWindow(_source, checkExecutable: true);
        ObsNative.obs_add_main_render_callback(_render, 0);
        _attached = true;
        using var frames = new ObsFramePump(0, _width, _height, activateOutput: true);
        var startup = Stopwatch.StartNew();
        while (true)
        {
            // WGC creates its frame pool on OBS' graphics thread, which owns the
            // WinRT dispatcher. Raw BGRA output alone also activates that renderer.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var frame = await frames.ReadAsync(timeout.Token).ConfigureAwait(false);
            ulong first;
            lock (_gate)
            {
                if (_failure is not null) throw new InvalidOperationException(_failure.Message, _failure);
                first = _firstRenderedAt;
            }
            if (first == 0)
            {
                if (startup.Elapsed.TotalSeconds >= 12) throw new TimeoutException("La ventana no entregó imagen. Comprueba que no esté minimizada ni protegida.");
                continue;
            }
            startup.Restart();
            // Do not expose OBS startup/queued blank frames as captured content.
            if (frame.Timestamp <= first) continue;
            await protocol.WriteAsync(ObsMoveMessageType.Frame, frame.Buffer.AsMemory(0, frame.Length), token).ConfigureAwait(false);
        }
    }

    private void Render(nint parameter, uint width, uint height)
    {
        lock (_gate)
        {
            if (_disposed || _failure is not null || _source is null) return;
            try
            {
                ValidateWindow(_source, checkExecutable: false);
                if (!IsWindowVisible((nint)_source.WindowHandle) || IsIconic((nint)_source.WindowHandle))
                {
                    _firstRenderedAt = 0;
                    return;
                }
                if (_capture == 0)
                {
                    if (Environment.TickCount64 < _retryCaptureAt) return;
                    if (!ObsWinrtNative.winrt_capture_supported())
                        throw new PlatformNotSupportedException("Esta versión de Windows no admite captura de ventanas con video.");
                    _capture = ObsWinrtNative.winrt_capture_init_window(_source.CaptureCursor,
                        (nint)_source.WindowHandle, clientArea: true, forceSdr: true);
                    if (_capture == 0)
                    {
                        _retryCaptureAt = Environment.TickCount64 + 1000;
                        return;
                    }
                }
                if (!ObsWinrtNative.winrt_capture_active(_capture))
                {
                    ObsWinrtNative.winrt_capture_free(_capture); _capture = 0;
                    _firstRenderedAt = 0;
                    _retryCaptureAt = Environment.TickCount64 + 1000;
                    return;
                }
                var sourceWidth = (int)ObsWinrtNative.winrt_capture_width(_capture);
                var sourceHeight = (int)ObsWinrtNative.winrt_capture_height(_capture);
                if (sourceWidth == 0 || sourceHeight == 0) { _firstRenderedAt = 0; return; }
                var croppedWidth = sourceWidth - _source.CropLeft - _source.CropRight;
                var croppedHeight = sourceHeight - _source.CropTop - _source.CropBottom;
                if (croppedWidth <= 0 || croppedHeight <= 0)
                    throw new InvalidOperationException("El recorte ocupa toda la ventana. Reduce los márgenes de captura.");
                var box = CaptureGeometry.Bounds(_source, sourceWidth, sourceHeight);
                var left = (float)(box.X * _width / CaptureGeometry.CanvasWidth);
                var top = (float)(box.Y * _height / CaptureGeometry.CanvasHeight);
                var targetWidth = (float)(box.Width * _width / CaptureGeometry.CanvasWidth);
                var targetHeight = (float)(box.Height * _height / CaptureGeometry.CanvasHeight);
                var x1 = Math.Clamp((int)Math.Round(left), 0, _width);
                var y1 = Math.Clamp((int)Math.Round(top), 0, _height);
                var x2 = Math.Clamp((int)Math.Round(left + targetWidth), 0, _width);
                var y2 = Math.Clamp((int)Math.Round(top + targetHeight), 0, _height);
                if (_firstRenderedAt == 0) _firstRenderedAt = ObsNative.obs_get_video_frame_time();
                if (x2 <= x1 || y2 <= y1) return;
                var clip = new ObsNative.GraphicsRect { X = x1, Y = y1, Width = x2 - x1, Height = y2 - y1 };
                ObsNative.gs_matrix_push();
                try
                {
                    ObsNative.gs_set_scissor_rect(ref clip);
                    ObsNative.gs_matrix_translate3f(left, top, 0);
                    ObsNative.gs_matrix_scale3f(targetWidth / croppedWidth, targetHeight / croppedHeight, 1);
                    ObsNative.gs_matrix_translate3f(-_source.CropLeft, -_source.CropTop, 0);
                    ObsWinrtNative.winrt_capture_render(_capture);
                    if (_firstRenderedAt == 0) _firstRenderedAt = ObsNative.obs_get_video_frame_time();
                }
                finally { ObsNative.gs_set_scissor_rect(0); ObsNative.gs_matrix_pop(); }
            }
            catch (Exception error) { _failure = error; } // Never unwind into native OBS.
        }
    }

    private static void ValidateWindow(WindowCaptureOptions source, bool checkExecutable)
    {
        source.ValidateTarget();
        var window = (nint)source.WindowHandle;
        GetWindowThreadProcessId(window, out var processId);
        if (!IsWindow(window) || processId != source.ProcessId)
            throw new InvalidOperationException("La ventana seleccionada se cerró. Elige nuevamente la ventana de YouTube.");
        var windowClass = new StringBuilder(1024);
        GetClassName(window, windowClass, windowClass.Capacity);
        if (!string.Equals(windowClass.ToString(), source.WindowClass, StringComparison.Ordinal))
            throw new InvalidOperationException("La ventana seleccionada cambió. Vuelve a seleccionarla.");
        if (checkExecutable)
        {
            using var process = Process.GetProcessById(source.ProcessId);
            if (source.ProcessStartTimeUtcTicks > 0 && process.StartTime.ToUniversalTime().Ticks != source.ProcessStartTimeUtcTicks)
                throw new InvalidOperationException("La aplicación seleccionada se reinició. Elige nuevamente su ventana.");
            var actual = process.MainModule?.FileName ?? "";
            var expected = source.Executable;
            if (!string.Equals(Path.GetFileName(actual), Path.GetFileName(expected), StringComparison.OrdinalIgnoreCase)
                || Path.IsPathFullyQualified(expected) && !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La ventana ya no pertenece a la aplicación seleccionada.");
        }
    }

    public void Dispose()
    {
        if (_attached) { _attached = false; ObsNative.obs_remove_main_render_callback(_render, 0); }
        nint capture;
        lock (_gate) { if (_disposed) return; _disposed = true; capture = _capture; _capture = 0; }
        if (capture != 0) ObsWinrtNative.winrt_capture_free(capture);
        _runtime.Dispose();
        GC.KeepAlive(_render);
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder buffer, int count);
}
