using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Culto.Desktop.Services;

public sealed partial class PowerPointPresentation
{
    public bool IsPaused => _isPaused;

    public void ExportTransitionFrame(int slide, string path)
    {
        // Capture the owned slideshow's current builds/media when available.
        // A not-yet-started show uses Office's full-resolution slide export.
        if (IsRunning && slide == SlideIndex && GetClientRect(Handle, out var bounds)
            && bounds.Right > 0 && bounds.Bottom > 0)
        {
            using var bitmap = new Bitmap(bounds.Right, bounds.Bottom, PixelFormat.Format32bppRgb);
            bool captured;
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var dc = graphics.GetHdc();
                try
                {
                    captured = PrintWindow(Handle, dc, 3);
                }
                finally { graphics.ReleaseHdc(dc); }
            }
            if (captured && Enumerable.Range(0, 9).Any(i =>
                bitmap.GetPixel(bitmap.Width * (i % 3 + 1) / 4, bitmap.Height * (i / 3 + 1) / 4).ToArgb() != Color.Black.ToArgb()))
            { bitmap.Save(path, ImageFormat.Png); return; }
        }
        ExportSlidePreview(slide, path, 1920, 1080);
    }

    [StructLayout(LayoutKind.Sequential)] private struct FrameRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out FrameRect rect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
}
