namespace Culto.Core;

public sealed class SavedSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<Cue> Items { get; set; } = [];

    public static SavedSet Capture(string name, IEnumerable<Cue> items) => new()
    {
        Name = name.Trim(), Items = items.Select(CloneCue).ToList()
    };

    public List<Cue> CreateItems() => Items.Select(CloneCue).ToList();

    private static Cue CloneCue(Cue cue) => new()
    {
        Title = cue.Title, Kind = cue.Kind, Body = cue.Body, Reference = cue.Reference,
        Version = cue.Version, FilePath = cue.FilePath, HymnNumber = cue.HymnNumber, Notes = cue.Notes, Capture = cue.Capture is {} capture ? capture with {} : null
    };
}
