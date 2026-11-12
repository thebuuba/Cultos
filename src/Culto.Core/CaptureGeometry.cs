namespace Culto.Core;

// Normalized output coordinates; a value type keeps cue copies independent.
public readonly record struct WindowCaptureTransform(double X, double Y, double Width, double Height)
{
    public bool IsValid() => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height)
        && X is >= -16 and <= 16 && Y is >= -16 and <= 16 && Width is > 0 and <= 32 && Height is > 0 and <= 32;
}

public readonly record struct CaptureBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public enum CaptureHandle { Move, Left, Top, Right, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

public static class CaptureGeometry
{
    public const double CanvasWidth = 1920, CanvasHeight = 1080;

    public static CaptureBox Bounds(WindowCaptureOptions options, int sourceWidth, int sourceHeight)
    {
        if (options.Transform is { } transform)
            return new(transform.X * CanvasWidth, transform.Y * CanvasHeight, transform.Width * CanvasWidth, transform.Height * CanvasHeight);
        var width = Math.Max(1, sourceWidth - options.CropLeft - options.CropRight);
        var height = Math.Max(1, sourceHeight - options.CropTop - options.CropBottom);
        var scale = Math.Min(CanvasWidth / width, CanvasHeight / height);
        return new((CanvasWidth - width * scale) / 2, (CanvasHeight - height * scale) / 2, width * scale, height * scale);
    }

    public static WindowCaptureOptions Place(WindowCaptureOptions source, CaptureBox box) => source with
    { Transform = new(box.X / CanvasWidth, box.Y / CanvasHeight, box.Width / CanvasWidth, box.Height / CanvasHeight) };
    public static WindowCaptureOptions Fit(WindowCaptureOptions source, int width, int height) =>
        Place(source, Bounds(source with { Transform = null }, width, height));
    public static WindowCaptureOptions Reset(WindowCaptureOptions source) => source with
    { Transform = null, CropLeft = 0, CropTop = 0, CropRight = 0, CropBottom = 0 };
    public static WindowCaptureOptions Stretch(WindowCaptureOptions source) => source with { Transform = new(0, 0, 1, 1) };
    public static WindowCaptureOptions Center(WindowCaptureOptions source, int width, int height)
    {
        var box = Bounds(source, width, height);
        return Place(source, box with { X = (CanvasWidth - box.Width) / 2, Y = (CanvasHeight - box.Height) / 2 });
    }

    public static WindowCaptureOptions Drag(WindowCaptureOptions source, int width, int height, CaptureHandle handle,
        double dx, double dy, bool crop = false, bool stretch = false, bool snap = true, double snapDistance = 12)
    {
        var box = Bounds(source, width, height);
        bool left = handle is CaptureHandle.Left or CaptureHandle.TopLeft or CaptureHandle.BottomLeft;
        bool right = handle is CaptureHandle.Right or CaptureHandle.TopRight or CaptureHandle.BottomRight;
        bool top = handle is CaptureHandle.Top or CaptureHandle.TopLeft or CaptureHandle.TopRight;
        bool bottom = handle is CaptureHandle.Bottom or CaptureHandle.BottomLeft or CaptureHandle.BottomRight;
        if (handle == CaptureHandle.Move)
        {
            var x = box.X + dx; var y = box.Y + dy;
            if (snap)
            {
                x = Snap(x, snapDistance, 0, (CanvasWidth - box.Width) / 2, CanvasWidth - box.Width);
                y = Snap(y, snapDistance, 0, (CanvasHeight - box.Height) / 2, CanvasHeight - box.Height);
            }
            return Place(source, box with { X = Math.Clamp(x, -CanvasWidth * 15, CanvasWidth * 15), Y = Math.Clamp(y, -CanvasHeight * 15, CanvasHeight * 15) });
        }
        if (crop)
        {
            var scaleX = box.Width / Math.Max(1, width - source.CropLeft - source.CropRight);
            var scaleY = box.Height / Math.Max(1, height - source.CropTop - source.CropBottom);
            int l = source.CropLeft, r = source.CropRight, t = source.CropTop, b = source.CropBottom;
            if (left) l = Math.Clamp(l + (int)Math.Round(dx / scaleX), 0, Math.Max(0, Math.Min(8192, width - r - 1)));
            if (right) r = Math.Clamp(r - (int)Math.Round(dx / scaleX), 0, Math.Max(0, Math.Min(8192, width - l - 1)));
            if (top) t = Math.Clamp(t + (int)Math.Round(dy / scaleY), 0, Math.Max(0, Math.Min(8192, height - b - 1)));
            if (bottom) b = Math.Clamp(b - (int)Math.Round(dy / scaleY), 0, Math.Max(0, Math.Min(8192, height - t - 1)));
            var result = source with { CropLeft = l, CropRight = r, CropTop = t, CropBottom = b };
            return Place(result, new(box.X + (l - source.CropLeft) * scaleX, box.Y + (t - source.CropTop) * scaleY,
                (width - l - r) * scaleX, (height - t - b) * scaleY));
        }
        double x1 = box.X + (left ? dx : 0), x2 = box.Right + (right ? dx : 0);
        double y1 = box.Y + (top ? dy : 0), y2 = box.Bottom + (bottom ? dy : 0);
        if (snap)
        {
            if (left) x1 = Snap(x1, snapDistance, 0, CanvasWidth / 2, CanvasWidth);
            if (right) x2 = Snap(x2, snapDistance, 0, CanvasWidth / 2, CanvasWidth);
            if (top) y1 = Snap(y1, snapDistance, 0, CanvasHeight / 2, CanvasHeight);
            if (bottom) y2 = Snap(y2, snapDistance, 0, CanvasHeight / 2, CanvasHeight);
        }
        double w = Math.Clamp(x2 - x1, 16, CanvasWidth * 16), h = Math.Clamp(y2 - y1, 16, CanvasHeight * 16);
        if (!stretch)
        {
            double scale = left || right ? w / box.Width : h / box.Height;
            if ((left || right) && (top || bottom) && Math.Abs(h / box.Height - 1) > Math.Abs(scale - 1)) scale = h / box.Height;
            scale = Math.Clamp(scale, Math.Max(16 / box.Width, 16 / box.Height),
                Math.Min(CanvasWidth * 16 / box.Width, CanvasHeight * 16 / box.Height));
            w = box.Width * scale; h = box.Height * scale;
        }
        return Place(source, new(left ? box.Right - w : right ? box.X : box.X + (box.Width - w) / 2,
            top ? box.Bottom - h : bottom ? box.Y : box.Y + (box.Height - h) / 2, w, h));
    }

    private static double Snap(double value, double distance, params double[] targets)
    {
        var nearest = targets.MinBy(target => Math.Abs(target - value));
        return Math.Abs(nearest - value) <= distance ? nearest : value;
    }
}
