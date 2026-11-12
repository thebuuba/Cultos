namespace Culto.Core;

// A channel must belong to the presentation copy owned by the caller. Its setter
// addresses one media shape, never a process/session/master audio endpoint.
public sealed record PresentationAudioChannel(double OriginalVolume, Action<double> SetVolume);

/// <summary>
/// Preserves per-media authored volumes while applying one cancellable gain envelope.
/// This class has no timer, COM, audio-device or UI dependency. Call all methods from
/// the owning thread; an Office adapter must supply only its own shape setters on STA.
/// </summary>
public sealed class PresentationAudioEnvelope : IDisposable
{
    private readonly PresentationAudioChannel[] _channels;
    private long _generation;
    private double _from, _target;
    private int _duration, _elapsed;
    private bool _active, _disposed;

    public bool HasChannels => _channels.Length != 0;
    public double CurrentGain { get; private set; } = 1;

    public PresentationAudioEnvelope(IEnumerable<PresentationAudioChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        _channels = channels.ToArray();
        foreach (var channel in _channels)
        {
            ArgumentNullException.ThrowIfNull(channel);
            ArgumentNullException.ThrowIfNull(channel.SetVolume);
            ValidateGain(channel.OriginalVolume, nameof(channels));
        }
    }

    /// <summary>
    /// Replaces the old fade and starts from CurrentGain. Zero duration applies the
    /// target synchronously; Advance(token, 0) then reports its completion once.
    /// </summary>
    public long BeginFade(double targetGain, int durationMilliseconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateGain(targetGain, nameof(targetGain));
        ArgumentOutOfRangeException.ThrowIfNegative(durationMilliseconds);
        Cancel();
        var token = _generation;
        _from = CurrentGain; _target = targetGain;
        _duration = durationMilliseconds; _elapsed = 0; _active = true;
        if (_duration == 0) ApplyGain(targetGain, token);
        return token;
    }

    /// <summary>
    /// elapsedMilliseconds is measured since BeginFade, preferably with Stopwatch.
    /// Returns true once when this request completes; canceled/stale tokens never
    /// write volumes or report completion. Late/out-of-order ticks cannot reverse gain.
    /// </summary>
    public bool Advance(long token, int elapsedMilliseconds)
    {
        if (_disposed || !_active || token != _generation) return false;
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        _elapsed = Math.Max(_elapsed, elapsedMilliseconds);
        var progress = _duration == 0 ? 1 : Math.Min(1, _elapsed / (double)_duration);
        if (!ApplyGain(_from + (_target - _from) * progress, token)) return false;
        if (progress < 1) return false;
        _active = false;
        return true;
    }

    // Cancellation deliberately retains the current gain so a newer request can
    // reverse direction smoothly. Restoration is explicit and also occurs on Dispose.
    public void Cancel()
    {
        _generation++;
        _active = false;
    }

    public void SetGain(double gain)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateGain(gain, nameof(gain));
        Cancel();
        ApplyGain(gain, _generation);
    }

    public void RestoreOriginalVolumes()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel();
        RestoreChannels();
    }

    private bool ApplyGain(double gain, long token)
    {
        CurrentGain = gain;
        try
        {
            foreach (var channel in _channels)
            {
                if (_disposed || token != _generation) return false;
                channel.SetVolume(channel.OriginalVolume * gain);
            }
            return !_disposed && token == _generation;
        }
        catch
        {
            // A failing setter cannot leave its timer request armed. Do not cancel
            // a newer request if a COM callback reentered and replaced this one.
            if (token == _generation) Cancel();
            throw;
        }
    }

    private void RestoreChannels()
    {
        CurrentGain = 1;
        var errors = new List<Exception>();
        foreach (var channel in _channels)
        {
            try { channel.SetVolume(channel.OriginalVolume); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count != 0)
            throw new AggregateException("No se pudo restaurar el volumen de algunos medios de la presentación propia.", errors);
    }

    private static void ValidateGain(double gain, string name)
    {
        if (!double.IsFinite(gain) || gain < 0 || gain > 1)
            throw new ArgumentOutOfRangeException(name, "El volumen y la ganancia deben estar entre 0 y 1.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel(); _disposed = true;
        RestoreChannels();
    }
}
