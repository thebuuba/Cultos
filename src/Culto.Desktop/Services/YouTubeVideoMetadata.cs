using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Windows.Media.Imaging;
using Culto.Core;

namespace Culto.Desktop.Services;

public static class YouTubeVideoMetadata
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Dictionary<string, BitmapSource> Thumbnails = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim ThumbnailWork = new(2);

    public static async Task<string?> ReadTitleAsync(YouTubeLink link, CancellationToken token = default)
    {
        try
        {
            var endpoint = "https://www.youtube.com/oembed?format=json&url=" + Uri.EscapeDataString(link.Url);
            using var json = JsonDocument.Parse(await Client.GetStringAsync(endpoint, token));
            return json.RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or TaskCanceledException)
        { token.ThrowIfCancellationRequested(); return null; }
    }

    public static async Task<BitmapSource?> ReadThumbnailAsync(string url, CancellationToken token)
    {
        if (!YouTubeLink.TryParse(url, out var link)) return null;
        lock (Thumbnails) if (Thumbnails.TryGetValue(link!.VideoId, out var cached)) return cached;
        await ThumbnailWork.WaitAsync(token);
        try
        {
            lock (Thumbnails) if (Thumbnails.TryGetValue(link!.VideoId, out var cached)) return cached;
            using var response = await Client.GetAsync(link!.ThumbnailUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 2_000_000) return null;
            using var source = await response.Content.ReadAsStreamAsync(token);
            using var data = new MemoryStream();
            var block = new byte[16384];
            int count;
            while ((count = await source.ReadAsync(block, token)) > 0)
            {
                data.Write(block, 0, count);
                if (data.Length > 2_000_000) return null;
            }
            data.Position = 0;
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 256;
            image.StreamSource = data; image.EndInit(); image.Freeze();
            lock (Thumbnails)
            {
                if (Thumbnails.Count >= 128) Thumbnails.Clear();
                Thumbnails[link.VideoId] = image;
            }
            return image;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or NotSupportedException or ArgumentException or FormatException or TaskCanceledException)
        { token.ThrowIfCancellationRequested(); return null; }
        finally { ThumbnailWork.Release(); }
    }
}
