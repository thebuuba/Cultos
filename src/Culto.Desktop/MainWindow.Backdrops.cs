using System.Windows;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void ChurchBackgroundClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift) ShowLogoPreservingAudio();
        else ShowLogo();
    }
    private void BlackBackgroundClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift) ShowBlackPreservingAudio();
        else ShowBlackBackdrop();
    }

    private void ShowBlackBackdrop()
    {
        _preserveAudioForBackdrop = false;
        ShowBlackBackdropCore();
    }
    private void ShowBlackBackdropCore()
    {
        if (!EnsureOutput()) return;
        if (_session.IsBlackout) _session.RestoreContent();
        else _session.ShowBlackout();
        ApplyBackdropTransition();
    }

    private void ApplyBackdropTransition()
    {
        _youTubeLinkProjection?.Cancel();
        _preserveAudioForNextCue = false;
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
        var transitions = CurrentTransitions;
        if (_sceneTransitionOverride is null && _logoSlides.IsRunning && _session.IsTextHidden && !_session.IsBlackout)
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
        if (_preserveAudioForBackdrop)
            _output.TransitionVisual(WithYouTubeLayout(_session.Live), _session.IsBlackout, _session.IsTextHidden);
        else _output.TransitionToBackdrop(WithYouTubeLayout(_session.Live), _session.IsBlackout, _session.IsTextHidden);
        _logoAwaitingOutput = false;
        _isPaused = _output.HasBackgroundMedia ? _output.IsPaused
            : _session.Live?.Kind != CueKind.WindowCapture || _session.IsBlackout || _session.IsTextHidden;
        UpdateSessionLabels();
        SetStatus(presentationError ?? (_session.IsBlackout ? "Fondo negro" : _session.IsTextHidden ? "Logo"
            : _isPaused ? "Contenido restaurado; reproducción pausada." : "Contenido restaurado."), presentationError is not null);
    }
}
