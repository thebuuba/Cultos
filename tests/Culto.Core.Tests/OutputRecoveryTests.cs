using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class OutputRecoveryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FailedReplacementRestoresActualOutputAndKeepsPreparedTarget(bool blackout, bool hidden)
    {
        var session = new PresentationSession();
        var previous = new Cue { Kind = CueKind.Video, Title = "Anterior", FilePath = "anterior.mkv" };
        var failed = new Cue { Kind = CueKind.Video, Title = "No disponible" };
        session.Prepare(failed); session.GoLive();
        var changes = 0; session.Changed += () => changes++;
        session.RestoreOutput(previous, blackout, hidden);
        Assert.Equal(previous.Id, session.Live!.Id);
        Assert.NotSame(previous, session.Live);
        Assert.Same(failed, session.Prepared);
        Assert.False(session.PreviewFollowsLive);
        Assert.Equal(blackout, session.IsBlackout);
        Assert.Equal(hidden, session.IsTextHidden);
        Assert.Equal(1, changes);
        previous.Title = "Editado";
        Assert.Equal("Anterior", session.Live.Title);
    }

    [Fact]
    public void FailedFirstProjectionCanRestoreEmptyOutput()
    {
        var session = new PresentationSession();
        var failed = new Cue { Title = "No disponible" };
        session.Prepare(failed); session.GoLive();
        session.RestoreOutput(null, false, false);
        Assert.Null(session.Live);
        Assert.Same(failed, session.Prepared);
    }
}
