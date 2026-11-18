using System.Text.RegularExpressions;

namespace Culto.Core;

public static class MediaLibrary
{
    public const string VideoPatterns = "*.mkv;*.mp4;*.mov;*.webm;*.avi;*.wmv;*.flv;*.mpg;*.mpeg;*.m4v;*.3gp;*.3g2;*.vob;*.asf;*.m2ts";
    public const string AudioPatterns = "*.mp3;*.wav;*.flac;*.ogg;*.wma;*.m4a;*.aac;*.aif;*.aiff;*.opus;*.oga;*.m4b";
    public const string ImagePatterns = "*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.wdp";
    public const string PowerPointPatterns = "*.pptx;*.ppt;*.ppsx;*.pps";
    public const string VideoFileFilter = "Videos|" + VideoPatterns;
    public const string AudioFileFilter = "Audio|" + AudioPatterns;
    public const string ImageFileFilter = "Imágenes|" + ImagePatterns;
    public const string MediaFileFilter = "Contenido multimedia|" + VideoPatterns + ";" + AudioPatterns + ";" + ImagePatterns;
    public const string FileFilter = MediaFileFilter + ";" + PowerPointPatterns + "|PowerPoint|" + PowerPointPatterns + "|Todos los archivos|*.*";

    private static CueKind? Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mkv" or ".mp4" or ".mov" or ".webm" or ".avi" or ".wmv" or ".flv" or ".mpg" or ".mpeg" or ".m4v" or ".3gp" or ".3g2" or ".vob" or ".asf" or ".m2ts" => CueKind.Video,
        ".mp3" or ".wav" or ".flac" or ".ogg" or ".wma" or ".m4a" or ".aac" or ".aif" or ".aiff" or ".opus" or ".oga" or ".m4b" => CueKind.Audio,
        ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".wdp" => CueKind.Image,
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
