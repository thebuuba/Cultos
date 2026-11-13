using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool _refreshingScenes;

    private void InitializeScenes() => RefreshSceneChoices();

    private Cue? SceneMedia(CueKind kind) => _session.Prepared is { } prepared && prepared.Kind == kind
        ? prepared : kind == CueKind.Image ? _workspace.QuickSceneImage : _workspace.QuickSceneVideo;

    private static CueKind? SceneMediaKind(object sender) => (sender as FrameworkElement)?.Tag switch
    {
        "Image" => CueKind.Image, "Video" => CueKind.Video, _ => null
    };

    private void SceneMediaClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || SceneMediaKind(sender) is not { } kind) return;
        if (SceneMedia(kind) is not { } cue) { ChooseSceneMediaClick(sender, e); return; }
        if (cue.Kind != kind || !CheckMedia(cue) || !RememberSceneMedia(cue)) return;
        PrepareCue(CopyCue(cue));
        ProjectPrepared();
    }

    private void ChooseSceneMediaClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || SceneMediaKind(sender) is not { } kind) return;
        var dialog = new OpenFileDialog
        {
            Title = kind == CueKind.Image ? "Elegir imagen de escena" : "Elegir video de escena",
            Multiselect = false,
            Filter = kind == CueKind.Image ? "Imágenes|*.jpg;*.jpeg;*.png;*.bmp" : "Videos|*.mkv;*.mp4;*.mov;*.webm;*.avi;*.wmv"
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
            SceneTransitionPicker.SelectedItem = SceneTransitionPicker.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag as string == _workspace.Transitions.Kind);
        }
        finally { _refreshingScenes = false; }
        RefreshSceneYouTube();
    }

    private static void RefreshSceneMedia(Cue? cue, CueThumbnail thumbnail, TextBlock title, string empty)
    {
        thumbnail.Cue = cue;
        title.Text = cue?.Title ?? empty;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.ToolTip = cue is null ? empty : cue.Title + "\n" + cue.FilePath;
    }

    private void SceneTransitionsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _refreshingScenes || SceneTransitionPicker.SelectedItem is not ComboBoxItem { Tag: string kind }
            || kind is not ("Cut" or "Dissolve" or "Slide" or "Zoom" or "Move") || kind == _workspace.Transitions.Kind) return;
        var previous = _workspace.Transitions;
        _workspace.Transitions = previous with { Kind = kind };
        if (!SaveWorkspace()) { _workspace.Transitions = previous; RefreshSceneChoices(); return; }
        if (_output is not null) _output.Transitions = _workspace.Transitions;
        SetStatus("Transición del sistema guardada.");
    }
}
