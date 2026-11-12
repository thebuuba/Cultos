using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class PreviewFollowTests
{
    private static bool Follows(PresentationSession session) => session.PreviewFollowsLive;

    [Fact]
    public void PreviewFollowsOutputUntilAnotherCueIsPrepared()
    {
        var session = new PresentationSession();
        Assert.True(Follows(session));
        var video = new Cue { Kind = CueKind.Video, Title = "Himno", FilePath = "himno.mkv" };
        session.Prepare(video);
        Assert.False(Follows(session));
        session.GoLive();
        Assert.True(Follows(session));
        session.Prepare(new Cue { Body = "Siguiente lectura" });
        Assert.False(Follows(session));
        Assert.Equal(video.Id, session.Live!.Id);
        session.Prepare(video);
        Assert.True(Follows(session));
    }

    [Fact]
    public void BlackoutAndBackgroundKeepFollowingActualOutput()
    {
        var session = new PresentationSession();
        session.Prepare(new Cue { Body = "Bienvenidos" }); session.GoLive();
        session.ShowBlackout(); Assert.True(Follows(session));
        session.ShowBackground(); Assert.True(Follows(session));
        session.Prepare(new Cue { Body = "Preparado" });
        session.RestoreContent(); Assert.False(Follows(session));
    }

    [Fact]
    public void EditingPreparedContentWithSameIdDoesNotMasqueradeAsLive()
    {
        var session = new PresentationSession();
        var cue = new Cue { Body = "Original" };
        session.Prepare(cue); session.GoLive();
        cue.Body = "Editado";
        session.Prepare(cue);
        Assert.False(Follows(session));
        Assert.Equal("Original", session.Live!.Body);
        session.GoLive(); Assert.True(Follows(session));
    }

    [Fact]
    public void AdvancingOrderReturnsPreviewToOutput()
    {
        var plan = new ServicePlan { Items = [new() { Title = "A" }, new() { Title = "B" }] };
        var session = new PresentationSession();
        session.Prepare(plan.Items[0]); session.GoLive();
        session.Prepare(new Cue { Title = "Otro" });
        Assert.True(session.Move(plan, 1));
        Assert.True(Follows(session));
        Assert.Equal(plan.Items[1].Id, session.Live!.Id);
    }

    [Fact]
    public void ReturningToOutputDoesNotReplayOrClearBlackout()
    {
        var session = new PresentationSession();
        session.Prepare(new Cue { Title = "En salida" }); session.GoLive();
        var live = session.Live;
        session.ShowBlackout();
        session.Prepare(new Cue { Title = "Otro" });
        session.FollowLive();
        Assert.True(Follows(session));
        Assert.Same(live, session.Live);
        Assert.NotSame(live, session.Prepared);
        Assert.Equal(live!.Id, session.Prepared!.Id);
        Assert.True(session.IsBlackout);
        session.Prepared.Body = "Edición sin enviar";
        Assert.NotEqual(session.Prepared.Body, session.Live!.Body);
        Assert.False(Follows(session));
    }
}
