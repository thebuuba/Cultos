using System.Runtime.InteropServices;
using Culto.MoveHost;
using Xunit;

namespace Culto.Core.Tests;

public sealed class ObsMoveAbiTests
{
    [Fact]
    public void VideoAndAudioStructsMatchThePinnedObsX64Headers()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(56, Marshal.SizeOf<ObsNative.VideoInfo>());
        Assert.Equal(8, Marshal.OffsetOf<ObsNative.VideoInfo>(nameof(ObsNative.VideoInfo.FpsNumerator)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<ObsNative.VideoInfo>(nameof(ObsNative.VideoInfo.OutputFormat)).ToInt32());
        Assert.Equal(40, Marshal.OffsetOf<ObsNative.VideoInfo>(nameof(ObsNative.VideoInfo.GpuConversion)).ToInt32());
        Assert.Equal(44, Marshal.OffsetOf<ObsNative.VideoInfo>(nameof(ObsNative.VideoInfo.ColorSpace)).ToInt32());
        Assert.Equal(8, Marshal.SizeOf<ObsNative.AudioInfo>());
        Assert.Equal(8, Marshal.SizeOf<ObsNative.Vec2>());
    }
}
