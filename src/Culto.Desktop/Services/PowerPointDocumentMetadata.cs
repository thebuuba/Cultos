using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Culto.Core;

namespace Culto.Desktop.Services;

public sealed record PowerPointSlideInfo(int Number, string Title);
public sealed record PowerPointFileMetadata(string Title, string FileName, string Details,
    BitmapSource? Cover, IReadOnlyList<PowerPointSlideInfo> Slides)
{
    private static readonly ConcurrentDictionary<string, PowerPointFileMetadata> Cache = new();
    public static PowerPointFileMetadata Get(Cue cue)
    {
        var file = new FileInfo(cue.FilePath ?? "missing");
        var key = file.FullName + "|" + (file.Exists ? file.LastWriteTimeUtc.Ticks + "|" + file.Length : "missing") + "|" + cue.Title;
        return Cache.GetOrAdd(key, _ => Read(cue, file));
    }
    public static void ClearCache() => Cache.Clear();
    private static PowerPointFileMetadata Read(Cue cue, FileInfo file)
    {
        var title = cue.Title;
        if (string.Equals(title, Path.GetFileNameWithoutExtension(file.Name), StringComparison.OrdinalIgnoreCase))
        {
            title = Regex.Replace(title, @"[-_]+", " ");
            title = Regex.Replace(title, @"^\d+[\s._-]+|\s*\(\d+\)$", "").Trim();
        }
        if (title.Length == 0) title = cue.Title;
        var slides = new List<PowerPointSlideInfo>();
        BitmapSource? cover = null;
        try
        {
            using var zip = ZipFile.OpenRead(file.FullName);
            XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
            XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var parts = new List<string>();
            if (zip.GetEntry("ppt/presentation.xml") is { } root && zip.GetEntry("ppt/_rels/presentation.xml.rels") is { } rels)
            {
                using var rootStream = root.Open(); using var relStream = rels.Open();
                var relationships = XDocument.Load(relStream).Root!.Elements()
                    .Where(e => (string?)e.Attribute("TargetMode") != "External")
                    .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
                foreach (var id in XDocument.Load(rootStream).Descendants(p + "sldId"))
                    if (relationships.TryGetValue((string?)id.Attribute(r + "id") ?? "", out var part))
                        parts.Add(new Uri(new Uri("https://package.invalid/ppt/presentation.xml"), part).AbsolutePath.TrimStart('/'));
            }
            foreach (var part in parts)
            {
                string text = "";
                if (zip.GetEntry(part) is { } entry)
                {
                    using var stream = entry.Open(); var xml = XDocument.Load(stream);
                    var heading = xml.Descendants(p + "sp").FirstOrDefault(shape => shape.Descendants(p + "ph")
                        .Any(ph => (string?)ph.Attribute("type") is "title" or "ctrTitle"));
                    text = heading is null ? xml.Descendants(a + "t").FirstOrDefault()?.Value ?? ""
                        : string.Join(" ", heading.Descendants(a + "t").Select(t => t.Value));
                }
                slides.Add(new(slides.Count + 1, string.IsNullOrWhiteSpace(text) ? $"Diapositiva {slides.Count + 1}" : text.Trim()));
            }
            var thumbnail = zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("docProps/thumbnail.", StringComparison.OrdinalIgnoreCase));
            if (thumbnail is { Length: > 0 and <= 10_000_000 })
            {
                using var stream = thumbnail.Open(); cover = LoadBitmap(stream, 160);
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
            or System.Xml.XmlException or NotSupportedException or FormatException or ArgumentException) { }
        string date = file.Exists ? file.LastWriteTime.ToString("dd MMM · HH:mm", CultureInfo.GetCultureInfo("es-DO")) : "Archivo no encontrado";
        string details = (slides.Count > 0 ? $"{slides.Count} diapositivas" : "PowerPoint") + " · " + date;
        return new(title, file.Name, details, cover, slides);
    }
    internal static BitmapImage LoadBitmap(Stream stream, int width)
    {
        // ZIP entry streams cannot seek; WIC otherwise defers decoding and the image cannot be frozen.
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); bytes.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = width; image.StreamSource = bytes; image.EndInit(); image.Freeze(); return image;
    }
}

public sealed class PowerPointMetadataConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Cue cue ? PowerPointFileMetadata.Get(cue) : Binding.DoNothing;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
