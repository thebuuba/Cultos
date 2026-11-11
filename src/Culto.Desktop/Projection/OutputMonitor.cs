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
    private IntPtr _thumbnail, _destination;
    private ThumbnailProperties _last;
    public bool IsConnected => _thumbnail != IntPtr.Zero;

    public OutputMonitor()
    {
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => Release();
        LayoutUpdated += (_, _) => Refresh();
        IsVisibleChanged += (_, _) => Refresh();
    }

    public void SetSource(Window? source)
    {
        if (ReferenceEquals(_source, source)) return;
        Release(); _source = source; Refresh(); InvalidateVisual();
    }

    public void Refresh()
    {
        // Docked content retains its logical owner when floated. Use the visual HWND.
        var destinationSource = PresentationSource.FromVisual(this) as HwndSource;
        if (!IsLoaded || !IsVisible || destinationSource is null || _source is null || !_source.IsVisible)
        { Release(); return; }
        // AvalonDock uses an intermediate HwndSource with a Border root for floating content.
        var handle = GetAncestor(destinationSource.Handle, 2);
        var sourceHandle = new WindowInteropHelper(_source).Handle;
        if (handle == IntPtr.Zero || sourceHandle == IntPtr.Zero || handle == sourceHandle) { Release(); return; }
        if (handle != _destination) Release();
        if (_thumbnail == IntPtr.Zero)
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
        if (_source is not null) return;
        var text = new FormattedText("Sin salida", System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14, Brushes.Gray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawing.DrawText(text, new Point(Math.Max(0, (ActualWidth - text.Width) / 2), Math.Max(0, (ActualHeight - text.Height) / 2)));
    }

    private void Release()
    {
        if (_thumbnail != IntPtr.Zero) DwmUnregisterThumbnail(_thumbnail);
        _thumbnail = _destination = IntPtr.Zero; _last = default;
    }
    public void Dispose() { _source = null; Release(); }

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
}
