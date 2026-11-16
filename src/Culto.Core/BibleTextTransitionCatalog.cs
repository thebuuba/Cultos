namespace Culto.Core;

public enum BibleTextMotion
{
    Slide, Wipe, Radial, Zoom, Rotate, Skew, Bounce, Spring, Blur, Wave, Fold, Unroll
}

public enum BibleTextScope
{
    Passage, Lines, Words
}

public record BibleTextTransitionPreset(
    string Id, string Name, string Family, BibleTextMotion Motion, BibleTextScope Scope, int Variant);

public static class BibleTextTransitionCatalog
{
    public static IReadOnlyList<BibleTextTransitionPreset> All { get; } = CreateAll();

    private static readonly IReadOnlyDictionary<string, BibleTextTransitionPreset> ById =
        All.ToDictionary(preset => preset.Id, StringComparer.Ordinal);

    public static bool TryGet(string? id, out BibleTextTransitionPreset preset)
    {
        preset = null!;
        return id is not null && ById.TryGetValue(id, out preset!);
    }

    private static IReadOnlyList<BibleTextTransitionPreset> CreateAll()
    {
        string[] directions =
        [
            "Desde abajo", "Desde arriba", "Desde la izquierda", "Desde la derecha",
            "Desde abajo a la izquierda", "Desde abajo a la derecha",
            "Desde arriba a la izquierda", "Desde arriba a la derecha"
        ];
        (BibleTextMotion Motion, string Family, string[] Variants)[] families =
        [
            (BibleTextMotion.Slide, "Deslizamiento", directions),
            (BibleTextMotion.Wipe, "Barrido", directions),
            (BibleTextMotion.Radial, "Apertura radial",
            [
                "Desde el centro", "Desde la izquierda", "Desde la derecha", "Desde arriba",
                "Desde abajo", "Desde arriba a la izquierda", "Desde arriba a la derecha",
                "Desde abajo a la derecha"
            ]),
            (BibleTextMotion.Zoom, "Acercamiento",
            [
                "Acercamiento suave", "Acercamiento amplio", "Alejamiento suave", "Alejamiento amplio",
                "Desde la izquierda", "Desde la derecha", "Desde arriba", "Desde abajo"
            ]),
            (BibleTextMotion.Rotate, "Rotación",
            [
                "Giro de −12° al centro", "Giro de −6° al centro", "Giro de 6° al centro", "Giro de 12° al centro",
                "Giro de −12° a la izquierda", "Giro de −6° a la izquierda",
                "Giro de 6° a la izquierda", "Giro de 12° a la izquierda"
            ]),
            (BibleTextMotion.Skew, "Inclinación",
            [
                "Horizontal de −16°", "Horizontal de −8°", "Horizontal de 8°", "Horizontal de 16°",
                "Vertical de −16°", "Vertical de −8°", "Vertical de 8°", "Vertical de 16°"
            ]),
            (BibleTextMotion.Bounce, "Rebote", directions),
            (BibleTextMotion.Spring, "Resorte", directions),
            (BibleTextMotion.Blur, "Desenfoque",
            [
                "Suave", "Medio", "Intenso", "Profundo", "Suave con acercamiento",
                "Medio con acercamiento", "Intenso con acercamiento", "Profundo con acercamiento"
            ]),
            (BibleTextMotion.Wave, "Onda", directions),
            (BibleTextMotion.Fold, "Pliegue", directions),
            (BibleTextMotion.Unroll, "Desenrollado", directions)
        ];
        var presets = new List<BibleTextTransitionPreset>(291)
        {
            new("TextRise", "Subida suave", "Deslizamiento", BibleTextMotion.Slide, BibleTextScope.Passage, 0),
            new("TextLines", "Entrada por líneas", "Deslizamiento", BibleTextMotion.Slide, BibleTextScope.Lines, 0),
            new("TextFocus", "Enfoque suave", "Desenfoque", BibleTextMotion.Blur, BibleTextScope.Passage, 0)
        };
        foreach (var family in families)
        foreach (var scope in Enum.GetValues<BibleTextScope>())
        {
            var scopeName = scope switch
            {
                BibleTextScope.Passage => "Pasaje",
                BibleTextScope.Lines => "Líneas",
                _ => "Palabras"
            };
            for (var variant = 0; variant < family.Variants.Length; variant++)
                presets.Add(new BibleTextTransitionPreset(
                    $"TextPreset:{family.Motion}:{scope}:{variant}",
                    $"{family.Family} · {scopeName} · {family.Variants[variant]}",
                    family.Family, family.Motion, scope, variant));
        }
        return Array.AsReadOnly(presets.ToArray());
    }
}
