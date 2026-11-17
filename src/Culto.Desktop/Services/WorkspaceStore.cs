using Culto.Core;
using Culto.Remote;

namespace Culto.Desktop.Services;

public sealed class WorkspaceData
{
    private string _churchName = "";
    public string ChurchName
    {
        get => _churchName;
        set
        {
            var name = value?.Trim() ?? "";
            _churchName = name.Length > 100 ? name[..100] : name;
        }
    }
    public bool FirstRunCompleted { get; set; }
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public ServicePlan Plan { get; set; } = new();
    public List<Cue> Library { get; set; } = [];
    public List<Cue> YouTubeSources { get; set; } = [];
    public List<Cue> BrowserSources { get; set; } = [];
    public Cue? QuickSceneImage { get; set; }
    public Cue? QuickSceneVideo { get; set; }
    public List<SceneTile> SceneTiles { get; set; } = SceneTile.Defaults();
    public WindowCaptureLayout? YouTubeLayout { get; set; }
    public Guid? ActiveVideoListId { get; set; }
    public List<SavedSet> VideoCollections { get; set; } = [];
    public List<SavedSet> SavedServices { get; set; } = [];
    public bool PresenterEnabled { get; set; }
    public bool StreamDeckEnabled { get; set; }
    public bool StreamDeckPluginEnabled { get; set; }
    public bool PresenterUsesArrows { get; set; }
    public List<DeckButton> Deck { get; set; } =
    [
        new("project", "Proyectar preparado", "#43D9BE", "project"),
        new("previous", "Anterior", "#A7C7FF", "previous"),
        new("next", "Siguiente", "#43D9BE", "next"),
        new("pause", "Pausar / continuar", "#FFCF75", "pause"),
        new("blackout", "Pantalla negra", "#FF9DAD", "blackout"),
        new("background", "Logo", "#A7C7FF", "background")
    ];
    public string? DisplayDevice { get; set; }
    public string? BackgroundImagePath { get; set; }
    public List<string> BackgroundImages { get; set; } = [];
    public LogoPlaybackOptions LogoPlayback { get; set; } = new();
    public BibleVisualStyle BibleStyle { get; set; } = new();
    public TransitionOptions Transitions { get; set; } = new();
    public List<TransitionPreset> TransitionPresets { get; set; } = [];
    public string? ActiveTransitionPresetId { get; set; }
    public int TransitionDefaultsVersion { get; set; }
    public int Volume { get; set; } = 80;
    public bool Loop { get; set; }
    public string MediaViewMode { get; set; } = "Thumbnails";
    public int MediaFolderFilter { get; set; } = 2;
    public string? MediaCustomFolderPath { get; set; }
    public bool MediaSortDescending { get; set; }
    public Dictionary<string, string> PanelTabColors { get; set; } = [];
    public List<WorkspaceNote> Notes { get; set; } = [];
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
            data.PanelTabColors ??= [];
            data.TransitionPresets = (data.TransitionPresets ?? []).Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Id))
                .DistinctBy(p => p.Id).Select(p => p with { Name = string.IsNullOrWhiteSpace(p.Name) ? "Transición" : p.Name.Trim(),
                    Options = (p.Options ?? new()).Normalize() }).ToList();
            if (!data.TransitionPresets.Any(p => p.Id == data.ActiveTransitionPresetId)) data.ActiveTransitionPresetId = null;
            data.SceneTiles = (data.SceneTiles ?? SceneTile.Defaults()).Where(scene => scene is not null
                && !string.IsNullOrWhiteSpace(scene.Id) && scene.Action is "logo" or "blackout" or "image" or "video" or "audio" or "youtube" or "browser" or "cue" or "scene" or "powerpoint")
                .DistinctBy(scene => scene.Id).ToList();
            data.Notes = (data.Notes ?? []).Where(note => note is not null).ToList();
            foreach (var note in data.Notes) { note.Title ??= ""; note.Body ??= ""; }
            data.YouTubeSources = (data.YouTubeSources ?? []).Where(cue => cue is not null && cue.Kind == CueKind.WindowCapture).ToList();
            data.BrowserSources = (data.BrowserSources ?? []).Where(cue => cue is not null && cue.Kind == CueKind.BrowserSource
                && cue.Browser is not null).ToList();
            if (data.QuickSceneImage?.Kind != CueKind.Image) data.QuickSceneImage = null;
            if (data.QuickSceneVideo?.Kind != CueKind.Video) data.QuickSceneVideo = null;
            foreach (var cue in data.Library.Where(cue => cue.Kind == CueKind.WindowCapture))
                if (data.YouTubeSources.All(source => source.Id != cue.Id)) data.YouTubeSources.Add(YouTubeDiscovery.Clone(cue));
            data.Library.RemoveAll(cue => cue.Kind == CueKind.WindowCapture);
            // Recover one unambiguous existing layout, including a cut that was
            // saved only on an independent copy in the worship order.
            if (data.YouTubeLayout is null)
            {
                var layouts = data.YouTubeSources.Concat(data.Plan.Items)
                    .Where(cue => cue?.Kind == CueKind.WindowCapture && cue.Capture is { } capture && YouTubeDiscovery.IsYouTube(capture))
                    .Select(cue => WindowCaptureLayout.From(cue.Capture!.Normalize()))
                    .Where(layout => layout != new WindowCaptureLayout()).Distinct().Take(2).ToArray();
                if (layouts.Length == 1) data.YouTubeLayout = layouts[0];
            }
            data.Deck = data.Deck.Select(button => button is { Action: "background", Label: "Mostrar fondo" }
                ? button with { Label = "Logo" } : button).ToList();
            data.VideoCollections = NormalizeSets(data.VideoCollections, videosOnly: true);
            data.SavedServices = NormalizeSets(data.SavedServices);
            data.BackgroundImages ??= [];
            data.LogoPlayback = (data.LogoPlayback ?? new()).Normalize();
            data.BibleStyle = (data.BibleStyle ?? new()).Normalize();
            data.Transitions = (data.Transitions ?? new()).UpgradeStoredDefaults(data.TransitionDefaultsVersion);
            data.TransitionDefaultsVersion = Math.Max(data.TransitionDefaultsVersion, 1);
            data.Volume = Math.Clamp(data.Volume, 0, 100);
            return data;
        }
        var welcome = new Cue { Title = "Bienvenidos", Body = "Bienvenidos\na la casa de Dios", Kind = CueKind.Text };
        var prayer = new Cue { Title = "Momento de oración", Body = "Unidos en oración", Kind = CueKind.Text };
        var closing = new Cue { Title = "Despedida", Body = "Que Dios te acompañe\ndurante esta semana", Kind = CueKind.Text };
        var items = new List<Cue> { welcome, prayer, closing };
        var initial = new WorkspaceData
        {
            TransitionDefaultsVersion = 1,
            Plan = new ServicePlan { Name = "Mi primer culto", Items = items },
            Library = items.Select(c => new Cue { Title = c.Title, Body = c.Body, Kind = c.Kind }).ToList()
        };
        BundledContent.Initialize(initial, AppContext.BaseDirectory, DirectoryPath);
        return initial;
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
    private static List<SavedSet> NormalizeSets(List<SavedSet>? sets, bool videosOnly = false) =>
        (sets ?? []).Where(set => set is not null).Select(set =>
        {
            set.Name = string.IsNullOrWhiteSpace(set.Name) ? "Sin título" : set.Name.Trim();
            set.Items = (set.Items ?? []).Where(cue => cue is not null && Enum.IsDefined(cue.Kind)
                && (!videosOnly || cue.Kind == CueKind.Video)).ToList();
            return set;
        }).ToList();
    public void Save(WorkspaceData data) => JsonFileStore.Save(Path.Combine(DirectoryPath, "workspace.json"), data);
    public void Archive(ServicePlan plan)
    {
        if (plan.Items.Count == 0) return;
        var path = Path.Combine(DirectoryPath, "Historial", $"culto-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        JsonFileStore.Save(path, plan);
    }
}
