namespace Culto.Core;

public sealed record LogoPlaybackOptions
{
    public double IntervalSeconds { get; set; } = 10;
    public int TransitionMilliseconds { get; set; } = 300;

    public LogoPlaybackOptions Normalize() => this with
    {
        IntervalSeconds = double.IsFinite(IntervalSeconds) ? Math.Clamp(IntervalSeconds, 1, 3600) : 10,
        TransitionMilliseconds = Math.Clamp(TransitionMilliseconds, 100, 3000)
    };
}
