using System.Diagnostics;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly DispatcherTimer _powerPointAudioTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _powerPointAudioClock = new();
    private PowerPointPresentation? _audioFadeOwner;
    private long _powerPointAudioToken;
    private Action? _afterPowerPointAudio;
    private bool _powerPointAudioReady;
    private double _powerPointAudioTarget;
    private readonly PendingCuePlayback _pendingCuePlayback = new();
    private (PowerPointPresentation Owner, Queue<int> Steps)? _pendingPowerPointNavigation;

    private void QueueLiveCue()
    {
        var token = _pendingCuePlayback.Begin(_workspace.Transitions, _workspace.BibleStyle);
        _isPaused = false;
        FadePowerPointOut(() => CommitPendingCue(token), _pendingCuePlayback.Transitions.AudioFadeOutMilliseconds);
    }

    private void CommitPendingCue(long token)
    {
        if (!_pendingCuePlayback.Matches(token) || _pendingCuePlayback.IsPresented) return;
        ShowLiveCueCore(_pendingCuePlayback.Transitions, _pendingCuePlayback.BibleStyle);
        if (!_pendingCuePlayback.MarkPresented(token)) return;
        _isPaused = _session.IsBlackout || _session.IsTextHidden || _pendingCuePlayback.Intent.IsPaused;
        if (_session.Live?.Kind == CueKind.PowerPoint) return; // Keep intent until the delayed native Start.
        if (_pendingCuePlayback.Complete(token) is not { } intent) return;
        if (intent.Restart) _output?.Restart();
        if (!_preserveAudioForBackdrop && (!_logoSlides.IsRunning || !_session.IsTextHidden || _session.IsBlackout)) _output?.SetPaused(_isPaused);
        UpdateSessionLabels();
    }

    private bool TogglePendingCuePause()
    {
        if (!_pendingCuePlayback.IsPending || _session.Live?.Kind is not (CueKind.PowerPoint or CueKind.Video or CueKind.Audio)) return false;
        if (_session.IsBlackout || _session.IsTextHidden) return true;
        _pendingCuePlayback.SetPaused(!_pendingCuePlayback.Intent.IsPaused);
        _isPaused = _pendingCuePlayback.Intent.IsPaused;
        if (_session.Live.Kind == CueKind.PowerPoint) PowerPointPauseButton.Content = _isPaused ? "Continuar" : "Pausar";
        else UpdatePlayButton(PauseButton, _isPaused, false);
        return true;
    }

    private bool RestartPendingCue()
    {
        if (!_pendingCuePlayback.IsPending || _session.Live?.Kind is not (CueKind.PowerPoint or CueKind.Video or CueKind.Audio)) return false;
        if (_session.IsBlackout || _session.IsTextHidden) return true;
        _pendingCuePlayback.RequestRestart(); _isPaused = false;
        if (_session.Live.Kind == CueKind.PowerPoint) PowerPointPauseButton.Content = "Pausar";
        else UpdatePlayButton(PauseButton, false, false);
        return true;
    }

    private void CancelPowerPointAudioFade()
    {
        _pendingPowerPointNavigation = null;
        _powerPointAudioTimer.Stop();
        _audioFadeOwner?.CancelAudioFade();
        _audioFadeOwner = null;
        _afterPowerPointAudio = null;
    }

    private void FadePowerPointOut(Action completed, int? milliseconds = null)
        => FadePowerPointAudio(0, milliseconds ?? _workspace.Transitions.AudioFadeOutMilliseconds, completed);

    private void FadePowerPointIn(int? milliseconds = null)
        => FadePowerPointAudio(1, milliseconds ?? _workspace.Transitions.AudioFadeInMilliseconds);

    private void FadePowerPointAudio(double target, int milliseconds, Action? completed = null)
    {
        CancelPowerPointAudioFade();
        var presentation = _powerPoint;
        if (presentation?.IsRunning != true) { completed?.Invoke(); return; }
        try
        {
            if (target == 0 && !presentation.HasAudioToFade)
            {
                presentation.SetAudioGain(0);
                completed?.Invoke();
                return;
            }
            if (!_powerPointAudioReady)
            {
                _powerPointAudioReady = true;
                _powerPointAudioTimer.Tick += (_, _) => AdvancePowerPointAudio();
            }
            _audioFadeOwner = presentation;
            _afterPowerPointAudio = completed;
            _powerPointAudioTarget = target;
            _powerPointAudioToken = presentation.BeginAudioFade(target, Math.Max(0, milliseconds));
            _powerPointAudioClock.Restart();
            _powerPointAudioTimer.Start();
            AdvancePowerPointAudio();
        }
        catch (Exception error) { PowerPointAudioFailed(error, target, completed); }
    }

    private void AdvancePowerPointAudio()
    {
        var owner = _audioFadeOwner;
        if (owner is null || !ReferenceEquals(owner, _powerPoint))
        { CancelPowerPointAudioFade(); return; }
        var token = _powerPointAudioToken;
        var target = _powerPointAudioTarget;
        var completed = _afterPowerPointAudio;
        try
        {
            if (owner.IsRunning && !owner.AdvanceAudioFade(token, (int)Math.Min(int.MaxValue, _powerPointAudioClock.ElapsedMilliseconds))) return;
            if (!ReferenceEquals(owner, _audioFadeOwner) || token != _powerPointAudioToken) return;
            CancelPowerPointAudioFade();
            completed?.Invoke();
        }
        catch (Exception error)
        {
            if (ReferenceEquals(owner, _audioFadeOwner) && token == _powerPointAudioToken)
                PowerPointAudioFailed(error, target, completed);
            else if (ReferenceEquals(owner, _powerPoint) && _audioFadeOwner is null && token == _powerPointAudioToken)
                PowerPointAudioFailed(error, target, null); // Completion itself failed; do not invoke it twice.
            else SetStatus("PowerPoint: " + error.Message, true);
        }
    }

    private void PowerPointAudioFailed(Exception error, double target, Action? completed)
    {
        CancelPowerPointAudioFade();
        // Close only our native show if its media setters fail, then permit the
        // requested replacement/blackout to proceed. Never change Office master audio.
        StopPowerPointOutput(keepPending: target == 0, keepCueRequest: target == 0);
        if (target == 0) completed?.Invoke();
        SetStatus("PowerPoint: no se pudo ajustar el fundido de audio. " + error.Message, true);
    }

    private bool AdvancePowerPointWithFade(int direction)
    {
        var presentation = _powerPoint;
        if (_pendingCuePlayback.IsPending || presentation?.IsRunning != true || _session.IsBlackout || _session.IsTextHidden) return false;
        if (_pendingPowerPointNavigation is { } pending && ReferenceEquals(pending.Owner, presentation))
        {
            pending.Steps.Enqueue(direction);
            return true;
        }
        var steps = new Queue<int>(); steps.Enqueue(direction);
        var completed = false;
        var transitions = _workspace.Transitions.Normalize();
        FadePowerPointOut(() =>
        {
            completed = true;
            if (!ReferenceEquals(_powerPoint, presentation)) return;
            var advanced = false;
            while (steps.TryDequeue(out var step)) advanced |= presentation.Advance(step);
            if (!advanced)
            { FadePowerPointIn(transitions.AudioFadeInMilliseconds); SetStatus("Sin más pasos en esa dirección."); return; }
            _isPaused = false;
            presentation.SetPaused(false);
            PowerPointPauseButton.Content = "Pausar";
            UpdatePowerPointPosition();
            FadePowerPointIn(transitions.AudioFadeInMilliseconds);
        }, transitions.AudioFadeOutMilliseconds);
        if (!completed) _pendingPowerPointNavigation = (presentation, steps);
        return true;
    }
}
