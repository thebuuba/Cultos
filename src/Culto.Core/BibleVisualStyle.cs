using System.Text.RegularExpressions;

namespace Culto.Core;

public sealed record BibleVisualStyle
{
    public string FontFamily { get; set; } = "Segoe UI";
    public string? ReferenceFontFamily { get; set; }
    public double FontSize { get; set; } = 76;
    public string TextColor { get; set; } = "#FFFFFF";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string Alignment { get; set; } = "Center";
    public string TextEffect { get; set; } = "None";
    public string BackgroundColor { get; set; } = "#101722";
    public string? BackgroundImagePath { get; set; }
    public double BackgroundDim { get; set; } = .35;
    public string Transition { get; set; } = "System";
    public string FavoriteTransitions { get; set; } = "";
    public int TransitionMilliseconds { get; set; } = 400;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool UsesTextTransition => BibleTextTransitionCatalog.TryGet(Transition, out _);

    public BibleVisualStyle Normalize() => this with
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI" : FontFamily.Trim(),
        ReferenceFontFamily = string.IsNullOrWhiteSpace(ReferenceFontFamily) ? null : ReferenceFontFamily.Trim(),
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 28, 120) : 76,
        TextColor = ValidColor(TextColor) ? TextColor.ToUpperInvariant() : "#FFFFFF",
        BackgroundColor = ValidColor(BackgroundColor) ? BackgroundColor.ToUpperInvariant() : "#101722",
        BackgroundDim = double.IsFinite(BackgroundDim) ? Math.Clamp(BackgroundDim, 0, .9) : .35,
        Alignment = Alignment is "Left" or "Right" ? Alignment : "Center",
        TextEffect = TextEffect is "Shadow" or "Glow" ? TextEffect : "None",
        Transition = Transition is "Cut" or "Dissolve" || UsesTextTransition ? Transition : "System",
        FavoriteTransitions = string.Join('|', (FavoriteTransitions ?? "")
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(id => BibleTextTransitionCatalog.TryGet(id, out _))
            .Distinct(StringComparer.Ordinal).Take(64)),
        TransitionMilliseconds = Math.Clamp(TransitionMilliseconds, 100, 2000)
    };

    public static bool ValidColor(string? value) => value is not null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");
}
