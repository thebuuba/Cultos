using Culto.Core;
using Culto.Remote;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private IReadOnlyList<RemoteScene> BuildRemoteScenes()
    {
        // A phone can use Scenes even while its desktop panel is hidden.
        RefreshYouTube();
        return _workspace.SceneTiles.Select(scene =>
        {
            var cue = ConfiguredSceneCue(scene);
            var kind = scene.Action is "cue" or "program" ? cue?.Kind switch
            {
                CueKind.BrowserSource => "browser", CueKind.WindowCapture => "youtube",
                { } value => value.ToString().ToLowerInvariant(), _ => "cue"
            } : scene.Action;
            var available = cue?.ProgramTitle is { } program ? File.Exists(program.ThemeImagePath) && File.Exists(program.PurposeImagePath) : scene.Action switch
            {
                "logo" => AvailableLogoPaths().Any(), "blackout" => true,
                _ => cue?.Kind switch
                {
                    CueKind.Image or CueKind.Video or CueKind.Audio or CueKind.PowerPoint => File.Exists(cue.FilePath),
                    CueKind.WindowCapture => cue.Capture is { } capture && WindowCaptureCatalog.WindowStillExists(capture),
                    CueKind.BrowserSource => cue.Browser is not null,
                    CueKind.Scene => SceneSourcesAvailable(cue.Scene),
                    CueKind.Text or CueKind.Bible => true, _ => false
                }
            };
            return new RemoteScene(scene.Id, scene.Label, kind, available, ConfiguredSceneActive(scene), cue?.Title, scene.ImageData, scene.Color);
        }).ToArray();
    }

    private static bool SceneSourcesAvailable(SceneComposition? scene)
    {
        if (scene is null) return false;
        try { scene.Validate(); }
        catch (ArgumentException) { return false; }
        return scene.Layers.Where(layer => layer.Visible).All(layer => layer.Source.Kind switch
        {
            CueKind.Image or CueKind.Video or CueKind.Audio => File.Exists(layer.Source.FilePath),
            CueKind.BrowserSource => layer.Source.Browser is not null,
            CueKind.WindowCapture => layer.Source.Capture is { } capture && WindowCaptureCatalog.WindowStillExists(capture),
            _ => true
        });
    }

    private CommandResult ExecuteRemoteSceneMode(string? id, bool preserve)
    {
        CommandResult result = new(false, "Esta escena ya no está disponible.");
        if (_workspace.SceneTiles.FirstOrDefault(scene => scene.Id == id) is not { } configured) return result;
        WithSceneTransition(configured, () => result = ExecuteRemoteSceneModeCore(id, preserve));
        return result;
    }

    private CommandResult ExecuteRemoteSceneModeCore(string? id, bool preserve)
    {
        var scene = BuildRemoteScenes().FirstOrDefault(item => item.Id == id);
        if (scene is null) return new(false, "Esta escena ya no está disponible.");
        if (!scene.Available) return new(false, scene.Detail ?? "Elige el contenido de esta escena en la PC.");
        if (preserve && scene.Kind is not ("logo" or "blackout" or "image" or "video" or "audio" or "text" or "bible")) return new(false, "Esta escena se controla normalmente.");
        var configured = _workspace.SceneTiles.First(item => item.Id == id);
        if (configured.Cue is { } fixedCue)
        {
            if (!CheckMedia(fixedCue)) return new(false, "Revisa el contenido de esta escena en la PC.");
            PrepareCue(CopyCue(fixedCue));
            if (preserve) return new(ProjectPreparedPreservingOther(), StatusText.Text);
            ProjectPrepared();
            return new(_output is not null && _session.Live?.Id == fixedCue.Id, "Escena enviada a pantalla.");
        }
        switch (configured.Action)
        {
            case "blackout":
                if (preserve) ShowBlackPreservingAudio(); else ShowBlackBackdrop();
                return new(_output is not null, _session.IsBlackout ? "Fondo negro." : "Contenido restaurado.");
            case "logo":
                return new(preserve ? ShowLogoPreservingAudio() : ShowLogo(), "Logo en pantalla.");
            case "youtube":
                var source = SceneYouTubeSource();
                if (source is not { IsAvailable: true } || !CheckMedia(source.Cue))
                    return new(false, "Abre YouTube en Chrome en la PC.");
                ProjectSceneYouTube(source);
                return new(_output is not null && _session.Live?.Capture?.WindowHandle == source.Cue.Capture?.WindowHandle,
                    "YouTube enviado a pantalla.");
            case "image":
            case "video":
                var cue = SceneMedia(configured.Action == "image" ? CueKind.Image : CueKind.Video);
                if (configured.Action == "video" && !preserve)
                {
                    if (cue is null || !CheckMedia(cue)) return new(false, "Prepara un video en la PC.");
                    return new(ShowSceneVideo(cue), "Video en pantalla.");
                }
                if (cue is null || !CheckMedia(cue) || !RememberSceneMedia(cue)) return new(false, "Elige el archivo en la PC.");
                PrepareCue(CopyCue(cue));
                if (preserve) return new(ProjectPreparedPreservingOther(), StatusText.Text);
                ProjectPrepared();
                return new(_output is not null && _session.Live?.Id == cue.Id, "Escena enviada a pantalla.");
            case "browser":
                var browser = _workspace.BrowserSources.FirstOrDefault(c => c.Id == _selectedBrowserSource)
                    ?? _workspace.BrowserSources.FirstOrDefault();
                if (browser is null || !CheckMedia(browser)) return new(false, "Revisa la fuente navegador en la PC.");
                ProjectBrowserSource(browser);
                return new(_output is not null && _session.Live?.Id == browser.Id, "Navegador enviado a pantalla.");
            default:
                return new(false, "Escena no disponible.");
        }
    }
}
