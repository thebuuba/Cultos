using Culto.Core;
using Xunit;

public class LogoSlideshowTests
{
    [Fact]
    public void StartsAtSelectedImageAndHoldsForFullIntervalAfterTransition()
    {
        var slideshow = new LogoSlideshow();
        Assert.Equal("b.png", slideshow.Start(["a.png", "b.png", "c.png"], "b.png", new() { IntervalSeconds = 2 }));
        Assert.Null(slideshow.Advance(0, true));
        Assert.Null(slideshow.Advance(800, true));
        Assert.Null(slideshow.Advance(1000, false));
        Assert.Null(slideshow.Advance(2999, false));
        Assert.Equal("c.png", slideshow.Advance(3000, false));
        Assert.Null(slideshow.Advance(3200, true));
        Assert.Null(slideshow.Advance(3300, false));
        Assert.Equal("a.png", slideshow.Advance(5300, false));
    }

    [Fact]
    public void LateTickAdvancesOnlyOneImageAndStartsANewHold()
    {
        var slideshow = new LogoSlideshow();
        slideshow.Start(["a.png", "b.png", "c.png"], null, new() { IntervalSeconds = 1 });
        slideshow.Advance(0, false);
        Assert.Equal("b.png", slideshow.Advance(50000, false));
        Assert.Null(slideshow.Advance(50001, false));
        Assert.Null(slideshow.Advance(51000, false));
        Assert.Equal("c.png", slideshow.Advance(51001, false));
    }

    [Fact]
    public void StopAndRestartDiscardThePreviousCountdown()
    {
        var slideshow = new LogoSlideshow();
        slideshow.Start(["a.png", "b.png"], null, new() { IntervalSeconds = 1 });
        slideshow.Advance(0, false);
        slideshow.Stop();
        Assert.False(slideshow.IsRunning);
        Assert.Null(slideshow.CurrentPath);
        Assert.Null(slideshow.Advance(2000, false));
        Assert.Equal("new.png", slideshow.Start(["new.png", "other.png"], null, new() { IntervalSeconds = 2 }));
        Assert.Null(slideshow.Advance(2000, false));
        Assert.Null(slideshow.Advance(3000, false));
        Assert.Equal("other.png", slideshow.Advance(4000, false));
    }

    [Fact]
    public void OlderClockSamplesDoNotOverwriteTheCurrentCountdown()
    {
        var slideshow = new LogoSlideshow();
        slideshow.Start(["a.png", "b.png"], null, new() { IntervalSeconds = 1 });
        slideshow.Advance(1000, false);
        Assert.Null(slideshow.Advance(500, true));
        Assert.Equal("b.png", slideshow.Advance(2000, false));
    }

    [Fact]
    public void ATransitionDuringTheHoldRestartsTheHoldWhenComplete()
    {
        var slideshow = new LogoSlideshow();
        slideshow.Start(["a.png", "b.png"], null, new() { IntervalSeconds = 1 });
        slideshow.Advance(0, false);
        slideshow.Advance(900, true);
        Assert.Null(slideshow.Advance(1100, false));
        Assert.Null(slideshow.Advance(2000, false));
        Assert.Equal("b.png", slideshow.Advance(2100, false));
    }

    [Fact]
    public void ListAndTimingAreSnapshotsAndMissingSelectionUsesFirstImage()
    {
        var paths = new List<string> { "a.png", "", "A.PNG", "b.png" };
        var options = new LogoPlaybackOptions { IntervalSeconds = 1 };
        var slideshow = new LogoSlideshow();
        Assert.Equal("a.png", slideshow.Start(paths, "missing.png", options));
        paths.Clear();
        options.IntervalSeconds = 60;
        slideshow.Advance(0, false);
        Assert.Equal("b.png", slideshow.Advance(1000, false));
        slideshow.Advance(1100, false);
        Assert.Equal("a.png", slideshow.Advance(2100, false));
    }

    [Fact]
    public void EmptyListStopsAndSingleImageNeverTransitionsToItself()
    {
        var slideshow = new LogoSlideshow();
        Assert.Equal("a.png", slideshow.Start(["a.png"], "A.PNG", new()));
        Assert.True(slideshow.IsRunning);
        Assert.Null(slideshow.Advance(0, false));
        Assert.Null(slideshow.Advance(long.MaxValue, false));
        Assert.Null(slideshow.Start([], null, new()));
        Assert.False(slideshow.IsRunning);
        Assert.Null(slideshow.CurrentPath);
    }

    [Theory]
    [InlineData(-10, 0, 1, 100)]
    [InlineData(4000, 5000, 3600, 3000)]
    [InlineData(1.5, 250, 1.5, 250)]
    [InlineData(double.NaN, 300, 10, 300)]
    [InlineData(double.PositiveInfinity, 300, 10, 300)]
    public void OptionsNormalizeWithoutChangingTheSource(double seconds, int transition, double expectedSeconds, int expectedTransition)
    {
        var source = new LogoPlaybackOptions { IntervalSeconds = seconds, TransitionMilliseconds = transition };
        var normalized = source.Normalize();
        Assert.Equal(expectedSeconds, normalized.IntervalSeconds);
        Assert.Equal(expectedTransition, normalized.TransitionMilliseconds);
        Assert.Equal(seconds, source.IntervalSeconds);
        Assert.Equal(transition, source.TransitionMilliseconds);
    }
}
