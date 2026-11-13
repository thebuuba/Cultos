using Culto.Core;
using Xunit;

public class VideoListPlaybackTests
{
    [Fact]
    public void ContinuesInEditedOrderAndStopsAfterExternalProjection()
    {
        var a = new Cue(); var b = new Cue(); var c = new Cue();
        var set = new SavedSet { Items = [a, c, b] };
        var state = new VideoListPlayback();
        state.Start(set.Id, a.Id);
        Assert.Equal(c.Id, state.Complete(a.Id, set)?.Id);
        Assert.Null(state.Complete(a.Id, set));
        state.Observe(Guid.NewGuid());
        Assert.Null(state.Complete(c.Id, set));
    }
    [Fact]
    public void ExplicitAfterExternalStartsAtSelectedItemAndDoesNotSurviveAnotherProjection()
    {
        var a = new Cue(); var b = new Cue(); var external = Guid.NewGuid();
        var set = new SavedSet { Items = [a,b] };
        var state = new VideoListPlayback();
        state.After(set.Id, external, b.Id);
        Assert.Equal(b.Id, state.Complete(external,set)?.Id);
        Assert.Null(state.Complete(b.Id,set));
        state.After(set.Id,external,a.Id); state.Observe(Guid.NewGuid());
        Assert.Null(state.Complete(external,set));
    }
}
