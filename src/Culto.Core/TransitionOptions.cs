namespace Culto.Core;

public sealed record TransitionOptions
{
    public string Kind { get; set; } = "Dissolve";
    public int DurationMilliseconds { get; set; } = 300;
    public int AudioFadeOutMilliseconds { get; set; } = 250;
    public int AudioFadeInMilliseconds { get; set; } = 250;
    public string AudioFadeEffect { get; set; } = "Normal";
    public int CathedralIntensityPercent { get; set; } = 25;
    public int CathedralTailMilliseconds { get; set; } = 2500;
    public MoveOptions Move { get; set; } = new();
    // Only the old factory tuple is upgraded; later deliberate choices retain their timing.
    public TransitionOptions UpgradeStoredDefaults(int defaultsVersion) => defaultsVersion < 1
        && Kind == "Dissolve" && DurationMilliseconds == 500
        && AudioFadeOutMilliseconds == 1500 && AudioFadeInMilliseconds == 1500
        ? new TransitionOptions() : Normalize();
    public TransitionOptions Normalize() => this with
    {
        Kind = Kind is "Cut" or "Slide" or "Zoom" or "Move" ? Kind : "Dissolve",
        DurationMilliseconds = Math.Clamp(DurationMilliseconds, 100, 3000),
        AudioFadeOutMilliseconds = Math.Clamp(AudioFadeOutMilliseconds, 0, 5000),
        AudioFadeInMilliseconds = Math.Clamp(AudioFadeInMilliseconds, 0, 5000),
        AudioFadeEffect = AudioFadeEffect == "Cathedral" ? "Cathedral" : "Normal",
        CathedralIntensityPercent = Math.Clamp(CathedralIntensityPercent, 0, 60),
        CathedralTailMilliseconds = Math.Clamp(CathedralTailMilliseconds, 500, 8000),
        Move = (Move ?? new()).Normalize()
    };
}
