using System.Globalization;
using System.Text.RegularExpressions;

namespace Culto.Core;

public sealed record YouTubeLink(string VideoId, int StartSeconds = 0)
{
    public string Url => "https://www.youtube.com/watch?v=" + VideoId + (StartSeconds > 0 ? "&t=" + StartSeconds.ToString(CultureInfo.InvariantCulture) + "s" : "");
    public string ThumbnailUrl => "https://i.ytimg.com/vi/" + VideoId + "/hqdefault.jpg";

    public static bool TryParse(string? text, out YouTubeLink? link)
    {
        link = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;
        var host = uri.IdnHost.ToLowerInvariant();
        if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtu.be" or "www.youtu.be")) return false;
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            query.TryAdd(Uri.UnescapeDataString(parts[0]), parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : "");
        }
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var id = host is "youtu.be" or "www.youtu.be" ? (segments.Length == 1 ? segments[0] : "")
            : uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase) ? query.GetValueOrDefault("v", "")
            : segments.Length == 2 && segments[0] is "shorts" or "live" or "embed" ? segments[1] : "";
        if (!Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)) return false;
        var time = query.GetValueOrDefault("t", query.GetValueOrDefault("start", ""));
        var seconds = 0;
        if (int.TryParse(time, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) seconds = Math.Clamp(number, 0, 86400);
        else if (Regex.Match(time, "^(?:(\\d{1,5})h)?(?:(\\d{1,5})m)?(?:(\\d{1,5})s)?$", RegexOptions.CultureInvariant) is { Success: true } match)
        {
            int Value(int group) => int.TryParse(match.Groups[group].Value, out var value) ? value : 0;
            seconds = Math.Clamp(Value(1) * 3600 + Value(2) * 60 + Value(3), 0, 86400);
        }
        link = new(id, seconds);
        return true;
    }
}
