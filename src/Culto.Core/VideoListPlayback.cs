namespace Culto.Core;

public sealed class VideoListPlayback
{
    public Guid? ListId { get; private set; }
    public Guid? ExpectedId { get; private set; }
    private Guid? _after;
    public void Start(Guid list, Guid cue) { ListId = list; ExpectedId = cue; _after = null; }
    public void After(Guid list, Guid current, Guid next) { Start(list,current); _after = next; }
    public void Cancel() { ListId = ExpectedId = _after = null; }
    public void Observe(Guid? cue) { if (cue != ExpectedId) Cancel(); }
    public Cue? Complete(Guid ended, SavedSet? list)
    {
        if (list is null || ExpectedId != ended || list.Id != ListId) return null;
        var index = list.Items.FindIndex(c => c.Id == ended);
        var next = _after is { } target ? list.Items.FirstOrDefault(c=>c.Id==target)
            : index >= 0 && index + 1 < list.Items.Count ? list.Items[index+1] : null;
        if (next is null) Cancel(); else Start(list.Id,next.Id);
        return next;
    }
}
