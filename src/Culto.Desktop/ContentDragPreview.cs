using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Culto.Core;

namespace Culto.Desktop;

// One lightweight card follows the OLE drag. It shares the thumbnail cache;
// it never creates a media player or captures input from the drop target.
internal sealed class ContentDragPreview : IDisposable
{
    private readonly Popup _popup;
    private readonly DispatcherTimer _timer;
    private nint _handle;

    public ContentDragPreview(FrameworkElement source, Cue cue, bool moving)
    {
        var accent = (Brush)source.FindResource("AccentBrush");
        var contents = new Grid();
        contents.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        contents.ColumnDefinitions.Add(new ColumnDefinition());
        contents.Children.Add(new MediaThumbnail { Cue = cue, Width = 80, Height = 48, VerticalAlignment = VerticalAlignment.Center });
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) };
        Grid.SetColumn(labels, 1);
        labels.Children.Add(new TextBlock
        {
            Text = moving ? "Mover" : "+ Añadir", Foreground = (Brush)source.FindResource("AccentTextBrush"), FontSize = 10, FontWeight = FontWeights.SemiBold
        });
        labels.Children.Add(new TextBlock
        {
            Text = cue.Title, Foreground = (Brush)source.FindResource("TextBrush"), FontSize = 12, FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 3)
        });
        labels.Children.Add(new TextBlock
        {
            Text = cue.Kind switch
            {
                CueKind.Bible => cue.Reference ?? "Biblia",
                CueKind.Video => cue.HymnNumber is { } number ? $"Himno {number:000} · Video" : "Video",
                CueKind.Image => "Imagen", CueKind.Audio => "Audio", CueKind.WindowCapture => "YouTube · Captura",
                _ => cue.HymnNumber is { } hymn ? $"Himno {hymn:000}" : "Texto"
            },
            Foreground = (Brush)source.FindResource("MutedBrush"), FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        contents.Children.Add(labels);
        _popup = new Popup
        {
            PlacementTarget = source, Placement = PlacementMode.AbsolutePoint, AllowsTransparency = true,
            StaysOpen = true, Focusable = false, IsHitTestVisible = false,
            Child = new Border
            {
                Width = 282, Padding = new Thickness(10), CornerRadius = new CornerRadius(12),
                Background = (Brush)source.FindResource("PanelBrush"),
                BorderBrush = accent, BorderThickness = new Thickness(1), Child = contents,
                IsHitTestVisible = false, SnapsToDevicePixels = true
            }
        };
        _timer = new DispatcherTimer(DispatcherPriority.Input, source.Dispatcher) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += FollowCursor;
        _popup.Opened += Opened;
        _popup.IsOpen = true;
        _timer.Start();
    }

    private void Opened(object? sender, EventArgs e)
    {
        if (_popup.Child is not Visual visual || PresentationSource.FromVisual(visual) is not HwndSource source) return;
        _handle = source.Handle;
        // WS_EX_TRANSPARENT | WS_EX_NOACTIVATE: even OLE hit testing passes
        // through the floating card, including across undocked panels.
        SetWindowLongPtr(_handle, -20, GetWindowLongPtr(_handle, -20) | 0x08000020);
        FollowCursor(null, EventArgs.Empty);
    }

    private void FollowCursor(object? sender, EventArgs e)
    {
        if (_handle != 0 && GetCursorPos(out var position))
            // Native screen coordinates keep the cursor offset correct on
            // secondary monitors, including negative origins and mixed DPI.
            SetWindowPos(_handle, 0, position.X + 18, position.Y + 20, 0, 0, 0x0015);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= FollowCursor;
        _popup.Opened -= Opened;
        _popup.IsOpen = false;
        _popup.Child = null;
        _handle = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
