using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public class WindowCaptureCueTests
{
    private static Cue Capture() => new() { Kind = CueKind.WindowCapture, Title = "YouTube",
        Capture = new WindowCaptureOptions { WindowHandle = 123, ProcessId = 456, WindowTitle = "Video",
            WindowClass = "Chrome_WidgetWin_1", Executable = "chrome.exe", CropTop = 80, ControlAudio = true } };

    [Fact]
    public void CapturesSurviveSessionAndSavedServiceCopies()
    {
        var cue = Capture();
        var session = new PresentationSession(); session.Prepare(cue); session.GoLive();
        Assert.Equal(cue.Capture, session.Live!.Capture);
        var stored = SavedSet.Capture("Sábado", [cue]);
        Assert.Equal(cue.Capture, Assert.Single(stored.CreateItems()).Capture);
        var json = System.Text.Json.JsonSerializer.Serialize(stored);
        Assert.Equal(cue.Capture, System.Text.Json.JsonSerializer.Deserialize<SavedSet>(json)!.Items[0].Capture);
    }

    [Fact]
    public void ChangingCaptureCropDoesNotAlterLiveOrAutomaticallyFollowIt()
    {
        var cue = Capture();
        var session = new PresentationSession(); session.Prepare(cue); session.GoLive();
        cue.Capture!.CropTop = 120;
        Assert.Equal(80, session.Live!.Capture!.CropTop);
        Assert.False(session.PreviewFollowsLive);
        session.FollowLive();
        Assert.Equal(80, session.Prepared!.Capture!.CropTop);
        Assert.True(session.PreviewFollowsLive);
    }

    [Fact]
    public void OldCueKindNumericValuesRemainUnchanged()
    {
        Assert.Equal(0, (int)CueKind.Text); Assert.Equal(5, (int)CueKind.PowerPoint);
        Assert.Equal(6, (int)CueKind.WindowCapture);
    }
}
