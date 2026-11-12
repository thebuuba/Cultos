namespace Culto.MoveHost;

// All values use OBS' render timestamp clock, never callback arrival or wall time.
internal sealed class ObsFrameTiming(ulong outputReadyAfter)
{
    private ulong? _initialFrame, _transitionStartedAfter;

    public bool TryAcceptInitialFrame(ulong timestamp)
    {
        if (_initialFrame.HasValue || timestamp <= outputReadyAfter) return false;
        _initialFrame = timestamp;
        return true;
    }

    public void TransitionStarted(ulong nativeRenderTimestamp)
    {
        if (_initialFrame is not { } initial) throw new InvalidOperationException("Move needs a fresh outgoing frame before starting.");
        _transitionStartedAfter = Math.Max(initial, nativeRenderTimestamp);
    }

    public bool IsTransitionFrame(ulong timestamp) =>
        _transitionStartedAfter is { } barrier && timestamp > barrier;
}
