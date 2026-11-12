using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;

namespace Culto.Desktop;

public sealed class WindowCaptureCanvas : FrameworkElement
{
    private WindowCaptureOptions _options = new();
    private WindowCaptureOptions? _dragOptions;
    private CaptureHandle _handle;
    private Point _dragStart;
    private int _sourceWidth, _sourceHeight;
    private BitmapSource? _frame;
    public event Action? Edited;
    public WindowCaptureOptions Options => _options with { };
    public bool HasSource => _sourceWidth > 0 && _sourceHeight > 0;
    public int SourceWidth => _sourceWidth;
    public int SourceHeight => _sourceHeight;
    public BitmapSource? Frame { get => _frame; set { _frame = value; InvalidateVisual(); } }

    public WindowCaptureCanvas()
    {
        Focusable = true; ClipToBounds = true; MinHeight = 120;
        AutomationProperties.SetName(this, "Transformación de captura");
        var menu = new ContextMenu();
        foreach (var entry in new[] { ("Ajustar a pantalla", "Ctrl+F", Key.F), ("Estirar a pantalla", "Ctrl+S", Key.S),
            ("Centrar", "Ctrl+D", Key.D), ("Restablecer", "Ctrl+R", Key.R) })
        {
            var item = new MenuItem { Header = entry.Item1, InputGestureText = entry.Item2 };
            var key = entry.Item3; item.Click += (_, _) => ApplyShortcut(key); menu.Items.Add(item);
        }
        ContextMenu = menu;
        LostMouseCapture += (_, _) => _dragOptions = null;
        IsKeyboardFocusedChanged += (_, _) => InvalidateVisual();
    }

