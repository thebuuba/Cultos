using System.Windows.Media.Imaging;

namespace Culto.Desktop.Services;

public static class SceneArtwork
{
    private const string Prefix = "data:image/png;base64,";

    public static string Import(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > 10 * 1024 * 1024) throw new ArgumentException("Elige una imagen de hasta 10 MB.");
        var frame = BitmapFrame.Create(file, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var horizontal = frame.PixelWidth >= frame.PixelHeight;
        file.Position = 0;
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = file;
        if (horizontal) image.DecodePixelWidth = 192; else image.DecodePixelHeight = 192;
        image.EndInit(); image.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream(); encoder.Save(output);
        return Prefix + Convert.ToBase64String(output.ToArray());
    }

    public static BitmapSource? Decode(string? data)
    {
        if (data is null || data.Length > 300_000 || !data.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(data[Prefix.Length..]));
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream;
            image.EndInit(); image.Freeze(); return image;
        }
        catch (Exception error) when (error is FormatException or IOException or NotSupportedException or ArgumentException)
        { return null; }
    }
}
