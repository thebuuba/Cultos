using System.Text.Json;
using Culto.Core;

namespace Culto.Desktop.Services;

// Distribution content is separate from user workspaces. Only a fresh profile
// is seeded; updating the application never replaces an existing church's data.
public static class BundledContent
{
    public sealed class Manifest
    {
        public int SchemaVersion { get; set; }
        public List<Hymn> Hymns { get; set; } = [];
        public List<string> Bibles { get; set; } = [];
        public List<string> Backgrounds { get; set; } = [];
        public string? DefaultBible { get; set; }
    }
    public sealed class Hymn
    {
        public int Number { get; set; }
        public string Title { get; set; } = "";
        public string Path { get; set; } = "";
    }

    public static void Initialize(WorkspaceData workspace, string applicationDirectory, string dataDirectory)
    {
        var content = Path.GetFullPath(Path.Combine(applicationDirectory, "Content"));
        var manifestPath = Path.Combine(content, "content-manifest.json");
        if (!File.Exists(manifestPath)) return; // Lightweight distribution.
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("El contenido incluido no tiene manifiesto.");
        if (manifest.SchemaVersion != 1 || manifest.Hymns is null || manifest.Bibles is null || manifest.Backgrounds is null)
            throw new InvalidDataException("El manifiesto del contenido incluido no es válido.");
        string Resolve(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Ruta de contenido no válida.");
            var path = Path.GetFullPath(Path.Combine(content, relative));
            if (!path.StartsWith(content + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidDataException("No se encuentra un archivo del contenido incluido: " + relative);
            return path;
        }
        // Validate the entire manifest before creating any profile content.
        var hymns = manifest.Hymns.Select(hymn =>
        {
            if (hymn.Number is < 1 or > 9999 || string.IsNullOrWhiteSpace(hymn.Title)) throw new InvalidDataException("Himno incluido no válido.");
            var cue = MediaLibrary.FromFile(Resolve(hymn.Path));
            if (cue.Kind is not (CueKind.Video or CueKind.Audio)) throw new InvalidDataException("Formato de himno incluido no válido.");
            cue.Title = hymn.Title; cue.HymnNumber = hymn.Number;
            return cue;
        }).ToArray();
        var bibles = manifest.Bibles.Select(path => new BibleCatalog(JsonFileStore.Load<BibleDocument>(Resolve(path))
            ?? throw new InvalidDataException("Biblia incluida vacía."))).ToArray();
        var backgrounds = manifest.Backgrounds.Select(Resolve).ToArray();
        if (manifest.DefaultBible is not null && !manifest.Bibles.Contains(manifest.DefaultBible, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("La Biblia predeterminada no forma parte del paquete.");
        var versions = new BibleVersionStore(dataDirectory);
        var installedNames = versions.List().Select(version => version.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var bible in bibles)
            if (installedNames.Add(bible.Document.Name)) versions.Add(bible);
        var activeBiblePath = Path.Combine(dataDirectory, "biblia.json");
        if (!File.Exists(activeBiblePath) && !File.Exists(activeBiblePath + ".bak") && bibles.Length > 0)
        {
            var selected = manifest.DefaultBible is null ? 0 : manifest.Bibles.FindIndex(path => path.Equals(manifest.DefaultBible, StringComparison.OrdinalIgnoreCase));
            JsonFileStore.Save(activeBiblePath, bibles[selected].Document);
        }
        workspace.Library.AddRange(hymns);
        workspace.BackgroundImages = backgrounds.ToList();
        // Generic presentation backgrounds are available without choosing a
        // church logo or inheriting the distributor's personal style settings.
    }
}
