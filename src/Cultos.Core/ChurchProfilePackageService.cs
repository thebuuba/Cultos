using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Cultos.Core;

public static class ChurchProfilePackageService
{
    private const int PackageVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private sealed class PackageManifest
    {
        public int PackageVersion { get; set; } = ChurchProfilePackageService.PackageVersion;
        public required ChurchProfile Profile { get; set; }
        public List<PresentationScene> Scenes { get; set; } = [];
    }

    public static void Export(string packagePath, ChurchProfile profile, IEnumerable<PresentationScene> scenes)
    {
        var fullPath = Path.GetFullPath(packagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (File.Exists(fullPath)) File.Delete(fullPath);

        using var archive = ZipFile.Open(fullPath, ZipArchiveMode.Create);
        var assetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var assetIndex = 0;

        string? AddAsset(string? localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath)) return null;

            var normalized = Path.GetFullPath(localPath);
            if (assetMap.TryGetValue(normalized, out var existing)) return existing;

            var safeName = SanitizeFileName(Path.GetFileName(normalized));
            var entryPath = $"assets/{++assetIndex:D3}-{safeName}";
            archive.CreateEntryFromFile(normalized, entryPath, CompressionLevel.Optimal);
            assetMap[normalized] = entryPath;
            return entryPath;
        }

        var portableProfile = CloneProfile(profile);
        portableProfile.LogoPath = AddAsset(profile.LogoPath);
        portableProfile.DefaultBackgroundPath = AddAsset(profile.DefaultBackgroundPath);

        var portableScenes = new List<PresentationScene>();
        foreach (var source in scenes.OrderBy(x => x.Position).ThenBy(x => x.Id))
        {
            var scene = CloneScene(source);
            scene.Id = 0;
            scene.MediaPath = AddAsset(source.MediaPath);

            if (scene.Type == SceneType.Logo)
            {
                var logo = DeserializeLogoSettings(source.SettingsJson);
                logo.ImagePaths = logo.ImagePaths
                    .Select(AddAsset)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToList();
                scene.SettingsJson = JsonSerializer.Serialize(logo, JsonOptions);
            }

            portableScenes.Add(scene);
        }

        var manifest = new PackageManifest
        {
            Profile = portableProfile,
            Scenes = portableScenes
        };

        var entry = archive.CreateEntry("profile.json", CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(JsonSerializer.Serialize(manifest, JsonOptions));
    }

    public static ChurchProfileImportResult Import(string packagePath, string destinationFolder)
    {
        var fullPath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("El paquete de perfil no existe.", fullPath);

        using var archive = ZipFile.OpenRead(fullPath);
        var manifestEntry = archive.GetEntry("profile.json")
            ?? throw new InvalidDataException("El paquete no contiene profile.json.");

        PackageManifest manifest;
        using (var stream = manifestEntry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            manifest = JsonSerializer.Deserialize<PackageManifest>(reader.ReadToEnd(), JsonOptions)
                ?? throw new InvalidDataException("El perfil no contiene datos válidos.");
        }

        if (manifest.PackageVersion <= 0 || manifest.PackageVersion > PackageVersion)
            throw new InvalidDataException("La versión del paquete de perfil no es compatible.");

        var root = Path.GetFullPath(destinationFolder);
        Directory.CreateDirectory(root);
        var extracted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? ExtractAsset(string? portablePath)
        {
            if (string.IsNullOrWhiteSpace(portablePath)) return null;
            if (!portablePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                return null;
            if (extracted.TryGetValue(portablePath, out var existing)) return existing;

            var entry = archive.GetEntry(portablePath)
                ?? throw new InvalidDataException($"Falta un archivo del perfil: {portablePath}");

            var relative = portablePath.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(root, relative));
            var expectedPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El paquete contiene una ruta no válida.");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
            extracted[portablePath] = destination;
            return destination;
        }

        var profile = CloneProfile(manifest.Profile);
        profile.LogoPath = ExtractAsset(manifest.Profile.LogoPath);
        profile.DefaultBackgroundPath = ExtractAsset(manifest.Profile.DefaultBackgroundPath);
        profile.UpdatedAt = DateTime.Now;

        var scenes = new List<PresentationScene>();
        foreach (var portable in manifest.Scenes.OrderBy(x => x.Position))
        {
            var scene = CloneScene(portable);
            scene.Id = 0;
            scene.MediaPath = ExtractAsset(portable.MediaPath);
            scene.UpdatedAt = DateTime.Now;

            if (scene.Type == SceneType.Logo)
            {
                var logo = DeserializeLogoSettings(portable.SettingsJson);
                logo.ImagePaths = logo.ImagePaths
                    .Select(ExtractAsset)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToList();
                scene.SettingsJson = JsonSerializer.Serialize(logo, JsonOptions);
            }

            scenes.Add(scene);
        }

        return new ChurchProfileImportResult
        {
            Profile = profile,
            Scenes = scenes
        };
    }

    private static ChurchProfile CloneProfile(ChurchProfile source) => new()
    {
        Version = source.Version,
        Name = source.Name,
        ShortName = source.ShortName,
        Location = source.Location,
        LogoPath = source.LogoPath,
        DefaultBackgroundPath = source.DefaultBackgroundPath,
        AccentHex = source.AccentHex,
        PreferredBibleTranslation = source.PreferredBibleTranslation,
        PreferredHymnal = source.PreferredHymnal,
        UpdatedAt = source.UpdatedAt
    };

    private static PresentationScene CloneScene(PresentationScene source) => new()
    {
        Id = source.Id,
        Key = source.Key,
        Name = source.Name,
        Type = source.Type,
        Position = source.Position,
        IsBuiltIn = source.IsBuiltIn,
        Title = source.Title,
        Content = source.Content,
        MediaPath = source.MediaPath,
        SettingsJson = source.SettingsJson,
        UpdatedAt = source.UpdatedAt
    };

    private static LogoSceneSettings DeserializeLogoSettings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<LogoSceneSettings>(json, JsonOptions) ?? new LogoSceneSettings();
        }
        catch
        {
            return new LogoSceneSettings();
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "archivo.bin" : cleaned;
    }
}
