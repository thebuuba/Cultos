using Culto.Core;
using Xunit;

public class PresentationAudioEnvelopeTests
{
    private sealed class Channel
    {
        private readonly double _originalVolume;
        public double Volume;
        public int Writes;
        public Channel(double volume) => _originalVolume = Volume = volume;
        public PresentationAudioChannel Bind() => new(_originalVolume, value => { Volume = value; Writes++; });
    }

    [Fact]
    public void FadeOutAndInPreserveEachOwnedMediaVolumeAndNeverWriteAnUnregisteredChannel()
    {
        var first = new Channel(.8); var second = new Channel(.3); var otherPresentation = new Channel(.6);
        using var envelope = new PresentationAudioEnvelope([first.Bind(), second.Bind()]);
        Assert.True(envelope.HasChannels);
        var fadeOut = envelope.BeginFade(0, 1500);
        Assert.False(envelope.Advance(fadeOut, 750));
        Assert.Equal(.4, first.Volume, 8); Assert.Equal(.15, second.Volume, 8);
        Assert.True(envelope.Advance(fadeOut, 1500));
        Assert.Equal(0, first.Volume); Assert.Equal(0, second.Volume);
        var fadeIn = envelope.BeginFade(1, 600);
        Assert.False(envelope.Advance(fadeIn, 300));
        Assert.Equal(.4, first.Volume, 8); Assert.Equal(.15, second.Volume, 8);
        Assert.True(envelope.Advance(fadeIn, 600));
        Assert.Equal(.8, first.Volume); Assert.Equal(.3, second.Volume);
        Assert.Equal(.6, otherPresentation.Volume); Assert.Equal(0, otherPresentation.Writes);
    }

    [Fact]
    public void NewFadeStartsAtCurrentGainAndOldTicksCannotWriteOrComplete()
    {
        var channel = new Channel(.8);
        using var envelope = new PresentationAudioEnvelope([channel.Bind()]);
        var outgoing = envelope.BeginFade(0, 1000);
        envelope.Advance(outgoing, 600);
        Assert.Equal(.4, envelope.CurrentGain, 8);
        var incoming = envelope.BeginFade(1, 500);
        var writes = channel.Writes;
        Assert.False(envelope.Advance(outgoing, 1000));
        Assert.Equal(writes, channel.Writes);
        Assert.False(envelope.Advance(incoming, 250));
        Assert.Equal(.56, channel.Volume, 8);
        Assert.True(envelope.Advance(incoming, 500));
        Assert.False(envelope.Advance(incoming, 700));
        Assert.Equal(.8, channel.Volume);
    }

    [Fact]
    public void CancelLeavesCurrentVolumeAndRestoreInvalidatesAllOldTicks()
    {
        var channel = new Channel(.7);
        using var envelope = new PresentationAudioEnvelope([channel.Bind()]);
        var token = envelope.BeginFade(0, 1000);
        envelope.Advance(token, 500); envelope.Cancel();
        Assert.False(envelope.Advance(token, 1000)); Assert.Equal(.35, channel.Volume, 8);
        var next = envelope.BeginFade(0, 1000);
        envelope.RestoreOriginalVolumes();
        Assert.False(envelope.Advance(next, 1000));
        Assert.Equal(.7, channel.Volume); Assert.Equal(1, envelope.CurrentGain);
    }

    [Fact]
    public void ZeroDurationAndImmediateGainApplySynchronouslyWithoutRaisingAuthoredMute()
    {
        var audible = new Channel(.45); var muted = new Channel(0);
        using var envelope = new PresentationAudioEnvelope([audible.Bind(), muted.Bind()]);
        envelope.SetGain(0);
        Assert.Equal(0, audible.Volume);
        var token = envelope.BeginFade(.5, 0);
        Assert.Equal(.225, audible.Volume, 8); Assert.Equal(0, muted.Volume);
        Assert.True(envelope.Advance(token, 0));
        Assert.False(envelope.Advance(token, 100));
        token = envelope.BeginFade(0, 1000);
        envelope.SetGain(.75);
        Assert.False(envelope.Advance(token, 1000));
        Assert.Equal(.3375, audible.Volume, 8); Assert.Equal(0, muted.Volume);
    }

    [Fact]
    public void OutOfOrderTicksCannotReverseAnEnvelopeAndLateTicksClampAtItsTarget()
    {
        var channel = new Channel(1);
        using var envelope = new PresentationAudioEnvelope([channel.Bind()]);
        var token = envelope.BeginFade(0, 1000);
        envelope.Advance(token, 750); envelope.Advance(token, 250);
        Assert.Equal(.25, channel.Volume);
        Assert.True(envelope.Advance(token, int.MaxValue)); Assert.Equal(0, channel.Volume);
    }

