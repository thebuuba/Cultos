using Culto.Core;
using Xunit;

public class CrossfadeTimelineTests
{
    [Fact]
    public void DefaultDurationsRunTogetherAndCompleteAtThreeHundredMilliseconds()
    {
        var middle = CrossfadeTimeline.Sample(150, 300, 250, 250);
        Assert.Equal(.5, middle.VisualProgress, 8);
        Assert.Equal(.4, middle.OutgoingGain, 8);
        Assert.Equal(.6, middle.IncomingGain, 8);
        Assert.False(middle.Complete);
        Assert.False(CrossfadeTimeline.Sample(299, 300, 250, 250).Complete);
        Assert.Equal(new CrossfadeSample(1, 0, 1, true), CrossfadeTimeline.Sample(300, 300, 250, 250));
    }

    [Fact]
    public void LongerAudioEnvelopeContinuesAfterVisualTransitionCompletes()
    {
        var frame = CrossfadeTimeline.Sample(300, 300, 1200, 600);
        Assert.Equal(1, frame.VisualProgress);
        Assert.Equal(.75, frame.OutgoingGain, 8);
        Assert.Equal(.5, frame.IncomingGain, 8);
        Assert.False(frame.Complete);
        Assert.False(CrossfadeTimeline.Sample(1199, 300, 1200, 600).Complete);
        Assert.True(CrossfadeTimeline.Sample(1200, 300, 1200, 600).Complete);
    }

    [Fact]
    public void ZeroDurationChannelsChangeImmediatelyWithoutDelayingOtherChannels()
    {
        Assert.Equal(new CrossfadeSample(1, 0, 1, true), CrossfadeTimeline.Sample(0, 0, 0, 0));
        Assert.Equal(new CrossfadeSample(1, 1, 1, false), CrossfadeTimeline.Sample(0, 0, 500, 0));
        Assert.Equal(new CrossfadeSample(1, .5, 1, false), CrossfadeTimeline.Sample(250, 0, 500, 0));
    }

    [Theory]
    [InlineData(-500, 0, 1, 0, false)]
    [InlineData(0, 0, 1, 0, false)]
    [InlineData(50, .25, .75, .25, false)]
    [InlineData(150, .75, .25, .75, false)]
    [InlineData(2000, 1, 0, 1, true)]
    public void SamplingClampsToTheTimeline(double elapsed, double visual, double outgoing, double incoming, bool complete)
    {
        Assert.Equal(new CrossfadeSample(visual, outgoing, incoming, complete), CrossfadeTimeline.Sample(elapsed, 200, 200, 200));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteTimeCannotProduceInvalidVisualOrVolumeValues(double elapsed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CrossfadeTimeline.Sample(elapsed, 300, 250, 250));
    }

    [Theory]
    [InlineData(-1, 250, 250)]
    [InlineData(300, -1, 250)]
    [InlineData(300, 250, -1)]
    public void NegativeDurationsAreRejected(int visual, int outgoing, int incoming)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CrossfadeTimeline.Sample(0, visual, outgoing, incoming));
    }
}
