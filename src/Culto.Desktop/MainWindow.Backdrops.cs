using System.Windows;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void ChurchBackgroundClick(object sender, RoutedEventArgs e)
        => ShowLogo();
    private void BlackBackgroundClick(object sender, RoutedEventArgs e) => ShowBlackBackdrop();

    private void ShowBlackBackdrop()
    {
        if (!EnsureOutput()) return;
        if (_session.IsBlackout) _session.RestoreContent();
        else _session.ShowBlackout();
        ApplyBackdropTransition();
    }

    private void ApplyBackdropTransition()
    {
        if (_pendingCuePlayback.IsPending)
        {
            _pendingCuePlayback.SetPaused(true);
            _isPaused = true;
            if (!_pendingCuePlayback.IsPresented)
            {
                var token = _pendingCuePlayback.Token;
                FadePowerPointOut(() => CommitPendingCue(token), _pendingCuePlayback.Transitions.AudioFadeOutMilliseconds);
                return;
            }
        }
        var transitions = _workspace.Transitions.Normalize();
        if (_logoSlides.IsRunning && _session.IsTextHidden && !_session.IsBlackout)
            transitions = transitions with { Kind = "Dissolve", DurationMilliseconds = _workspace.LogoPlayback.Normalize().TransitionMilliseconds };
        FadePowerPointOut(() => ApplyBackdropTransitionCore(transitions), transitions.AudioFadeOutMilliseconds);
    }

    private void ApplyBackdropTransitionCore(TransitionOptions transitions)
    {
        string? presentationError = null;
        if (_powerPoint?.IsRunning == true)
        {
            // Hide the native window before COM playback calls, which can be rejected
            // while PowerPoint is busy. A rejected pause must never defeat blackout.
            RoutePowerPointOutput();
            try { _powerPoint.SetPaused(true); PowerPointPauseButton.Content = "Continuar"; }
            catch (Exception error)
            {
                StopPowerPointOutput(); presentationError = "PowerPoint se cerró al detener la reproducción: " + error.Message;
            }
        }
        _output!.BackgroundImagePath = CurrentBackgroundPath;
        _output.Transitions = transitions;
        _output.TransitionToBackdrop(WithYouTubeLayout(_session.Live), _session.IsBlackout, _session.IsTextHidden);
        _logoAwaitingOutput = false;
        _isPaused = _output.HasBackgroundMedia ? _output.IsPaused
            : _session.Live?.Kind != CueKind.WindowCapture || _session.IsBlackout || _session.IsTextHidden;
        UpdateSessionLabels();
        SetStatus(presentationError ?? (_session.IsBlackout ? "Fondo negro" : _session.IsTextHidden ? "Logo"
            : _isPaused ? "Contenido restaurado; reproducción pausada." : "Contenido restaurado."), presentationError is not null);
    }

    private void ChangeChurchBackgroundClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Fondo de la iglesia", CheckFileExists = true,
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!CheckMedia(new Cue { Kind = CueKind.Image, Title = "Fondo iglesia", FilePath = dialog.FileName })) return;
        SetChurchBackground(dialog.FileName);
    }

    private void ClearChurchBackgroundClick(object sender, RoutedEventArgs e) => SetChurchBackground(null);

    private void SetChurchBackground(string? path)
    {
        _workspace.BackgroundImagePath = path;
        if (path is not null && !_workspace.BackgroundImages.Contains(path, StringComparer.OrdinalIgnoreCase))
            _workspace.BackgroundImages.Add(path);
        SaveWorkspace();
        if (_output is not null)
        {
            _output.BackgroundImagePath = path;
            if (_session.IsTextHidden && !_session.IsBlackout) ApplyBackdropTransition();
        }
        SetStatus(path is null ? "Fondo sencillo seleccionado." : "Fondo de la iglesia guardado.");
    }
}
