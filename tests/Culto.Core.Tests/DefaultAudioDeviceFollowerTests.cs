using Culto.Desktop.Projection;
using Xunit;

public sealed class DefaultAudioDeviceFollowerTests
{
    [Fact]
    public void OutputFollowsChangesAndRecoversWhenSameAdapterReturns()
    {
        string? device = "speakers";
        var selected = new List<string?>();
        using var follower = new DefaultAudioDeviceFollower(() => device, selected.Add);
        follower.Refresh(); follower.Refresh();
        device = "usb"; follower.Refresh();
        device = null; follower.Refresh(); follower.Refresh();
        device = "usb"; follower.Refresh();
        Assert.Equal(new string?[] { "speakers", "usb", null, "usb" }, selected);
        follower.Dispose();
        device = "speakers"; follower.Refresh();
        Assert.Equal(4, selected.Count);
    }

    [Fact]
    public void FailedSinkIsRetriedWithoutAnotherWindowsDeviceChange()
    {
        int attempts = 0;
        using var follower = new DefaultAudioDeviceFollower(() => "usb", _ =>
        {
            if (++attempts == 1) throw new InvalidOperationException("Adapter is still starting");
        });
        follower.Refresh(); follower.Refresh(); follower.Refresh();
        Assert.Equal(2, attempts);
        follower.Invalidate(); follower.Refresh(); follower.Refresh();
        Assert.Equal(3, attempts);
    }
}
