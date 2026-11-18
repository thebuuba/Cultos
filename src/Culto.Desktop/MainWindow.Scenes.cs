using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool _refreshingScenes;

    private void UpdateSceneActiveStates()
    {
        var live = _output is not null && !_session.IsBlackout && !_session.IsTextHidden ? _session.Live : null;
        foreach (var (button, active) in new[]
        {
            (ChurchBackgroundButton, _output is not null && _session.IsTextHidden && !_session.IsBlackout),
            (BlackoutButton, _output is not null && _session.IsBlackout),
            (SceneImageButton, live?.Kind == CueKind.Image),
            (SceneVideoButton, live?.Kind == CueKind.Video),
            (SceneYouTubeButton, live?.Kind == CueKind.WindowCapture && live.Capture is { } capture && Services.YouTubeDiscovery.IsYouTube(capture)),
            (SceneBrowserButton, live?.Kind == CueKind.BrowserSource)
        }) System.Windows.Automation.AutomationProperties.SetItemStatus(button, active ? "En pantalla" : "");
    }

    private void InitializeScenes() => RefreshSceneChoices();

    private Cue? ActiveSceneVideo() => _output is null || _output.HasEnded ? null
        : _output.AudioCue is { Kind: CueKind.Video } sounding ? sounding
        : _session.Live is { Kind: CueKind.Video } live && _output.ActiveSurface.HasLoadedMedia ? live : null;

    private Cue? SceneMedia(CueKind kind) => kind == CueKind.Video
        ? ActiveSceneVideo() ?? (_session.Prepared is { Kind: CueKind.Video } video ? video : null)
        : _session.Prepared is { Kind: CueKind.Image } image ? image : _workspace.QuickSceneImage;

    private bool ShowSceneVideo(Cue cue)
    {
        var active = ActiveSceneVideo();
        if (active?.Id == cue.Id && !_session.IsBlackout && !_session.IsTextHidden
            && _output?.HasBackgroundMedia != true) return true;
        var paused = active?.Id == cue.Id && _output?.IsPaused == true;
        PrepareCue(CopyCue(cue));
        ProjectPrepared();
        if (_session.Live?.Id != cue.Id || _output is null) return false;
        if (paused)
        {
            _pendingCuePlayback.SetPaused(true);
            _output.SetPaused(true);
            _isPaused = true;
            UpdateSessionLabels();
        }
        return true;
    }

    private static CueKind? SceneMediaKind(object sender) => (sender as FrameworkElement)?.Tag switch
    {
        "Image" => CueKind.Image, "Video" => CueKind.Video, _ => null
    };

    private void SceneMediaClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || SceneMediaKind(sender) is not { } kind) return;
        if (kind == CueKind.Video && System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Shift)
        {
            if (SceneMedia(kind) is { } video && CheckMedia(video)) ShowSceneVideo(video);
            else SetStatus("Prepara un video en la PC para usar esta escena.");
            return;
        }
        if (SceneMedia(kind) is not { } cue) { ChooseSceneMediaClick(sender, e); return; }
        if (cue.Kind != kind || !CheckMedia(cue) || !RememberSceneMedia(cue)) return;
        PrepareCue(CopyCue(cue));
        ProjectPreparedFromInput();
    }

    private void ChooseSceneMediaClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || SceneMediaKind(sender) is not { } kind) return;
        var dialog = new OpenFileDialog
        {
            Title = kind == CueKind.Image ? "Elegir imagen de escena" : "Elegir video de escena",
            Multiselect = false,
            Filter = kind == CueKind.Image ? MediaLibrary.ImageFileFilter : MediaLibrary.VideoFileFilter
        };
        try
        {
            if (dialog.ShowDialog(this) != true) return;
            var cue = MediaLibrary.FromFile(dialog.FileName);
            if (cue.Kind != kind) { SetStatus("Elige un archivo del tipo de esta escena.", true); return; }
            if (!CheckMedia(cue) || !RememberSceneMedia(cue)) return;
            PrepareCue(cue);
            SetStatus("Escena guardada: " + cue.Title);
        }
        catch (Exception error) { SetStatus("No se pudo elegir la escena: " + error.Message, true); }
    }

    private bool RememberSceneMedia(Cue cue)
    {
        var previous = cue.Kind == CueKind.Image ? _workspace.QuickSceneImage : _workspace.QuickSceneVideo;
        if (cue.Kind == CueKind.Image) _workspace.QuickSceneImage = CopyCue(cue);
        else _workspace.QuickSceneVideo = CopyCue(cue);
        if (!SaveWorkspace())
        {
            if (cue.Kind == CueKind.Image) _workspace.QuickSceneImage = previous;
            else _workspace.QuickSceneVideo = previous;
            return false;
        }
        RefreshSceneChoices();
        return true;
    }

    private void RefreshSceneChoices()
    {
        if (!_ready) return;
        RefreshSceneMedia(SceneMedia(CueKind.Image), SceneImageThumb, SceneImageTitle, "Imagen");
        RefreshSceneMedia(SceneMedia(CueKind.Video), SceneVideoThumb, SceneVideoTitle, "Video");
        _refreshingScenes = true;
        try
        {
            RefreshTransitionPresetChoices();
            SceneTransitionPicker.SelectedItem = SceneTransitionPicker.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag as string == (_workspace.ActiveTransitionPresetId is { } id ? "preset:" + id : _workspace.Transitions.Kind));
        }
        finally { _refreshingScenes = false; }
        RefreshSceneYouTube();
        RefreshBrowserScene();
        RefreshConfiguredScenes();
    }

    private static void RefreshSceneMedia(Cue? cue, CueThumbnail thumbnail, TextBlock title, string empty)
    {
        thumbnail.Cue = cue;
        thumbnail.Visibility = cue is null ? Visibility.Collapsed : Visibility.Visible;
        title.Text = cue?.Title ?? empty;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.ToolTip = cue is null ? empty : cue.Title + "\n" + cue.FilePath;
    }

    private void SceneTransitionsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _refreshingScenes || SceneTransitionPicker.SelectedItem is not ComboBoxItem { Tag: string kind }) return;
        var preset = _workspace.TransitionPresets.FirstOrDefault(p => "preset:" + p.Id == kind);
        if (preset is null && kind is not ("Cut" or "Dissolve" or "Slide" or "Zoom" or "Move")) return;
        if (preset is null && _workspace.ActiveTransitionPresetId is null && kind == _workspace.Transitions.Kind) return;
        var previous = _workspace.Transitions;
        var previousActive = _workspace.ActiveTransitionPresetId;
        _workspace.Transitions = preset?.Options.Normalize() ?? previous with { Kind = kind };
        _workspace.ActiveTransitionPresetId = preset?.Id;
        if (!SaveWorkspace()) { _workspace.Transitions = previous; _workspace.ActiveTransitionPresetId = previousActive; RefreshSceneChoices(); return; }
        if (_output is not null) _output.Transitions = _workspace.Transitions;
        SetStatus("Transición del sistema guardada.");
    }
}
