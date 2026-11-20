using System.Runtime.InteropServices;
using System.Diagnostics;
using Culto.MoveHost;
using Xunit;

namespace Culto.Core.Tests;

public sealed class ObsLiveMoveTests
{
    private static ObsLiveLayer Layer => new("camera", "Local\\CultoLive-test", 16, 16, 0, 0, 32, 32);
    [Fact]
    public void LiveCommandRoundTripsSharedIdentityAndDifferentTransforms()
    {
        var command = new ObsLiveMoveCommand(320, 180, 500, new(), [Layer], [Layer with { X = 80, Rotation = 30, Opacity = .5 }]);
        var decoded = ObsLiveMoveCommand.Decode(ObsLiveMoveCommand.Encode(command));
        Assert.Equal(decoded.Outgoing[0].SourceId, decoded.Incoming[0].SourceId);
        Assert.Equal(80, decoded.Incoming[0].X);
        Assert.Equal(.5, decoded.Incoming[0].Opacity);
    }
    [Fact]
    public void SharedIdentityRejectsDifferentMapsAndDuplicateItems()
    {
        var command = new ObsLiveMoveCommand(320, 180, 500, new(), [Layer], [Layer with { MapName = "Local\\CultoLive-other" }]);
        Assert.Throws<InvalidDataException>(command.Validate);
        Assert.Throws<InvalidDataException>((command with { Outgoing = [Layer, Layer], Incoming = [] }).Validate);
        Assert.Throws<InvalidDataException>((command with { Incoming = [Layer with { X = double.NaN }] }).Validate);
    }
    [Fact]
    public void SourceRegistrationPrefixMatchesObs33X64Abi()
    {
        Assert.Equal(128, Marshal.SizeOf<ObsNative.SourceInfo>());
        Assert.Equal(16, Marshal.OffsetOf<ObsNative.SourceInfo>(nameof(ObsNative.SourceInfo.GetName)).ToInt32());
        Assert.Equal(120, Marshal.OffsetOf<ObsNative.SourceInfo>(nameof(ObsNative.SourceInfo.VideoRender)).ToInt32());
    }
    [Fact]
    public void FullHdSharedFrameTransferFitsOneSixtyFpsFrame()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var writer = SharedVideoFrame.Create(1920, 1080);
        using var reader = SharedVideoFrame.Open(writer.MapName, 1920, 1080);
        var pixels = Enumerable.Repeat((byte)193, writer.ByteCount).ToArray();
        var result = new byte[pixels.Length];
        var milliseconds = new List<double>();
        for (int i = 0; i < 13; i++)
        {
            var clock = Stopwatch.StartNew();
            writer.Write(pixels);
            Assert.True(reader.TryRead(result));
            if (i >= 3) milliseconds.Add(clock.Elapsed.TotalMilliseconds);
        }
        Assert.Equal(pixels, result);
        Assert.InRange(milliseconds.Order().ElementAt(milliseconds.Count / 2), 0, 1000d / 60);
    }
    [Fact]
    public async Task SharedFramesNeverExposeAnIncompleteWriterFrame()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var writer = SharedVideoFrame.Create(80, 45);
        using var reader = SharedVideoFrame.Open(writer.MapName, 80, 45);
        var result = new byte[80 * 45 * 4];
        Assert.False(reader.TryRead(result));
        var a = Enumerable.Repeat((byte)31, result.Length).ToArray();
        var b = Enumerable.Repeat((byte)193, result.Length).ToArray();
        writer.Write(a);
        Assert.True(reader.TryRead(result)); Assert.Equal(a, result);
        var updates = Task.Run(() => { if (OperatingSystem.IsWindows()) for (int i = 0; i < 200; i++) writer.Write((i & 1) == 0 ? a : b); });
        do
        {
            if (reader.TryRead(result)) Assert.True(result.All(pixel => pixel == result[0]));
        } while (!updates.IsCompleted);
        await updates;
        Assert.True(reader.TryRead(result)); Assert.Equal(b, result);
    }
}
