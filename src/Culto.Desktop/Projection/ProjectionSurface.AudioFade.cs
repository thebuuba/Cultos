using System;
using System.Diagnostics;
using System.Windows.Threading;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private sealed record Presentation(Cue? Cue, bool Blackout, bool HideText, bool Dissolve, bool PauseAfter = false)
    {
        public BibleVisualStyle BibleStyle { get; init; } = new();
        public TransitionOptions Transitions { get; init; } = new();
        public bool AnimateAudio { get; init; }
    }
    private readonly DispatcherTimer _audioTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _audioClock = new();
    private Presentation? _pendingPresentation;
    private Action? _audioCompleted;
    private double _audioGain, _audioFrom, _audioTo, _audioMilliseconds;
    private bool _awaitingAudioStart, _ended;
    private int _audioGeneration, _playbackGeneration;

    // This is playback intent, including loading and a queued media change.
    public bool IsPaused => _mixer?.IsPaused ?? (_disposed || _suppressed || _pauseRequested
        || _pendingPresentation is { Blackout: true } or { HideText: true } or { PauseAfter: true });

    private void InitializeAudioFade() => _audioTimer.Tick += (_, _) =>
    {
        var generation = _audioGeneration;
        var progress = Math.Clamp(_audioClock.Elapsed.TotalMilliseconds / _audioMilliseconds, 0, 1);
        _audioGain = _audioFrom + (_audioTo - _audioFrom) * progress;
        ApplyAudioVolume();
        if (progress < 1) return;
        _audioTimer.Stop();
        var completed = _audioCompleted;
        _audioCompleted = null;
        if (generation == _audioGeneration && !_disposed) completed?.Invoke();
    };

    private void ApplyAudioVolume()
    {
        ApplyCapturedAudioVolume();
        if (_player is null || _disposed) return;
        var silent = _preview || _suppressed || _pauseRequested || _awaitingAudioStart;
        var volume = silent ? 0 : NativeFadeVolume(_volume, _audioGain);
        // A preloaded surface stays natively muted even if VLC creates its
        // audio output after Playing, until the shared blend gives it gain.
        silent |= volume == 0;
        if (_player.Volume != volume) _player.Volume = volume;
        if (_player.Mute != silent) _player.Mute = silent;
    }

    // VLC DirectSound maps its volume percentage to amplitude cubically.
    // Compensate only the envelope, preserving the operator's master volume:
    // a half-gain fade must emit half amplitude, not one eighth.
    internal static int NativeFadeVolume(int masterVolume, double gain) =>
        (int)Math.Round(Math.Clamp(masterVolume, 0, 100) * Math.Cbrt(Math.Clamp(gain, 0, 1)));

    private void OnAudioOutputChanged(object? sender, EventArgs args)
    {
        var generation = System.Threading.Volatile.Read(ref _playbackGeneration);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // VLC can create its audio output after Playing. A zero-duration fade
            // has already finished then; apply the current gain when native audio arrives.
            if (!_disposed && generation == _playbackGeneration) ApplyAudioVolume();
        }));
    }

    private void CancelAudioFade(bool silence = false)
    {
        _audioGeneration++;
        _audioTimer.Stop();
        _audioCompleted = null;
        if (!silence) return;
        _awaitingAudioStart = false;
        _audioGain = 0;
        ApplyAudioVolume();
    }

    private void FadeAudio(double target, double milliseconds, Action? completed = null)
    {
        CancelAudioFade();
        _audioFrom = _audioGain;
        _audioTo = target;
        _audioMilliseconds = milliseconds;
        _audioCompleted = completed;
        if (milliseconds <= 0)
        {
            _audioGain = target; _audioCompleted = null; ApplyAudioVolume(); completed?.Invoke(); return;
        }
        _audioClock.Restart();
        _audioTimer.Start();
    }

    private void Present(Presentation presentation)
    {
        if (_disposed) return;
        _pendingPresentation = null;
        CancelAudioFade();
        var cue = presentation.Cue;
        var key = cue?.Kind is CueKind.Video or CueKind.Audio ? $"{cue.Id}|{cue.Kind}|{cue.FilePath}" : null;
        var suppress = presentation.Blackout || presentation.HideText;
        var fadeOutgoing = suppress ? presentation.AnimateAudio : key != _mediaKey || presentation.PauseAfter;
        if (!_preview && !_suppressed && !_pauseRequested && fadeOutgoing
            && _player?.IsPlaying == true && _audioGain > 0)
        {
            // Keep one decoder alive during the outgoing envelope; the newest request wins.
            _pendingPresentation = presentation;
            FadeAudio(0, presentation.Transitions.AudioFadeOutMilliseconds, () =>
            {
                var pending = _pendingPresentation;
                _pendingPresentation = null;
                if (pending is not null) ApplyPresentation(pending);
            });
            return;
        }
        ApplyPresentation(presentation);
    }

    private void ApplyPresentation(Presentation presentation, bool paused = false)
    {
        _presentedBibleStyle = presentation.BibleStyle;
        _presentedTransitions = presentation.Transitions;
        if (presentation.Dissolve && !paused)
            TransitionToCore(presentation);
        else ShowCueCore(presentation.Cue, presentation.Blackout, presentation.HideText, paused || presentation.PauseAfter);
        // Re-selecting the playing cue can cancel an outgoing fade without restarting it.
        if (!_preview && !_suppressed && !_pauseRequested && !_awaitingAudioStart
            && _player?.IsPlaying == true && _audioGain < 1)
            FadeAudio(1, _presentedTransitions.AudioFadeInMilliseconds);
    }

    private void BeginAudioPlayback()
    {
        CancelAudioFade(silence: true);
        _awaitingAudioStart = !_preview;
        ApplyAudioVolume();
    }
}
