namespace Culto.Core;

public sealed record TransitionOptions
{
    public string Kind { get; set; } = "Dissolve";
    public int DurationMilliseconds { get; set; } = 500;
    public int AudioFadeOutMilliseconds { get; set; } = 1500;
    public int AudioFadeInMilliseconds { get; set; } = 1500;
    public TransitionOptions Normalize() => this with
    {
        Kind = Kind is "Cut" or "Slide" or "Zoom" or "Move" ? Kind : "Dissolve",
        DurationMilliseconds = Math.Clamp(DurationMilliseconds, 100, 3000),
        AudioFadeOutMilliseconds = Math.Clamp(AudioFadeOutMilliseconds, 0, 5000),
        AudioFadeInMilliseconds = Math.Clamp(AudioFadeInMilliseconds, 0, 5000)
    };
}
