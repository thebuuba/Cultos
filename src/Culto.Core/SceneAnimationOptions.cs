namespace Culto.Core;

public sealed record SceneAnimationOptions
{
    public string Entrance { get; set; } = "None";
    public string Exit { get; set; } = "None";
    public int DurationMilliseconds { get; set; } = 600;
    public int DelayMilliseconds { get; set; }
    public int HoldMilliseconds { get; set; }

    public void Validate()
    {
        if (!IsEffect(Entrance) || !IsEffect(Exit))
            throw new ArgumentException("El efecto de animación no es válido.");
        if (DurationMilliseconds is < 100 or > 3000 || DelayMilliseconds is < 0 or > 10000
            || HoldMilliseconds is < 0 or > 3600000)
            throw new ArgumentException("Revisa la duración, demora y permanencia de la animación.");
    }

    private static bool IsEffect(string? effect) => effect is "None" or "Fade" or "Rise" or "SlideLeft" or "Zoom";
}

public readonly record struct SceneAnimationSample(double Opacity, double OffsetX, double OffsetY, double Scale, bool Complete);

public static class SceneAnimationTimeline
{
    public static SceneAnimationSample SampleEntry(SceneAnimationOptions options, double elapsedMilliseconds)
    {
        Validate(options, elapsedMilliseconds);
        var elapsed = Math.Max(0, elapsedMilliseconds) - options.DelayMilliseconds;
        if (elapsed < 0) return Effect(options.Entrance, 1, false) with { Opacity = 0 };
        var entranceDuration = options.Entrance == "None" ? 0 : options.DurationMilliseconds;
        if (elapsed < entranceDuration)
            return Effect(options.Entrance, 1 - Ease(elapsed / entranceDuration), false);
        if (options.HoldMilliseconds > 0 && options.Exit != "None")
        {
            var exitElapsed = elapsed - entranceDuration - options.HoldMilliseconds;
            return exitElapsed < 0 ? new(1, 0, 0, 1, false) : SampleExit(options, exitElapsed);
        }
        return new(1, 0, 0, 1, true);
    }

    public static SceneAnimationSample SampleExit(SceneAnimationOptions options, double elapsedMilliseconds)
    {
        Validate(options, elapsedMilliseconds);
        if (options.Exit == "None") return new(1, 0, 0, 1, true);
        var progress = Math.Clamp(elapsedMilliseconds / options.DurationMilliseconds, 0, 1);
        return Effect(options.Exit, Ease(progress), progress >= 1);
    }

    private static double Ease(double progress) => 1 - Math.Pow(1 - progress, 3);

    private static SceneAnimationSample Effect(string effect, double amount, bool complete) => new(
        effect == "None" ? 1 : 1 - amount,
        effect == "SlideLeft" ? -.12 * amount : 0,
        effect == "Rise" ? .12 * amount : 0,
        effect == "Zoom" ? 1 - .1 * amount : 1,
        complete);

    private static void Validate(SceneAnimationOptions options, double elapsedMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!double.IsFinite(elapsedMilliseconds)) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
    }
}
