using Cultos.Core;
using System.IO;
using System.Text.Json;

namespace Cultos.App;

public sealed class ChurchProfileStore
{
    private readonly string _dataFolder;
    private readonly string _profilePath;
    private readonly string _assetFolder;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ChurchProfileStore(string dataFolder)
    {
        _dataFolder = dataFolder;
        _profilePath = Path.Combine(dataFolder, "profile.json");
        _assetFolder = Path.Combine(dataFolder, "profile-assets");
        Directory.CreateDirectory(_dataFolder);
        Directory.CreateDirectory(_assetFolder);
    }

    public ChurchProfile Load()
    {
        try
        {
            if (!File.Exists(_profilePath)) return new ChurchProfile();
            return JsonSerializer.Deserialize<ChurchProfile>(File.ReadAllText(_profilePath), _options)
                ?? new ChurchProfile();
        }
        catch (Exception ex)
        {
            AppLogger.Error("No se pudo leer profile.json", ex);
            return new ChurchProfile();
        }
    }

    public void Save(ChurchProfile profile)
    {
        profile.UpdatedAt = DateTime.Now;
        var temp = _profilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(profile, _options));
        File.Move(temp, _profilePath, true);
    }

    public string ImportAsset(string sourcePath, string category)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("El archivo seleccionado no existe.", sourcePath);

        var safeCategory = string.Concat(category.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safeCategory)) safeCategory = "asset";

        var extension = Path.GetExtension(sourcePath);
        var destination = Path.Combine(
            _assetFolder,
            $"{safeCategory}-{DateTime.Now:yyyyMMddHHmmssfff}{extension}");

        File.Copy(sourcePath, destination, true);
        return destination;
    }

    public void ExportPackage(string packagePath, ChurchProfile profile, IEnumerable<PresentationScene> scenes) =>
        ChurchProfilePackageService.Export(packagePath, profile, scenes);

    public ChurchProfileImportResult ImportPackage(string packagePath)
    {
        var folderName = "import-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
        var destination = Path.Combine(_dataFolder, "profiles", folderName);
        return ChurchProfilePackageService.Import(packagePath, destination);
    }
}
