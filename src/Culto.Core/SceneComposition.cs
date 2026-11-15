using System.Text.RegularExpressions;

namespace Culto.Core;

public sealed class SceneComposition
{
    public const int MaxLayers = 32;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public string Background { get; set; } = "#000000";
    // Layers are painted from back to front, in list order.
    public List<SceneLayer> Layers { get; set; } = [];

    public SceneComposition Copy()
    {
        Validate();
        return new SceneComposition { Width = Width, Height = Height, Background = Background,
            Layers = Layers.Select(layer => layer.Copy()).ToList() };
    }

    public SceneComposition Normalize()
    {
        var copy = Copy();
        copy.Background = copy.Background.Trim().ToUpperInvariant();
        foreach (var layer in copy.Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Id)) layer.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(layer.SourceId)) layer.SourceId = Guid.NewGuid().ToString("N");
            layer.Name = (layer.Name ?? "").Trim();
        }
        return copy;
    }

    public void Validate()
    {
        if (Width is < 1 or > 8192 || Height is < 1 or > 8192)
            throw new ArgumentException("El lienzo debe medir entre 1 y 8192 píxeles por lado.");
        if (!Regex.IsMatch((Background ?? "").Trim(), "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"))
            throw new ArgumentException("El fondo debe tener formato #RRGGBB o #AARRGGBB.");
        if (Layers is null || Layers.Count > MaxLayers)
            throw new ArgumentException($"La escena admite hasta {MaxLayers} capas.");
        var identities = new HashSet<string>();
        foreach (var layer in Layers)
        {
            if (layer is null) throw new ArgumentException("La capa no puede estar vacía.");
            layer.Validate();
            if (!string.IsNullOrWhiteSpace(layer.Id) && !identities.Add(layer.Id))
                throw new ArgumentException("Cada capa debe tener una identidad distinta.");
        }
    }
}

public sealed class SceneLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public Cue Source { get; set; } = new();
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 1920;
    public double Height { get; set; } = 1080;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public bool Muted { get; set; }
    public int Volume { get; set; } = 100;

    public void Validate()
    {
        if (Source is null || Source.Kind is CueKind.Scene or CueKind.PowerPoint || Source.Scene is not null)
            throw new ArgumentException("Las capas no admiten escenas anidadas ni PowerPoint.");
        if (!Enum.IsDefined(Source.Kind)) throw new ArgumentException("El tipo de fuente no es válido.");
        if (!double.IsFinite(X) || !double.IsFinite(Y) || Math.Abs(X) > 32768 || Math.Abs(Y) > 32768
            || !double.IsFinite(Width) || !double.IsFinite(Height) || Width is < 1 or > 16384 || Height is < 1 or > 16384
            || !double.IsFinite(Rotation) || Math.Abs(Rotation) > 36000
            || !double.IsFinite(Opacity) || Opacity is < 0 or > 1 || Volume is < 0 or > 100)
            throw new ArgumentException("Revisa la posición, dimensiones, rotación, opacidad y volumen de la capa.");
    }

    public SceneLayer Copy()
    {
        Validate();
        var copy = (SceneLayer)MemberwiseClone();
        copy.Source = Source.Copy();
        return copy;
    }
}
