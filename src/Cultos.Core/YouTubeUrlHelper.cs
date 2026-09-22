namespace Cultos.Core;

public static class YouTubeUrlHelper
{
    public static bool TryBuildEmbedUrl(string input, bool autoplay, out string embedUrl)
    {
        embedUrl = "";
        if (string.IsNullOrWhiteSpace(input)) return false;

        var value = input.Trim();
        string? videoId = null;

        if (!value.Contains("://", StringComparison.Ordinal))
            value = "https://" + value;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
            if (host.StartsWith("m.", StringComparison.Ordinal)) host = host[2..];

            if (host == "youtu.be")
            {
                videoId = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }
            else if (host is "youtube.com" or "youtube-nocookie.com")
            {
                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length >= 2 && segments[0] is "shorts" or "embed" or "live")
                    videoId = segments[1];
                else if (segments.Length >= 1 && segments[0] == "watch")
                    videoId = QueryValue(uri.Query, "v");
                else
                    videoId = QueryValue(uri.Query, "v");
            }
        }

        if (!IsValidVideoId(videoId)) return false;

        embedUrl =
            $"https://www.youtube-nocookie.com/embed/{videoId}?autoplay={(autoplay ? 1 : 0)}&rel=0&modestbranding=1";
        return true;
    }

    public static string? ExtractVideoId(string input) =>
        TryBuildEmbedUrl(input, false, out var url)
            ? new Uri(url).AbsolutePath.Trim('/').Split('/').LastOrDefault()
            : null;

    private static string? QueryValue(string query, string key)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = separator >= 0 ? pair[..separator] : pair;
            if (!string.Equals(Uri.UnescapeDataString(name), key, StringComparison.OrdinalIgnoreCase)) continue;
            var value = separator >= 0 ? pair[(separator + 1)..] : "";
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return null;
    }

    private static bool IsValidVideoId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 6 and <= 32 &&
        value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
}
