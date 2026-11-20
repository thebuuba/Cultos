using System;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private CathedralAudioOutput? _cathedralAudio;
    private bool _cathedralRouteFailed;
    internal CathedralAudioOutput? CathedralAudio => _cathedralAudio;

    internal void BeginCathedralFade(TransitionOptions options)
    {
        if (options.AudioFadeEffect != "Cathedral" || options.CathedralIntensityPercent <= 0 || _preview || _cathedralAudio is null) return;
        _cathedralAudio.SetMasterGain(Math.Pow(_volume / 100d, 3) * _transportAudioGain);
        _cathedralAudio.BeginFadeOut(options.AudioFadeOutMilliseconds, options.CathedralTailMilliseconds, options.CathedralIntensityPercent);
    }

    internal CathedralAudioOutput? DetachCathedralTail()
    {
        if (_cathedralAudio is not { HasTail: true } tail) return null;
        _cathedralAudio = null; tail.EndInput(); return tail;
    }

    private void AttachCathedralAudio()
    {
        if (_preview || _cathedralRouteFailed || (!_smoothAudio && _transitions.AudioFadeEffect != "Cathedral") || _player is null) return;
        var output = new CathedralAudioOutput();
        try
        {
            output.Attach(_player, () => _vlc!.Clock); _cathedralAudio = output;
            _player.Volume = 100; _player.Mute = false;
        }
        catch (Exception error)
        {
            output.Dispose(); _cathedralRouteFailed = true;
            _player.Dispose();
            _player = new LibVLCSharp.Shared.MediaPlayer(_vlc!) { Volume = 0, Mute = true, EnableMouseInput = false, EnableKeyInput = false };
            PlaybackError?.Invoke("Catedral no está disponible; se mantiene el fundido normal. " + error.Message);
        }
    }

    private void RestoreDryAudio()
    {
        if (_player is null || _cathedralAudio is null) return;
        var failed = _cathedralAudio; var message = failed.LastError;
        _cathedralAudio = null; _cathedralRouteFailed = true;
        var old = _player; var position = old.Time; var paused = _pauseRequested;
        old.EncounteredError -= OnPlaybackError; old.Playing -= OnPlaying; old.EndReached -= OnEndReached; old.TimeChanged -= OnAudioOutputChanged;
        _playbackGeneration++; _player = null;
        try
        {
            old.Stop(); old.Dispose(); failed.Dispose();
            EnsurePlayer(); _player!.Media = _media;
            _started = false;
            if (!paused) { Start(); if (position > 0) _player.Time = position; }
            PlaybackError?.Invoke("Se recuperó el audio con fundido normal. Catedral: " + message);
        }
        catch (Exception error) { ReportError("No se pudo recuperar la salida de audio. " + error.Message); }
    }
}
