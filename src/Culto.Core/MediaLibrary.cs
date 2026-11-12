using System.Text.RegularExpressions;

namespace Culto.Core;

public static class MediaLibrary
{
    private static CueKind? Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mkv" or ".mp4" or ".mov" or ".webm" or ".avi" or ".wmv" => CueKind.Video,
        ".mp3" or ".wav" or ".flac" or ".ogg" => CueKind.Audio,
        ".jpg" or ".jpeg" or ".png" or ".bmp" => CueKind.Image,
        ".pptx" or ".ppt" or ".ppsx" or ".pps" => CueKind.PowerPoint,
        _ => null
    };

    public static bool IsSupported(string path) => !string.IsNullOrWhiteSpace(path) && Kind(path) is not null;

    public static Cue FromFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("No se encontró el archivo multimedia.", path);
        var kind = Kind(path) ?? throw new ArgumentException("Formato multimedia no admitido.", nameof(path));
        var title = Path.GetFileNameWithoutExtension(path);
        var number = Regex.Match(title, @"^(?:himno\s*)?(\d{1,4})(?=\s|[-_.]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return new Cue
        {
            Title = title, Kind = kind, FilePath = Path.GetFullPath(path),
            HymnNumber = kind is CueKind.Video or CueKind.Audio && number.Success && int.TryParse(number.Groups[1].Value, out var hymn) && hymn > 0 ? hymn : null
        };
    }
}
