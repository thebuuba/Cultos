using System;
using System.Diagnostics;
using System.Windows.Threading;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private sealed record Presentation(Cue? Cue, bool Blackout, bool HideText, bool Dissolve);
    private readonly DispatcherTimer _audioTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _audioClock = new();
    private Presentation? _pendingPresentation;
    private Action? _audioCompleted;
    private double _audioGain, _audioFrom, _audioTo, _audioMilliseconds;
    private bool _awaitingAudioStart, _ended;
    private int _audioGeneration, _playbackGeneration;

    // This is playback intent, including loading and a queued media change.
    public bool IsPaused => _disposed || _suppressed || _pauseRequested;

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
        if (_player is null || _disposed) return;
        var silent = _preview || _suppressed || _pauseRequested || _awaitingAudioStart;
        _player.Volume = silent ? 0 : (int)Math.Round(_volume * _audioGain);
        _player.Mute = silent;
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
        if (!_preview && !presentation.Blackout && !presentation.HideText && !_suppressed
            && !_pauseRequested && key != _mediaKey && _player?.IsPlaying == true && _audioGain > 0)
        {
            // Keep one decoder alive during the outgoing envelope; the newest request wins.
            _pendingPresentation = presentation;
            FadeAudio(0, 180, () =>
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
        if (presentation.Dissolve && !paused && presentation.Cue is not null)
            TransitionToCore(presentation.Cue);
        else ShowCueCore(presentation.Cue, presentation.Blackout, presentation.HideText, paused);
        // Re-selecting the playing cue can cancel an outgoing fade without restarting it.
        if (!_preview && !_suppressed && !_pauseRequested && !_awaitingAudioStart
            && _player?.IsPlaying == true && _audioGain < 1)
            FadeAudio(1, 350);
    }

    private void BeginAudioPlayback()
    {
        CancelAudioFade(silence: true);
        _awaitingAudioStart = !_preview;
        ApplyAudioVolume();
    }
}
