using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<string, Button> _sceneTileButtons = [];
    private string? _sceneTileSignature;

    private Cue? ConfiguredSceneCue(SceneTile scene) => scene.Cue ?? scene.Action switch
    {
        "image" => SceneMedia(CueKind.Image), "video" => SceneMedia(CueKind.Video),
        "browser" => _workspace.BrowserSources.FirstOrDefault(c => c.Id == _selectedBrowserSource) ?? _workspace.BrowserSources.FirstOrDefault(),
        "youtube" => SceneYouTubeSource()?.Cue, _ => null
    };

    private bool ConfiguredSceneActive(SceneTile scene)
    {
        if (_output is null) return false;
        if (scene.Action == "blackout") return _session.IsBlackout;
        if (scene.Action == "logo") return _session.IsTextHidden && !_session.IsBlackout;
        if (_session.IsBlackout || _session.IsTextHidden) return false;
        var cue = ConfiguredSceneCue(scene);
        return cue is not null && (cue.Kind == CueKind.WindowCapture
            ? _session.Live?.Capture?.WindowHandle == cue.Capture?.WindowHandle : _session.Live?.Id == cue.Id);
    }

    private void AddSceneClick(object sender, RoutedEventArgs e) => EditSceneTile(null);

    private void PreviewConfiguredScene(SceneTile scene)
    {
        var cue = ConfiguredSceneCue(scene);
        if (scene.Action == "logo") cue = AvailableLogoPaths().FirstOrDefault() is { } path
            ? new Cue { Kind = CueKind.Image, Title = "Logo", FilePath = path } : null;
        if (scene.Action == "blackout") cue = new Cue { Kind = CueKind.Scene, Title = "Fondo negro", Scene = new SceneComposition() };
        if (cue is null) { SetStatus("Configura primero el contenido de esta escena."); return; }
        if (!CheckMedia(cue)) return;
        PrepareCue(CopyCue(cue));
        _independentScenePreview = true;
        RenderPreparedPreview();
        ActivateWorkspacePanel("preview");
    }

    private void EditSceneTile(SceneTile? scene)
    {
        var sources = _workspace.Library.Concat(_plan).Concat(_workspace.BrowserSources).Concat(_workspace.YouTubeSources)
            .Concat(_workspace.SceneTiles.Select(tile => tile.Cue).OfType<Cue>())
            .Concat(_workspace.SceneTiles.SelectMany(tile => tile.Cue?.Scene?.Layers.Select(layer => layer.Source) ?? Enumerable.Empty<Cue>()))
            .Concat(YouTubeList.Items.OfType<YouTubeSource>().Select(source => source.Cue))
            .Concat(new[] { _workspace.QuickSceneImage, _workspace.QuickSceneVideo, _session.Prepared }.OfType<Cue>());
        var dialog = new SceneTileDialog(scene, sources, _workspace.TransitionPresets, composition =>
        {
            var preview = new Projection.ProjectionSurface(smoothTransitions: true) { Volume = 0 };
            preview.ShowCue(new Cue { Kind = CueKind.Scene, Scene = composition.Copy(), Title = "Vista previa de capas" });
            return preview;
        }) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is { } edited) SaveSceneTile(edited);
    }

    private bool SaveSceneTiles(List<SceneTile> scenes)
    {
        var previous = _workspace.SceneTiles;
        _workspace.SceneTiles = scenes;
        if (!SaveWorkspace()) { _workspace.SceneTiles = previous; return false; }
        _sceneTileSignature = null; RefreshConfiguredScenes();
        return true;
    }

    private bool SaveSceneTile(SceneTile scene)
    {
        var previous = _workspace.SceneTiles.FirstOrDefault(item => item.Id == scene.Id);
        var refreshLive = previous?.Cue?.ProgramTitle is not null && scene.Cue?.ProgramTitle is not null
            && _session.Live?.Id == previous.Cue.Id && !_session.IsBlackout && !_session.IsTextHidden;
        var refreshPreview = previous?.Cue is not null && _session.Prepared?.Id == previous.Cue.Id;
        var scenes = _workspace.SceneTiles.ToList();
        var index = scenes.FindIndex(item => item.Id == scene.Id);
        if (index < 0) scenes.Add(scene); else scenes[index] = scene;
        if (!SaveSceneTiles(scenes)) return false;
        if (scene.Cue is { } edited && (refreshLive || refreshPreview))
        {
            var cue = CopyCue(edited);
            cue.ProgramTitlePage = refreshLive ? _session.Live!.ProgramTitlePage : _session.Prepared!.ProgramTitlePage;
            PrepareCue(cue);
            if (refreshLive) ProjectPrepared();
        }
        SetStatus("Escena guardada: " + scene.AccessibleName); return true;
    }

    private bool RemoveSceneTile(string id)
    {
        var scenes = _workspace.SceneTiles.Where(scene => scene.Id != id).ToList();
        if (scenes.Count == _workspace.SceneTiles.Count || !SaveSceneTiles(scenes)) return false;
        SetStatus("Acceso eliminado. El archivo original y la proyección actual se conservan."); return true;
    }

    private bool MoveSceneTile(string id, int direction)
    {
        var scenes = _workspace.SceneTiles.ToList();
        var index = scenes.FindIndex(scene => scene.Id == id);
        var destination = index + Math.Sign(direction);
        if (index < 0 || destination < 0 || destination >= scenes.Count) return false;
        (scenes[index], scenes[destination]) = (scenes[destination], scenes[index]);
        return SaveSceneTiles(scenes);
    }

    private void DuplicateSceneTile(SceneTile scene) => SaveSceneTile(scene with
    { Id = Guid.NewGuid().ToString("N"), Label = string.IsNullOrWhiteSpace(scene.Label) ? "" : scene.Label + " (copia)", Cue = scene.Cue?.Copy(newId: true) });

    private void RefreshConfiguredScenes()
    {
        if (!_ready) return;
        var signature = System.Text.Json.JsonSerializer.Serialize(_workspace.SceneTiles);
        if (_sceneTileSignature != signature)
        {
            _sceneTileSignature = signature;
            OutputBackdropsPanel.Children.Clear(); _sceneTileButtons.Clear();
            var builtins = new Dictionary<string, Button> { ["logo"] = ChurchBackgroundButton, ["blackout"] = BlackoutButton,
                ["image"] = SceneImageButton, ["video"] = SceneVideoButton, ["youtube"] = SceneYouTubeButton, ["browser"] = SceneBrowserButton };
            foreach (var scene in _workspace.SceneTiles)
            {
                var button = builtins.GetValueOrDefault(scene.Id) ?? new Button { Style = (Style)FindResource("SceneButton") };
                if (!builtins.ContainsKey(scene.Id)) button.Click += ConfiguredSceneClick;
                button.Tag = scene.Id; button.Background = SceneTileVisual.Background(scene.Color); button.Foreground = SceneTileVisual.Ink(scene.Color);
                button.Content = SceneTileVisual.Content(scene); button.ToolTip = scene.AccessibleName + "\nClic derecho: editar, duplicar, mover o eliminar";
                AutomationProperties.SetName(button, scene.AccessibleName);
                button.ContextMenu = new ContextMenu();
                button.ContextMenu.Items.Add(new MenuItem { Header = "Editar escena…" });
                button.ContextMenu.Opened += (_, _) => FillSceneTileMenu(button, scene);
                _sceneTileButtons.Add(scene.Id, button); OutputBackdropsPanel.Children.Add(button);
            }
        }
        foreach (var scene in _workspace.SceneTiles)
            if (_sceneTileButtons.TryGetValue(scene.Id, out var button))
                AutomationProperties.SetItemStatus(button, ConfiguredSceneActive(scene) ? "En pantalla" : "");
    }

    private void FillSceneTileMenu(Button button, SceneTile scene)
    {
        var menu = button.ContextMenu!; menu.Items.Clear();
        void Add(string label, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = label, IsEnabled = enabled }; item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Add("Ver en vista previa", () => PreviewConfiguredScene(scene));
        if (scene.Cue is null && scene.Action == "youtube") SceneYouTubeMenuOpened(menu, new RoutedEventArgs());
        if (scene.Cue is null && scene.Action == "browser") SceneBrowserMenuOpened(menu, new RoutedEventArgs());
        if (scene.Action == "logo") Add("Configurar logo…", () => LogoSettingsClick(this, new RoutedEventArgs()));
        var kind = scene.Cue?.Kind;
        if (scene.Action is "logo" or "blackout" or "image" or "video" or "audio" || kind is CueKind.Image or CueKind.Video or CueKind.Audio or CueKind.Text or CueKind.Bible)
            Add(kind is CueKind.Video or CueKind.Audio || scene.Action is "video" or "audio" ? "Solo audio · conservar imagen" : "Mostrar conservando audio",
                () => { var result = ExecuteRemoteSceneMode(scene.Id, true); SetStatus(result.Message, !result.Success); });
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        Add("Editar escena…", () => EditSceneTile(scene));
        Add("Duplicar escena", () => DuplicateSceneTile(scene));
        var index = _workspace.SceneTiles.FindIndex(item => item.Id == scene.Id);
        Add("Mover antes", () => MoveSceneTile(scene.Id, -1), index > 0);
        Add("Mover después", () => MoveSceneTile(scene.Id, 1), index < _workspace.SceneTiles.Count - 1);
        menu.Items.Add(new Separator());
        Add("Eliminar escena", () => RemoveSceneTile(scene.Id));
    }

    private void WithSceneMenuTransition(ContextMenu menu, Action action)
    {
        if (menu.PlacementTarget is Button { Tag: string id } && _workspace.SceneTiles.FirstOrDefault(scene => scene.Id == id) is { } scene)
            WithSceneTransition(scene, action);
        else action();
    }

    private void ConfiguredSceneClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not Button { Tag: string id } || _workspace.SceneTiles.FirstOrDefault(scene => scene.Id == id) is not { } scene) return;
        WithSceneTransition(scene, () => ConfiguredSceneClickCore(sender, e, id, scene));
    }

    private void ConfiguredSceneClickCore(object sender, RoutedEventArgs e, string id, SceneTile scene)
    {
        var preserve = Keyboard.Modifiers == ModifierKeys.Shift;
        if (scene.Cue is not null || preserve)
        {
            var result = ExecuteRemoteSceneMode(id, preserve); SetStatus(result.Message, !result.Success); return;
        }
        switch (scene.Action)
        {
            case "logo": ChurchBackgroundClick(sender, e); break;
            case "blackout": BlackBackgroundClick(sender, e); break;
            case "youtube": SceneYouTubeClick(sender, e); break;
            case "browser": SceneBrowserClick(sender, e); break;
            case "image":
            case "video":
                SceneMediaClick(new Button { Tag = scene.Action == "image" ? "Image" : "Video" }, e); break;
            default: EditSceneTile(scene); break;
        }
        RefreshConfiguredScenes();
    }
}
