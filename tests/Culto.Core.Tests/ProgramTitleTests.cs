using System.Text.Json;
using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class ProgramTitleTests
{
    [Fact]
    public void ThemeAndPurposeAdvanceWithoutLeavingTheSceneOrChangingTheSavedCue()
    {
        var cue = new Cue { Title = "Programa", Kind = CueKind.Image, FilePath = "tema.png",
            ProgramTitle = new("tema.png", "proposito.jpg") };
        var session = new PresentationSession();
        session.Prepare(cue); session.GoLive();
        Assert.False(session.MoveProgramTitle(-1));
        Assert.True(session.MoveProgramTitle(1));
        Assert.Equal(cue.Id, session.Live!.Id);
        Assert.Equal("proposito.jpg", session.Live.FilePath);
        Assert.Equal(CueKind.Image, session.Live.Kind);
        Assert.Equal("", session.Live.Body);
        Assert.Equal("", session.Live.Reference);
        Assert.Equal("tema.png", cue.FilePath);
        Assert.False(session.MoveProgramTitle(1));
        Assert.True(session.MoveProgramTitle(-1));
        Assert.Equal("tema.png", session.Live.FilePath);
        session.Prepare(cue); session.GoLive();
        Assert.Equal("tema.png", session.Live.FilePath);
        Assert.False(session.MoveProgramTitle(0));
    }

    [Fact]
    public void ProgramTitleIsSavedAndCopiedAndOldCuesStillWork()
    {
        var cue = new Cue { ProgramTitle = new("Tema", "Propósito") };
        var restored = JsonSerializer.Deserialize<Cue>(JsonSerializer.Serialize(cue))!;
        Assert.Equal(cue.ProgramTitle, restored.ProgramTitle);
        Assert.Equal(cue.ProgramTitle, cue.Copy(newId: true).ProgramTitle);
        Assert.Null(JsonSerializer.Deserialize<Cue>("{}")!.ProgramTitle);
        var session = new PresentationSession();
        session.Prepare(new Cue()); session.GoLive();
        Assert.False(session.MoveProgramTitle(1));
    }
}
