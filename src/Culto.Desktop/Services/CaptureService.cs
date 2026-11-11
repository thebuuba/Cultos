using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Culto.Desktop.Services;

public static class CaptureService
{
    public static string CaptureDisplay(DisplayInfo display, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (display.Width <= 0 || display.Height <= 0) throw new ArgumentException("La pantalla no tiene dimensiones válidas.", nameof(display));
        string path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new Bitmap(display.Width, display.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(display.Left, display.Top, 0, 0, new Size(display.Width, display.Height), CopyPixelOperation.SourceCopy);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }
}
