using Culto.MoveHost;
using Xunit;

namespace Culto.Core.Tests;

public sealed class ObsMoveFrameTimingTests
{
    [Fact]
    public void LateCallbacksCannotDeliverPreviousCommandsOrPretransitionFrames()
    {
        var timing = new ObsFrameTiming(outputReadyAfter: 500);
        Assert.False(timing.TryAcceptInitialFrame(470)); // Previous command, delivered by a new callback.
        Assert.False(timing.TryAcceptInitialFrame(500)); // Render overlapping the output assignment.
        Assert.True(timing.TryAcceptInitialFrame(501));
        timing.TransitionStarted(nativeRenderTimestamp: 510);
        foreach (ulong bufferedTimestamp in new ulong[] { 470, 500, 501, 509, 510 })
            Assert.False(timing.IsTransitionFrame(bufferedTimestamp)); // Even if callback arrived after Start.
        Assert.True(timing.IsTransitionFrame(511));
    }

    [Fact]
    public void CannotStartBeforeAFreshOutgoingFrameAndCannotDeliverTwoInitialFrames()
    {
        var timing = new ObsFrameTiming(100);
        Assert.False(timing.IsTransitionFrame(200));
        Assert.Throws<InvalidOperationException>(() => timing.TransitionStarted(200));
        Assert.True(timing.TryAcceptInitialFrame(201));
        Assert.False(timing.TryAcceptInitialFrame(202));
    }

    [Fact]
    public void TransitionBarrierNeverPrecedesTheDeliveredOutgoingFrame()
    {
        var timing = new ObsFrameTiming(100);
        Assert.True(timing.TryAcceptInitialFrame(120));
        timing.TransitionStarted(110);
        Assert.False(timing.IsTransitionFrame(115));
        Assert.False(timing.IsTransitionFrame(120));
        Assert.True(timing.IsTransitionFrame(121));
    }
}
