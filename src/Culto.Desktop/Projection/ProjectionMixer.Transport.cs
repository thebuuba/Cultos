using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace Culto.Desktop.Projection;

internal sealed partial class ProjectionMixer
{
    private DispatcherTimer? _transportTimer;
    private readonly Stopwatch _transportClock = new();
    private double _transportGain = 1, _transportFrom, _transportTarget, _transportMilliseconds;
    private bool? _transportPaused;
    private Action? _transportCompleted;

    private void CancelTransportFade()
    {
        _transportTimer?.Stop();
        _transportCompleted = null;
        _transportPaused = null;
        _transportGain = 1;
    }

    private void FadeTransport(double target, int milliseconds, Action? completed = null)
    {
        _transportTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Normal,
            (_, _) => AdvanceTransportFade(), Dispatcher);
        _transportFrom = _transportGain;
        _transportTarget = target;
        _transportMilliseconds = milliseconds;
        _transportCompleted = completed;
        _transportClock.Restart();
        _transportTimer.Start();
    }

    private void AdvanceTransportFade()
    {
        if (_disposed) { CancelTransportFade(); return; }
        var progress = Math.Clamp(_transportClock.Elapsed.TotalMilliseconds / _transportMilliseconds, 0, 1);
        // Ease both ends of the envelope to avoid a sudden change of slope.
        progress = progress * progress * (3 - 2 * progress);
        _transportGain = _transportFrom + (_transportTarget - _transportFrom) * progress;
        ApplyVolumes();
        if (progress < 1) return;
        _transportTimer!.Stop();
        var completed = _transportCompleted;
        _transportCompleted = null;
        completed?.Invoke();
    }

    public void SetPausedWithFade(bool paused)
    {
        if (_disposed) return;
        if (_loading is not null || _queued is not null || !ActiveSurface.HasLoadedMedia)
        { SetPaused(paused); return; }
        if (paused && IsPaused) return;
        var wasPaused = IsPaused;
        _transportTimer?.Stop();
        _transportCompleted = null;
        _transportPaused = paused;
        if (paused)
        {
            FadeTransport(0, 420, () => { SetPausedCore(true); _transportPaused = null; });
        }
        else
        {
            if (wasPaused && !ActiveSurface.IsPlaying) _transportGain = 0;
            SetPausedCore(false);
            FadeTransport(1, 480, () => _transportPaused = null);
        }
    }

    public void SeekWithFade(double position)
    {
        if (_disposed || !double.IsFinite(position) || _queued is not null || _loading is not null
            || !HasBackgroundMedia && (_blackout || _hidden)) return;
        if (IsPaused || !ActiveSurface.IsPlaying)
        {
            var paused = IsPaused;
            SetPaused(paused);
            SeekCore(position);
            return;
        }
        _transportPaused = null;
        FadeTransport(0, 220, () =>
        {
            SeekCore(position);
            FadeTransport(1, 380);
        });
    }
}
