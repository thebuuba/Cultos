using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    // This belongs to the current backdrop scene so advancing Logo frames keeps
    // its audio. It is reset by the next normal projection, never saved.
    private bool _preserveAudioForBackdrop;
    private bool _preserveAudioForNextCue;

    private void PlayFileFromInput()
    {
        if (_session.Prepared?.Kind != CueKind.Audio) { ProjectPreparedFromInput(); return; }
        var cue = _session.Prepared;
        if (_session.Live?.Kind == CueKind.PowerPoint && _pendingCuePlayback.IsPending)
        { SetStatus("Espera a que PowerPoint termine de entrar para cambiar solo el audio."); return; }
        if (!CheckMedia(cue) || !EnsureOutput()) return;
        try { _powerPoint?.SetAudioGain(0); }
        catch (Exception error) { SetStatus("No se pudo silenciar PowerPoint: " + error.Message, true); return; }
        _pendingCuePlayback.Cancel(); CancelPowerPointAudioFade();
        _videoListPlayback.Cancel(); _videoListAfter.IsChecked = false;
        _preserveAudioForBackdrop = _session.IsTextHidden || _session.IsBlackout;
        _output!.Transitions = CurrentTransitions;
        _output.PlayAudioKeepingVisual(CopyCue(cue));
        _independentScenePreview = true;
        RenderPreparedPreview();
        _isPaused = false;
        SchedulePlayedSearchReset(cue);
        UpdateBackgroundAudioState(); UpdateUsabilityState();
        SetStatus("Reproduciendo audio: " + cue.Title + ". La escena de Programa se conserva.");
    }

    private void ProjectPreparedFromInput()
    {
        if (Keyboard.Modifiers == ModifierKeys.Shift) ProjectPreparedPreservingOther();
        else ProjectPrepared();
    }

    private bool ProjectPreparedPreservingOther()
    {
        if (TryHandlePreparedScene(activate: !_handlingStreamDeck, preserve: true, out var projected)) return projected;
        if (_session.Prepared is not { } cue) { SetStatus("Selecciona primero un contenido."); return false; }
        if (cue.Kind is CueKind.Video or CueKind.Audio)
        {
            if (_output is null) { SetStatus("Muestra primero el logo, una imagen, texto o negro."); return false; }
            if (!_session.IsBlackout && !_session.IsTextHidden && _session.Live?.Kind is CueKind.Video or CueKind.Audio or CueKind.WindowCapture or CueKind.PowerPoint or CueKind.BrowserSource)
            { SetStatus("Muestra primero el logo, una imagen, texto o negro para conservar esa imagen."); return false; }
            if (!CheckMedia(cue)) return false;
            _pendingCuePlayback.Cancel(); CancelPowerPointAudioFade();
            _videoListPlayback.Cancel(); _videoListAfter.IsChecked = false;
            _preserveAudioForBackdrop = _session.IsTextHidden || _session.IsBlackout;
            _output.Transitions = CurrentTransitions;
            _output.PlayAudioKeepingVisual(CopyCue(cue));
            SchedulePlayedSearchReset(cue);
            _isPaused = false;
            UpdateBackgroundAudioState(); UpdateUsabilityState();
            SetStatus("Solo audio: " + cue.Title + ". La imagen continúa.");
            return true;
        }
        if (cue.Kind is not (CueKind.Bible or CueKind.Text or CueKind.Image))
        { SetStatus("Conservar audio está disponible para Biblia, texto e imagen. YouTube se controla en Chrome."); return false; }
        if (!CheckMedia(cue)) return false;
        ProjectPreparedCore(activate: !_handlingStreamDeck, preserve: true);
        return _session.Live?.Id == cue.Id;
    }

    private bool ShowLogoPreservingAudio()
    {
        var previous = _preserveAudioForBackdrop;
        _preserveAudioForBackdrop = true;
        if (StartLogoPresentation(_workspace.BackgroundImagePath)) return true;
        _preserveAudioForBackdrop = previous; return false;
    }

    private void ShowBlackPreservingAudio()
    {
        _preserveAudioForBackdrop = true;
        ShowBlackBackdropCore();
    }

    private void InitializeSplitMediaActions()
    {
        foreach (var (button, label, action) in new (Button, string, Action)[]
        {
            (ChurchBackgroundButton, "Mostrar logo conservando audio · Shift+clic", () => ShowLogoPreservingAudio()),
            (BlackoutButton, "Negro conservando audio · Shift+clic", ShowBlackPreservingAudio)
        })
        {
            button.ContextMenu ??= new ContextMenu();
            var item = new MenuItem { Header = label }; item.Click += (_, _) => action();
            button.ContextMenu.Items.Add(item);
        }
        foreach (var list in new[] { LibraryList, MediaList, PlanList })
            list.PreviewKeyDown += (_, e) =>
            {
                if (e.Handled || e.IsRepeat || e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.Shift
                    || list.SelectedItem is not Cue cue) return;
                e.Handled = true; PrepareCue(cue); ProjectPreparedPreservingOther();
            };
        var settings = new MenuItem { Header = "Configurar logo…" };
        settings.Click += (_, _) => LogoSettingsClick(this, new RoutedEventArgs());
        ChurchBackgroundButton.ContextMenu!.Items.Add(settings);
        foreach (var (button, kind) in new[] { (SceneImageButton, CueKind.Image), (SceneVideoButton, CueKind.Video) })
        {
            var item = new MenuItem { Header = kind == CueKind.Video ? "Solo audio · conservar imagen" : "Mostrar conservando audio" };
            item.Click += (_, _) => { if (SceneMedia(kind) is { } cue) { PrepareCue(CopyCue(cue)); ProjectPreparedPreservingOther(); } };
            button.ContextMenu!.Items.Add(item);
        }
    }
}
