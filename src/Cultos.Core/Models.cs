namespace Cultos.Core;

public enum ContentType { Welcome, Bible, Hymn, Song, FreeText, Image, Video, Background, Audio, Web }
public enum PresentationState { Empty, Content, Black, Logo }
public enum SceneType { Logo, Bible, Hymn, Song, Video, YouTube, Title, Black, Image, Audio, Custom }

public sealed class PresentationScene
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Name { get; set; } = "Escena";
    public SceneType Type { get; set; }
    public int Position { get; set; }
    public bool IsBuiltIn { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? MediaPath { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}


public sealed record BibleVerse(int Id, string Book, int Chapter, int Verse, string Text, string Translation)
{
    public string Reference => $"{Book} {Chapter}:{Verse}";
}

public sealed record Hymn(int Id, int Number, string Title, string Lyrics, string Sections);
public sealed record LibrarySong(int Id, string Title, string Author, string Lyrics, string Tags);
public sealed record MediaLibraryItem(int Id, string Name, string Path, string Kind)
{
    public bool Exists => File.Exists(Path);
}

public sealed class LogoSceneSettings
{
    public List<string> ImagePaths { get; set; } = [];
    public int IntervalSeconds { get; set; } = 8;
    public bool Loop { get; set; } = true;
}

public sealed class ServiceItem
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public ContentType Type { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? MediaPath { get; set; }
    public int Position { get; set; }
    public string Status { get; set; } = "Pendiente";
}

public sealed class WorshipService
{
    public int Id { get; set; }
    public string Name { get; set; } = "Nuevo culto";
    public DateTime Date { get; set; } = DateTime.Today;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<ServiceItem> Items { get; set; } = [];
}

public sealed record PresentationSnapshot(PresentationState State, string Title, string Content, string? MediaPath = null);
