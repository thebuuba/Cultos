namespace Culto.Core;

// Values follow Move 3.2.1's position flags and easing enums.
public sealed record MoveItemOptions
{
    public int Position { get; init; } = 6;
    public double Zoom { get; init; }
    public double Curve { get; init; }
    public int Easing { get; init; } = 3;
    public int Function { get; init; } = 2;
    public MoveItemOptions Normalize(int defaultPosition) => this with
    {
        Position = Position is 0 or 1 or 2 or 6 or 10 or 18 or 22 or 26 or 34 or 38 or 42 or 68 or 72 or 80 or 96 ? Position : defaultPosition,
        Zoom = double.IsFinite(Zoom) ? Math.Clamp(Zoom, 0, 200) : 0,
        Curve = double.IsFinite(Curve) ? Math.Clamp(Curve, -2, 2) : 0,
        Easing = Easing is >= 0 and <= 3 ? Easing : 3,
        Function = Function is >= 1 and <= 10 ? Function : 2
    };
}
public sealed record MoveOptions
{
    public MoveItemOptions In { get; init; } = new();
    public MoveItemOptions Out { get; init; } = new() { Position = 10 };
    public MoveOptions Normalize() => this with
    { In = (In ?? new()).Normalize(6), Out = (Out ?? new() { Position = 10 }).Normalize(10) };
}
public sealed record TransitionPreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Nueva transición";
    public TransitionOptions Options { get; init; } = new() { Kind = "Move" };
}
