using System.Security.Cryptography;
using System.Text;

namespace Culto.Core;

public sealed record BibleVersion(string Name, string FilePath);

public sealed class BibleVersionStore(string dataDirectory)
{
    private readonly string directory = Path.Combine(Path.GetFullPath(dataDirectory), "Biblias");
    public List<string> Warnings { get; } = [];

    public BibleVersion Add(BibleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var name = catalog.Document.Name.Trim();
        if (name.Length == 0) throw new InvalidDataException("La versión de la Biblia necesita un nombre.");
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToUpperInvariant())));
        var path = Path.Combine(directory, key + ".json");
        JsonFileStore.Save(path, catalog.Document);
        return new(name, path);
    }

    public IReadOnlyList<BibleVersion> List()
    {
        Warnings.Clear();
        if (!Directory.Exists(directory)) return [];
        var versions = new List<BibleVersion>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            try
            {
                var document = JsonFileStore.Load<BibleDocument>(path);
                if (document is null || string.IsNullOrWhiteSpace(document.Name) || document.Verses is not { Count: > 0 })
                    throw new InvalidDataException("Versión sin datos.");
                versions.Add(new(document.Name, path));
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            { Warnings.Add(Path.GetFileName(path)); }
        }
        return versions.OrderBy(version => version.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public BibleCatalog Load(BibleVersion version)
    {
        var document = JsonFileStore.Load<BibleDocument>(version.FilePath)
            ?? throw new FileNotFoundException("No se encuentra esta versión de la Biblia.");
        return new BibleCatalog(document);
    }
}
