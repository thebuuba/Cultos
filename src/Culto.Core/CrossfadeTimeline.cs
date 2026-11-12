namespace Culto.Core;

public readonly record struct CrossfadeSample(double VisualProgress, double OutgoingGain, double IncomingGain, bool Complete);

public static class CrossfadeTimeline
{
    // The caller starts one clock when incoming content is ready; none of the envelopes wait for another.
    public static CrossfadeSample Sample(double elapsedMilliseconds, int visualMs, int outMs, int inMs)
    {
        if (!double.IsFinite(elapsedMilliseconds)) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
        ArgumentOutOfRangeException.ThrowIfNegative(visualMs);
        ArgumentOutOfRangeException.ThrowIfNegative(outMs);
        ArgumentOutOfRangeException.ThrowIfNegative(inMs);
        var elapsed = Math.Max(0, elapsedMilliseconds);
        double Progress(int milliseconds) => milliseconds == 0 ? 1 : Math.Min(1, elapsed / milliseconds);
        return new CrossfadeSample(Progress(visualMs), 1 - Progress(outMs), Progress(inMs),
            elapsed >= Math.Max(visualMs, Math.Max(outMs, inMs)));
    }
}
