using Culto.Core;

namespace Culto.Desktop.Services;

public sealed record SceneTile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Label { get; init; } = "";
    public string Action { get; init; } = "image";
    public string Color { get; init; } = "#E6F3FC";
    public string? ImageData { get; init; }
    public Cue? Cue { get; init; }
    public string? TransitionPresetId { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string AccessibleName => !string.IsNullOrWhiteSpace(Label) ? Label : Action switch
    {
        "logo" => "Logo", "blackout" => "Fondo negro", "image" => "Imagen", "video" => "Video",
        "audio" => "Audio", "youtube" => "YouTube", "browser" => "Navegador", "powerpoint" => "PowerPoint",
        _ => string.IsNullOrWhiteSpace(Cue?.Title) ? "Escena" : Cue.Title
    };

    public static List<SceneTile> Defaults() =>
    [
        new() { Id = "logo", Label = "Logo", Action = "logo", Color = "#F5FBF3" },
        new() { Id = "blackout", Label = "Fondo negro", Action = "blackout", Color = "#08090C" },
        new() { Id = "image", Label = "Imagen", Action = "image", Color = "#E6F3FC" },
        new() { Id = "video", Label = "Video", Action = "video", Color = "#FFF0D7" },
        new() { Id = "youtube", Label = "YouTube", Action = "youtube", Color = "#FFFFFF" },
        new() { Id = "browser", Label = "Navegador", Action = "browser", Color = "#354E6D" }
    ];
}
