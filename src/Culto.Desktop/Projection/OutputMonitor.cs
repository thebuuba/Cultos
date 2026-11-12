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
    private IntPtr _nativeSource;
    private bool _embedNative;
    private IntPtr _embeddedParent;
    private int _originalStyle;
    private NativeRect _embeddedBounds;
    private VisualBrush? _localImage;
    private IntPtr _thumbnail, _destination;
    private ThumbnailProperties _last;
    public bool IsConnected => _thumbnail != IntPtr.Zero || _embeddedParent != IntPtr.Zero;

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
        _localImage = surface is null ? null : new VisualBrush(surface) { Stretch = Stretch.Uniform };
        InvalidateVisual();
    }

    public void Refresh()
    {
        // Docked content retains its logical owner when floated. Use the visual HWND.
        var destinationSource = PresentationSource.FromVisual(this) as HwndSource;
        if (!IsLoaded || !IsVisible || destinationSource is null || (_nativeSource == IntPtr.Zero && (_source is null || !_source.IsVisible)))
        { if (_embeddedParent != IntPtr.Zero) ShowWindow(_nativeSource, 0); Release(); return; }
        // AvalonDock uses an intermediate HwndSource with a Border root for floating content.
        var handle = GetAncestor(destinationSource.Handle, 2);
        var sourceHandle = _nativeSource != IntPtr.Zero ? _nativeSource : new WindowInteropHelper(_source!).Handle;
        if (handle == IntPtr.Zero || sourceHandle == IntPtr.Zero || handle == sourceHandle) { Release(); return; }
        if (handle != _destination) Release();
        if (!_embedNative && _thumbnail == IntPtr.Zero)
        {
            if (DwmRegisterThumbnail(handle, sourceHandle, out _thumbnail) != 0) { _thumbnail = IntPtr.Zero; return; }
            _destination = handle;
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
        if (DwmQueryThumbnailSourceSize(_thumbnail, out var size) == 0 && size.Width > 0 && size.Height > 0)
        {
            GetClientRect(sourceHandle, out var sourceRect);
            var sw = sourceRect.Right; var sh = sourceRect.Bottom;
            if (sw > 0 && sh > 0)
            {
                var scale = Math.Min((rect.Right - rect.Left) / (double)sw, (rect.Bottom - rect.Top) / (double)sh);
                var w = (int)(sw * scale); var h = (int)(sh * scale);
                rect.Left += (rect.Right - rect.Left - w) / 2; rect.Top += (rect.Bottom - rect.Top - h) / 2;
                rect.Right = rect.Left + w; rect.Bottom = rect.Top + h;
            }
        }
        var properties = new ThumbnailProperties { Flags = 1 | 4 | 8 | 16, Destination = rect, Opacity = 255,
            Visible = !IsIconic(handle) && ActualWidth > 1 && ActualHeight > 1, SourceClientAreaOnly = true };
        if (!properties.Equals(_last)) { DwmUpdateThumbnailProperties(_thumbnail, ref properties); _last = properties; }
    }

    protected override void OnRender(DrawingContext drawing)
    {
        drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 23, 34)), null, new Rect(RenderSize));
        if (_source is not null || _nativeSource != IntPtr.Zero) return;
        if (_localImage is not null)
        {
            drawing.DrawRectangle(_localImage, null, new Rect(RenderSize));
            return;
        }
        var text = new FormattedText("Sin salida", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14, Brushes.Gray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawing.DrawText(text, new Point(Math.Max(0, (ActualWidth - text.Width) / 2), Math.Max(0, (ActualHeight - text.Height) / 2)));
    }

    private void Release()
    {
        if (_thumbnail != IntPtr.Zero) DwmUnregisterThumbnail(_thumbnail);
        _thumbnail = _destination = IntPtr.Zero; _last = default;
    }
    private void DetachNative()
    {
        if (_embeddedParent == IntPtr.Zero) return;
        ShowWindow(_nativeSource, 0);
        SetParent(_nativeSource, IntPtr.Zero);
        SetWindowLong(_nativeSource, -16, _originalStyle & ~0x10000000);
        _embeddedParent = IntPtr.Zero; _embeddedBounds = default;
    }
    public void Dispose() { DetachNative(); _source = null; _nativeSource = IntPtr.Zero; _localImage = null; Release(); }

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
