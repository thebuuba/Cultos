using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Culto.Core;

public sealed record AppRelease(Version Version, Uri InstallerUrl, string Sha256, long Size, string Notes);

public sealed class AppUpdateClient
{
    public static string Repository { get; } = ReadRepository();
    private const long MaximumInstallerSize = 1_500_000_000;
    private readonly HttpClient _http;
    private readonly string _repository;
    public Uri FeedUrl => new($"https://github.com/{_repository}/releases/latest/download/update.json");

    public AppUpdateClient(HttpClient http, string? repository = null)
    {
        _http = http;
        _repository = repository ?? Repository;
        if (!Regex.IsMatch(_repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
            throw new ArgumentException("El repositorio de actualizaciones no es válido.");
    }

    private static string ReadRepository()
    {
        using var stream = typeof(AppUpdateClient).Assembly.GetManifestResourceStream("Culto.UpdateChannel.json")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("repository").GetString()!;
    }

    private static Version Normalize(Version value) => new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));

    private void Validate(AppRelease release)
    {
        var expected = new Uri($"https://github.com/{_repository}/releases/download/v{release.Version}/CultoStudio-{release.Version}-Windows-x64-Setup.exe");
        if (release.InstallerUrl != expected || release.Size is <= 0 or > MaximumInstallerSize ||
            !Regex.IsMatch(release.Sha256, "^[0-9a-fA-F]{64}$"))
            throw new InvalidDataException("La información de la actualización no es válida.");
    }

    public async Task<AppRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var response = await _http.GetAsync(FeedUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int length;
        while ((length = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (content.Length + length > 65536) throw new InvalidDataException("La información de la actualización es demasiado grande.");
            content.Write(buffer, 0, length);
        }
        try
        {
            using var json = JsonDocument.Parse(content.ToArray());
            var root = json.RootElement;
            var text = root.GetProperty("version").GetString();
            if (text is null || !Regex.IsMatch(text, @"^\d+\.\d+\.\d+(\.\d+)?$") || !Version.TryParse(text, out var version))
                throw new InvalidDataException("La versión publicada no es válida.");
            var release = new AppRelease(version, new Uri(root.GetProperty("installerUrl").GetString()!),
                root.GetProperty("sha256").GetString()!, root.GetProperty("size").GetInt64(),
                root.TryGetProperty("notes", out var notes) ? notes.GetString() ?? "" : "");
            Validate(release);
            return Normalize(release.Version) > Normalize(currentVersion) ? release : null;
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or UriFormatException or ArgumentNullException or FormatException)
        { throw new InvalidDataException("No se pudo leer la información de la actualización.", error); }
    }

    public async Task<string> DownloadAsync(AppRelease release, string directory, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        cancellationToken = timeout.Token;
        Validate(release);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, $"CultoStudio-{release.Version}-Setup.exe");
        if (File.Exists(target) && new FileInfo(target).Length == release.Size)
        {
            await using var existing = File.OpenRead(target);
            if (CryptographicOperations.FixedTimeEquals(await SHA256.HashDataAsync(existing, cancellationToken), Convert.FromHexString(release.Sha256)))
            { progress?.Report(100); return target; }
        }
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".partial");
        try
        {
            using var response = await _http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long size && size != release.Size)
                throw new InvalidDataException("El tamaño de la descarga no coincide con la versión publicada.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long total = 0;
                int length;
                while ((length = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += length;
                    if (total > release.Size) throw new InvalidDataException("La descarga supera el tamaño publicado.");
                    hash.AppendData(buffer, 0, length);
                    await output.WriteAsync(buffer.AsMemory(0, length), cancellationToken);
                    progress?.Report(100d * total / release.Size);
                }
                if (total != release.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(release.Sha256)))
                    throw new InvalidDataException("La descarga no pasó la verificación. Vuelve a intentarlo.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, true);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
