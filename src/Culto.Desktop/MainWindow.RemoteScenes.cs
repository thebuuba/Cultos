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
        var image = SceneMedia(CueKind.Image);
        var video = SceneMedia(CueKind.Video);
        var youtube = SceneYouTubeSource();
        var live = _output is not null && !_session.IsBlackout && !_session.IsTextHidden ? _session.Live : null;
        var sources = new List<RemoteScene>
        {
            new("logo", "Logo", "logo", AvailableLogoPaths().Any(), _output is not null && _session.IsTextHidden && !_session.IsBlackout),
            new("blackout", "Fondo negro", "blackout", true, _output is not null && _session.IsBlackout),
            new("image", "Imagen", "image", image is not null && File.Exists(image.FilePath), live?.Kind == CueKind.Image, image?.Title),
            new("video", "Video", "video", video is not null && File.Exists(video.FilePath), live?.Kind == CueKind.Video, video?.Title),
            new("youtube", "YouTube", "youtube", youtube is { IsAvailable: true },
                live?.Kind == CueKind.WindowCapture && live.Capture is { } capture && YouTubeDiscovery.IsYouTube(capture),
                youtube?.Title ?? "Abre YouTube en Chrome en la PC")
        };
        // Saved HTML scenes share the same dispatcher without exposing their URLs.
        if (_workspace.BrowserSources.Count > 0)
            sources.Add(new("browser", "Navegador", "browser", true, live?.Kind == CueKind.BrowserSource));
        return sources;
    }

    private CommandResult ExecuteRemoteScene(string? id)
        => ExecuteRemoteSceneMode(id, false);

    private CommandResult ExecuteRemoteSceneMode(string? id, bool preserve)
    {
        var scene = BuildRemoteScenes().FirstOrDefault(item => item.Id == id);
        if (scene is null) return new(false, "Esta escena ya no está disponible.");
        if (!scene.Available) return new(false, scene.Detail ?? "Elige el contenido de esta escena en la PC.");
        if (preserve && id is not ("logo" or "blackout" or "image" or "video")) return new(false, "Esta escena se controla normalmente.");
        switch (id)
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
                var cue = SceneMedia(id == "image" ? CueKind.Image : CueKind.Video);
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