    [Fact]
    public void DisposalRestoresOwnedVolumesAndMakesQueuedTicksHarmless()
    {
        var channel = new Channel(.6);
        var envelope = new PresentationAudioEnvelope([channel.Bind()]);
        var token = envelope.BeginFade(0, 500);
        envelope.Advance(token, 250); envelope.Dispose();
        Assert.Equal(.6, channel.Volume);
        var writes = channel.Writes;
        Assert.False(envelope.Advance(token, 500)); envelope.Dispose();
        Assert.Equal(writes, channel.Writes);
        Assert.Throws<ObjectDisposedException>(() => envelope.BeginFade(0, 500));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-.1)]
    [InlineData(1.1)]
    public void InvalidGainsAreRejectedWithoutChangingAnActiveFade(double gain)
    {
        var channel = new Channel(.8);
        using var envelope = new PresentationAudioEnvelope([channel.Bind()]);
        var token = envelope.BeginFade(0, 1000);
        Assert.Throws<ArgumentOutOfRangeException>(() => envelope.BeginFade(gain, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => envelope.SetGain(gain));
        Assert.True(envelope.Advance(token, 1000)); Assert.Equal(0, channel.Volume);
    }

    [Fact]
    public void InvalidDurationAndBaselinesAreRejectedBeforeAnyMediaWrite()
    {
        var channel = new Channel(.8);
        using var envelope = new PresentationAudioEnvelope([channel.Bind()]);
        Assert.Throws<ArgumentOutOfRangeException>(() => envelope.BeginFade(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PresentationAudioEnvelope([channel.Bind(), new(double.NaN, _ => { })]));
        Assert.Equal(0, channel.Writes);
    }

    [Fact]
    public void EmptyPresentationNeedsNoMediaAccess()
    {
        using var envelope = new PresentationAudioEnvelope([]);
        Assert.False(envelope.HasChannels);
        var token = envelope.BeginFade(0, 0);
        Assert.True(envelope.Advance(token, 0));
        envelope.RestoreOriginalVolumes(); Assert.Equal(1, envelope.CurrentGain);
    }

    [Fact]
    public void ChangingTheCallersChannelListCannotRedirectWritesToAnotherPresentation()
    {
        var owned = new Channel(.8); var foreign = new Channel(.7);
        var channels = new[] { owned.Bind() };
        using var envelope = new PresentationAudioEnvelope(channels);
        channels[0] = foreign.Bind();
        envelope.SetGain(0);
        Assert.Equal(0, owned.Volume);
        Assert.Equal(.7, foreign.Volume); Assert.Equal(0, foreign.Writes);
    }

    [Fact]
    public void FailedWriteInvalidatesItsFadeAndRestorationStillAttemptsOtherChannels()
    {
        var first = new Channel(.8); var last = new Channel(.4);
        var failure = new InvalidOperationException("Owned shape became unavailable");
        var envelope = new PresentationAudioEnvelope([first.Bind(), new(.5, _ => throw failure), last.Bind()]);
        var token = envelope.BeginFade(0, 1000);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => envelope.Advance(token, 500)));
        var writes = first.Writes;
        Assert.False(envelope.Advance(token, 1000)); Assert.Equal(writes, first.Writes);
        var error = Assert.Throws<AggregateException>(() => envelope.RestoreOriginalVolumes());
        Assert.Contains(failure, error.InnerExceptions);
        Assert.Equal(.8, first.Volume); Assert.Equal(.4, last.Volume); Assert.Equal(1, last.Writes);
        Assert.Throws<AggregateException>(() => envelope.Dispose());
        Assert.False(envelope.Advance(token, 1000));
    }

    [Fact]
    public void ReentrantCancellationStopsTheOldTickBeforeWritingAnotherChannel()
    {
        PresentationAudioEnvelope? envelope = null;
        var last = new Channel(.4);
        envelope = new PresentationAudioEnvelope([new(.8, _ => envelope!.Cancel()), last.Bind()]);
        var token = envelope.BeginFade(0, 1000);
        Assert.False(envelope.Advance(token, 500));
        Assert.Equal(.4, last.Volume); Assert.Equal(0, last.Writes);
        // No external resources exist in this fake; avoid invoking its cancellation callback during cleanup.
    }
}
