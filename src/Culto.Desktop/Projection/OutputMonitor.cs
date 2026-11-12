using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Culto.Desktop.Projection;

// The compositor mirrors the actual output HWND, including video and dissolves.
// There is no second decoder or audio player to drift from the congregation output.
public sealed class OutputMonitor : FrameworkElement, IDisposable
{
    private Window? _source;
    private OutputMonitor? _mirrorSource;
    private IntPtr _nativeSource;
    private bool _embedNative;
    private IntPtr _embeddedParent;
    private int _originalStyle;
    private NativeRect _embeddedBounds;
    private VisualBrush? _localImage;
    private IntPtr _thumbnail, _destination, _registeredSource;
    private long _retryAfter;
    private ThumbnailProperties _last;
    public bool IsConnected => _thumbnail != IntPtr.Zero || _embeddedParent != IntPtr.Zero;
    public IntPtr NativeSource => _nativeSource;

    public OutputMonitor()
    {
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => { DetachNative(); Release(); };
        LayoutUpdated += (_, _) => Refresh();
        IsVisibleChanged += (_, _) => Refresh();
    }

    public void SetSource(Window? source)
    {
        if (ReferenceEquals(_source, source)) return;
        Release(); _source = source; Refresh(); InvalidateVisual();
    }

    public void SetNativeSource(IntPtr source, bool embedded = false)
    {
        if (_nativeSource == source && _embedNative == embedded) { Refresh(); return; }
        DetachNative(); Release(); _nativeSource = source; _embedNative = embedded;
        Refresh(); InvalidateVisual();
    }

    public void SetLocalSource(ProjectionSurface? surface)
    {
        if (ReferenceEquals(_localImage?.Visual, surface)) return;
        _localImage = surface is null ? null : new VisualBrush(surface) { Stretch = Stretch.Uniform };
        InvalidateVisual();
    }

    public void SetMirrorSource(OutputMonitor? source)
    {
        if (ReferenceEquals(source, this)) throw new ArgumentException("A monitor cannot mirror itself.", nameof(source));
        if (ReferenceEquals(_mirrorSource, source)) return;
        Release(); _retryAfter = 0; _mirrorSource = source; Refresh(); InvalidateVisual();
    }