    public void SetSource(int width, int height, WindowCaptureOptions options)
    {
        _sourceWidth = width; _sourceHeight = height;
        _options = options with { }; _dragOptions = null; Frame = null;
        InvalidateVisual();
    }
    public void UpdateSourceSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || width == _sourceWidth && height == _sourceHeight) return;
        _sourceWidth = width; _sourceHeight = height;
        // A resized browser must always leave at least one source pixel visible.
        _options = _options with { CropLeft = Math.Min(_options.CropLeft, width - 1), CropTop = Math.Min(_options.CropTop, height - 1) };
        _options = _options with { CropRight = Math.Min(_options.CropRight, width - _options.CropLeft - 1),
            CropBottom = Math.Min(_options.CropBottom, height - _options.CropTop - 1) };
        _dragOptions = null; InvalidateVisual();
    }
    public void SetDraft(WindowCaptureOptions options) { _options = options with { }; InvalidateVisual(); }
    private Rect CanvasBounds
    {
        get
        {
            var scale = Math.Max(.001, Math.Min(Math.Max(1, ActualWidth - 32) / CaptureGeometry.CanvasWidth,
                Math.Max(1, ActualHeight - 32) / CaptureGeometry.CanvasHeight));
            return new((ActualWidth - CaptureGeometry.CanvasWidth * scale) / 2,
                (ActualHeight - CaptureGeometry.CanvasHeight * scale) / 2, CaptureGeometry.CanvasWidth * scale, CaptureGeometry.CanvasHeight * scale);
        }
    }
    private Point ToCanvas(Point point)
    {
        var canvas = CanvasBounds; double scale = canvas.Width / CaptureGeometry.CanvasWidth;
        return new((point.X - canvas.X) / scale, (point.Y - canvas.Y) / scale);
    }
    private static Rect Rectangle(CaptureBox box) => new(box.X, box.Y, box.Width, box.Height);
    private static IEnumerable<(CaptureHandle Handle, Point Point)> Handles(CaptureBox box)
    {
        yield return (CaptureHandle.TopLeft, new(box.X, box.Y));
        yield return (CaptureHandle.Top, new(box.X + box.Width / 2, box.Y));
        yield return (CaptureHandle.TopRight, new(box.Right, box.Y));
        yield return (CaptureHandle.Right, new(box.Right, box.Y + box.Height / 2));
        yield return (CaptureHandle.BottomRight, new(box.Right, box.Bottom));
        yield return (CaptureHandle.Bottom, new(box.X + box.Width / 2, box.Bottom));
        yield return (CaptureHandle.BottomLeft, new(box.X, box.Bottom));
        yield return (CaptureHandle.Left, new(box.X, box.Y + box.Height / 2));
    }
    private CaptureHandle? Hit(Point point)
    {
        if (!HasSource || Frame is null) return null;
        var box = CaptureGeometry.Bounds(_options, _sourceWidth, _sourceHeight);
        var tolerance = 10 * CaptureGeometry.CanvasWidth / CanvasBounds.Width;
        foreach (var handle in Handles(box))
            if (Math.Abs(handle.Point.X - point.X) <= tolerance && Math.Abs(handle.Point.Y - point.Y) <= tolerance) return handle.Handle;
        return Rectangle(box).Contains(point) ? CaptureHandle.Move : null;
    }

    protected override void OnRender(DrawingContext drawing)
    {
        drawing.DrawRectangle((Brush)FindResource("CanvasBrush"), null, new Rect(RenderSize));
        var canvas = CanvasBounds;
        drawing.DrawRectangle(Brushes.Black, new Pen(IsKeyboardFocused ? (Brush)FindResource("AmberBrush") : (Brush)FindResource("BorderBrush"), 1), canvas);
        if (!HasSource || _frame is null) return;
        var scale = canvas.Width / CaptureGeometry.CanvasWidth;
        var box = CaptureGeometry.Bounds(_options, _sourceWidth, _sourceHeight);
        var matrix = new Matrix(scale, 0, 0, scale, canvas.X, canvas.Y);
        drawing.PushClip(new RectangleGeometry(canvas));
        drawing.PushTransform(new MatrixTransform(matrix));
        double frameScale = Math.Min((double)_frame.PixelWidth / _sourceWidth, (double)_frame.PixelHeight / _sourceHeight);
        var viewbox = new Rect(((_frame.PixelWidth - _sourceWidth * frameScale) / 2 + _options.CropLeft * frameScale) / _frame.PixelWidth,
            ((_frame.PixelHeight - _sourceHeight * frameScale) / 2 + _options.CropTop * frameScale) / _frame.PixelHeight,
            Math.Max(1, _sourceWidth - _options.CropLeft - _options.CropRight) * frameScale / _frame.PixelWidth,
            Math.Max(1, _sourceHeight - _options.CropTop - _options.CropBottom) * frameScale / _frame.PixelHeight);
        drawing.DrawRectangle(new ImageBrush(_frame) { Viewbox = viewbox, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Stretch = Stretch.Fill }, null, Rectangle(box));
        drawing.Pop(); drawing.Pop();
        drawing.PushTransform(new MatrixTransform(matrix));
        var color = _options.CropLeft + _options.CropRight + _options.CropTop + _options.CropBottom > 0
            ? new SolidColorBrush(Color.FromRgb(68, 229, 155)) : new SolidColorBrush(Color.FromRgb(255, 104, 115));
        var pen = new Pen(color, 1.5 / scale);
        drawing.DrawRectangle(null, pen, Rectangle(box));
        foreach (var handle in Handles(box))
            drawing.DrawRectangle(Brushes.White, pen, new Rect(handle.Point.X - 4 / scale, handle.Point.Y - 4 / scale, 8 / scale, 8 / scale));
        drawing.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e); Focus();
        var point = ToCanvas(e.GetPosition(this));
        if (Hit(point) is not { } handle) return;
        _dragOptions = Options; _dragStart = point; _handle = handle; CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = ToCanvas(e.GetPosition(this));
        if (_dragOptions is { } start && IsMouseCaptured)
        {
            var modifiers = Keyboard.Modifiers;
            _options = CaptureGeometry.Drag(start, _sourceWidth, _sourceHeight, _handle, point.X - _dragStart.X, point.Y - _dragStart.Y,
                crop: modifiers.HasFlag(ModifierKeys.Alt), stretch: modifiers.HasFlag(ModifierKeys.Shift),
                snap: !modifiers.HasFlag(ModifierKeys.Control), snapDistance: 6 * CaptureGeometry.CanvasWidth / CanvasBounds.Width);
            InvalidateVisual(); Edited?.Invoke(); e.Handled = true;
        }
        else Cursor = Hit(point) switch
        {
            CaptureHandle.Move => Cursors.SizeAll,
            CaptureHandle.TopLeft or CaptureHandle.BottomRight => Cursors.SizeNWSE,
            CaptureHandle.TopRight or CaptureHandle.BottomLeft => Cursors.SizeNESW,
            CaptureHandle.Left or CaptureHandle.Right => Cursors.SizeWE,
            CaptureHandle.Top or CaptureHandle.Bottom => Cursors.SizeNS,
            _ => Cursors.Arrow
        };
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragOptions is null) return;
        _dragOptions = null; if (IsMouseCaptured) ReleaseMouseCapture(); e.Handled = true;
    }
    public void ApplyShortcut(Key key)
    {
        if (!HasSource) return;
        _options = key switch
        {
            Key.F => CaptureGeometry.Fit(_options, _sourceWidth, _sourceHeight),
            Key.S => CaptureGeometry.Stretch(_options),
            Key.D => CaptureGeometry.Center(_options, _sourceWidth, _sourceHeight),
            Key.R => CaptureGeometry.Reset(_options), _ => _options
        };
        InvalidateVisual(); Edited?.Invoke();
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftAlt or Key.RightAlt) { e.Handled = true; return; }
        if (key == Key.Escape && _dragOptions is { } start)
        { _options = start; _dragOptions = null; ReleaseMouseCapture(); InvalidateVisual(); Edited?.Invoke(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && key is Key.F or Key.S or Key.D or Key.R)
        { ApplyShortcut(key); e.Handled = true; return; }
        if (HasSource && key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
            _options = CaptureGeometry.Drag(_options, _sourceWidth, _sourceHeight, CaptureHandle.Move,
                key == Key.Left ? -step : key == Key.Right ? step : 0, key == Key.Up ? -step : key == Key.Down ? step : 0, snap: false);
            InvalidateVisual(); Edited?.Invoke(); e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if ((e.Key == Key.System ? e.SystemKey : e.Key) is Key.LeftAlt or Key.RightAlt) e.Handled = true;
        base.OnPreviewKeyUp(e);
    }
}
