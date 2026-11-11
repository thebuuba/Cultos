using Culto.Core;
using Culto.Remote;

namespace Culto.Desktop.Services;

public sealed class WorkspaceData
{
    public ServicePlan Plan { get; set; } = new();
    public List<Cue> Library { get; set; } = [];
    public List<DeckButton> Deck { get; set; } =
    [
        new("project", "Proyectar preparado", "#43D9BE", "project"),
        new("previous", "Anterior", "#A7C7FF", "previous"),
        new("next", "Siguiente", "#43D9BE", "next"),
        new("pause", "Pausar / continuar", "#FFCF75", "pause"),
        new("blackout", "Pantalla negra", "#FF9DAD", "blackout"),
        new("background", "Mostrar fondo", "#A7C7FF", "background")
    ];
    public string? DisplayDevice { get; set; }
    public int Volume { get; set; } = 80;
    public bool Loop { get; set; }
}

public sealed class WorkspaceStore
{
    public string DirectoryPath { get; }
    public string BiblePath => Path.Combine(DirectoryPath, "biblia.json");
    public WorkspaceStore()
    {
        DirectoryPath = Path.GetFullPath(Environment.GetEnvironmentVariable("CULTO_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CultoStudio"));
        Directory.CreateDirectory(DirectoryPath);
    }
    public WorkspaceData Load()
    {
        var data = JsonFileStore.Load<WorkspaceData>(Path.Combine(DirectoryPath, "workspace.json"));
        if (data is not null)
        {
            data.Plan ??= new(); data.Plan.Items ??= []; data.Library ??= []; data.Deck ??= [];
            data.Volume = Math.Clamp(data.Volume, 0, 100);
            return data;
        }
        var welcome = new Cue { Title = "Bienvenidos", Body = "Bienvenidos\na la casa de Dios", Kind = CueKind.Text };
        var prayer = new Cue { Title = "Momento de oración", Body = "Unidos en oración", Kind = CueKind.Text };
        var closing = new Cue { Title = "Despedida", Body = "Que Dios te acompañe\ndurante esta semana", Kind = CueKind.Text };
        var items = new List<Cue> { welcome, prayer, closing };
        return new WorkspaceData
        {
            Plan = new ServicePlan { Name = "Mi primer culto", Items = items },
            Library = items.Select(c => new Cue { Title = c.Title, Body = c.Body, Kind = c.Kind }).ToList()
        };
    }
    public void ArchiveDamagedFile(string fileName)
    {
        if (fileName is not ("workspace.json" or "biblia.json")) throw new ArgumentException("Archivo de recuperación no admitido.", nameof(fileName));
        var suffix = $".recovery-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        foreach (var name in new[] { fileName, fileName + ".bak" })
        {
            var source = Path.GetFullPath(Path.Combine(DirectoryPath, name));
            var destination = source + suffix;
            if (!string.Equals(Path.GetDirectoryName(source), DirectoryPath, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(destination), DirectoryPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("La ruta de recuperación debe permanecer dentro de la carpeta de datos.");
            if (File.Exists(source)) File.Move(source, destination);
        }
    }
    public void Save(WorkspaceData data) => JsonFileStore.Save(Path.Combine(DirectoryPath, "workspace.json"), data);
    public void Archive(ServicePlan plan)
    {
        if (plan.Items.Count == 0) return;
        var path = Path.Combine(DirectoryPath, "Historial", $"culto-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        JsonFileStore.Save(path, plan);
    }
}