    public void Refresh()
    {
        // Docked content retains its logical owner when floated. Use the visual HWND.
        var destinationSource = PresentationSource.FromVisual(this) as HwndSource;
        if (!IsLoaded || !IsVisible || destinationSource is null
            || (_mirrorSource is not null && (!_mirrorSource.IsVisible || !_mirrorSource.IsConnected))
            || (_mirrorSource is null && _nativeSource == IntPtr.Zero && (_source is null || !_source.IsVisible)))
        { if (_embeddedParent != IntPtr.Zero) ShowWindow(_nativeSource, 0); Release(); return; }
        // AvalonDock uses an intermediate HwndSource with a Border root for floating content.
        var handle = GetAncestor(destinationSource.Handle, 2);
        var mirrorRoot = _mirrorSource is null ? null : PresentationSource.FromVisual(_mirrorSource) as HwndSource;
        var sourceHandle = _mirrorSource is not null
            ? mirrorRoot is null ? IntPtr.Zero : GetAncestor(mirrorRoot.Handle, 2)
            : _nativeSource != IntPtr.Zero ? _nativeSource : new WindowInteropHelper(_source!).Handle;
        if (handle == IntPtr.Zero || sourceHandle == IntPtr.Zero || (handle == sourceHandle && _mirrorSource is null)) { Release(); return; }
        NativeRect sourceRegion = default;
        if (_mirrorSource is not null)
        {
            // DWM only accepts top-level HWNDs. Crop the compositor of the
            // embedded show's parent, never the whole desktop or another show.
            sourceRegion = ClientBounds(_mirrorSource, sourceHandle);
            var targetRegion = ClientBounds(this, sourceHandle);
            if (sourceRegion.Right <= sourceRegion.Left || sourceRegion.Bottom <= sourceRegion.Top
                || (handle == sourceHandle && sourceRegion.Left < targetRegion.Right && sourceRegion.Right > targetRegion.Left
                    && sourceRegion.Top < targetRegion.Bottom && sourceRegion.Bottom > targetRegion.Top))
            { Release(); return; } // Never feed the preview back into its own source.
        }
        if (handle != _destination || sourceHandle != _registeredSource) Release();
        if (!_embedNative && _thumbnail == IntPtr.Zero)
        {
            if (Environment.TickCount64 < _retryAfter) return;
            if (DwmRegisterThumbnail(handle, sourceHandle, out _thumbnail) != 0)
            { _thumbnail = IntPtr.Zero; _retryAfter = Environment.TickCount64 + 1000; return; }
            _destination = handle;
            _registeredSource = sourceHandle;
        }
        var origin = PointToScreen(new Point());
        var corner = PointToScreen(new Point(ActualWidth, ActualHeight));
        var client = new NativePoint();
        ClientToScreen(handle, ref client);
        var rect = new NativeRect { Left = (int)Math.Round(origin.X - client.X), Top = (int)Math.Round(origin.Y - client.Y),
            Right = (int)Math.Round(corner.X - client.X), Bottom = (int)Math.Round(corner.Y - client.Y) };
        if (_embedNative)
        {
            // PowerPoint suspends its render surface when completely offscreen.
            // A native child occupies the local monitor; HDMI keeps the same show
            // as a top-level window and uses the DWM mirror below.
            if (_embeddedParent != handle)
            {
                DetachNative();
                _originalStyle = GetWindowLong(_nativeSource, -16);
                SetWindowLong(_nativeSource, -16, (_originalStyle & ~unchecked((int)0x80000000)) | 0x46000000);
                Marshal.SetLastPInvokeError(0);
                var previousParent = SetParent(_nativeSource, handle);
                if (previousParent == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0)
                {
                    SetWindowLong(_nativeSource, -16, _originalStyle);
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "No se pudo integrar la ventana de PowerPoint.");
                }
                _embeddedParent = handle; _embeddedBounds = default;
            }
            GetWindowRect(_nativeSource, out var nativeBounds);
            nativeBounds.Left -= client.X; nativeBounds.Right -= client.X;
            nativeBounds.Top -= client.Y; nativeBounds.Bottom -= client.Y;
            if (!_embeddedBounds.Equals(rect) || !nativeBounds.Equals(rect))
            {
                SetWindowPos(_nativeSource, IntPtr.Zero, rect.Left, rect.Top, Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top), 0x0010 | 0x0004 | 0x0020);
                ShowScrollBar(_nativeSource, 3, false);
                _embeddedBounds = rect;
            }
            var show = !IsIconic(handle) && ActualWidth > 1 && ActualHeight > 1;
            if (IsWindowVisible(_nativeSource) != show) ShowWindow(_nativeSource, show ? 8 : 0);
            NativePresentationWindow.FitContent(_nativeSource);
            return;
        }
        if (_nativeSource != IntPtr.Zero) NativePresentationWindow.FitContent(_nativeSource);
        // Preserve the source aspect ratio even when the output is a resizable rehearsal window.
        if (_mirrorSource is not null || DwmQueryThumbnailSourceSize(_thumbnail, out var size) == 0 && size.Width > 0 && size.Height > 0)
        {
            GetClientRect(sourceHandle, out var sourceRect);
            var sw = _mirrorSource is null ? sourceRect.Right : sourceRegion.Right - sourceRegion.Left;
            var sh = _mirrorSource is null ? sourceRect.Bottom : sourceRegion.Bottom - sourceRegion.Top;
            if (sw > 0 && sh > 0)
            {
                var scale = Math.Min((rect.Right - rect.Left) / (double)sw, (rect.Bottom - rect.Top) / (double)sh);
                var w = (int)(sw * scale); var h = (int)(sh * scale);
                rect.Left += (rect.Right - rect.Left - w) / 2; rect.Top += (rect.Bottom - rect.Top - h) / 2;
                rect.Right = rect.Left + w; rect.Bottom = rect.Top + h;
            }
        }
        var properties = new ThumbnailProperties { Flags = (uint)(1 | 4 | 8 | 16 | (_mirrorSource is not null ? 2 : 0)),
            Destination = rect, Source = sourceRegion, Opacity = 255,
            Visible = !IsIconic(handle) && ActualWidth > 1 && ActualHeight > 1, SourceClientAreaOnly = true };
        if (!properties.Equals(_last))
        {
            if (DwmUpdateThumbnailProperties(_thumbnail, ref properties) != 0)
            { _retryAfter = Environment.TickCount64 + 1000; Release(); return; }
            _last = properties;
        }
    }

    private static NativeRect ClientBounds(FrameworkElement element, IntPtr window)
    {
        var origin = element.PointToScreen(new Point());
        var corner = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        var client = new NativePoint(); ClientToScreen(window, ref client);
        return new NativeRect { Left = (int)Math.Round(origin.X - client.X), Top = (int)Math.Round(origin.Y - client.Y),
            Right = (int)Math.Round(corner.X - client.X), Bottom = (int)Math.Round(corner.Y - client.Y) };
    }

    protected override void OnRender(DrawingContext drawing)
    {
        drawing.DrawRectangle(Brushes.Black, null, new Rect(RenderSize));
        if (_mirrorSource is not null)
        {
            if (_thumbnail == IntPtr.Zero) DrawStatus(drawing, "PowerPoint · ver En pantalla");
            return;
        }
        if (_source is not null || _nativeSource != IntPtr.Zero) return;
        if (_localImage is not null)
        {
            drawing.DrawRectangle(_localImage, null, new Rect(RenderSize));
            return;
        }
        DrawStatus(drawing, "Sin salida");
    }

    private void DrawStatus(DrawingContext drawing, string message)
    {
        var text = new FormattedText(message, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14, Brushes.Gray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawing.DrawText(text, new Point(Math.Max(0, (ActualWidth - text.Width) / 2), Math.Max(0, (ActualHeight - text.Height) / 2)));
    }

    private void Release()
    {
        var hadThumbnail = _thumbnail != IntPtr.Zero;
        if (hadThumbnail) DwmUnregisterThumbnail(_thumbnail);
        _thumbnail = _destination = _registeredSource = IntPtr.Zero; _last = default;
        if (hadThumbnail) InvalidateVisual();
    }
    private void DetachNative()
    {
        if (_embeddedParent == IntPtr.Zero) return;
        ShowWindow(_nativeSource, 0);
        SetParent(_nativeSource, IntPtr.Zero);
        SetWindowLong(_nativeSource, -16, _originalStyle & ~0x10000000);
        _embeddedParent = IntPtr.Zero; _embeddedBounds = default;
    }
    public void Dispose() { DetachNative(); _source = null; _mirrorSource = null; _nativeSource = IntPtr.Zero; _localImage = null; Release(); }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] private struct ThumbnailProperties
    {
        public uint Flags; public NativeRect Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail, out NativeSize size);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool ShowScrollBar(IntPtr window, int bar, bool show);
}
