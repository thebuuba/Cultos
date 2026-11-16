namespace Culto.Core;

public enum CueKind { Text, Bible, Image, Video, Audio, PowerPoint, WindowCapture, BrowserSource, Scene }

public sealed class Cue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public CueKind Kind { get; set; }
    public string Body { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Version { get; set; } = "";
    public string? FilePath { get; set; }
    public string? YouTubeUrl { get; set; }
    public int? HymnNumber { get; set; }
    public string Notes { get; set; } = "";
    public WindowCaptureOptions? Capture { get; set; }
    public BrowserSourceOptions? Browser { get; set; }
    public SceneComposition? Scene { get; set; }

    public Cue Copy(bool newId = false)
    {
        var copy = (Cue)MemberwiseClone();
        if (newId) copy.Id = Guid.NewGuid();
        copy.Capture = Capture is { } capture ? capture with { } : null;
        copy.Browser = Browser is { } browser ? browser with { } : null;
        copy.Scene = Scene?.Copy();
        return copy;
    }
}

public sealed class ServicePlan
{
    public string Name { get; set; } = "Culto del sábado";
    public List<Cue> Items { get; set; } = [];
}

public sealed record BibleVerse(string Book, int Chapter, int Verse, string Text)
{
    public int? EndVerse { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int LastVerse => EndVerse ?? Verse;
    [System.Text.Json.Serialization.JsonIgnore]
    public string VerseNumber => LastVerse == Verse ? Verse.ToString() : $"{Verse}-{LastVerse}";
}

public sealed class BibleDocument
{
    public string Name { get; set; } = "";
    public string Attribution { get; set; } = "";
    public List<BibleVerse> Verses { get; set; } = [];
}
