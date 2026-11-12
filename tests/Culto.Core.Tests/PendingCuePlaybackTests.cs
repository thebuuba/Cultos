using Culto.Core;
using Xunit;

public class PendingCuePlaybackTests
{
    [Fact]
    public void BlackoutDuringReplacementPreservesTheRequestAndItsPausedIntentThroughNativeStartup()
    {
        var pending = new PendingCuePlayback();
        var request = pending.Begin();
        pending.SetPaused(true);
        Assert.True(pending.Matches(request));
        Assert.True(pending.MarkPresented(request));
        Assert.True(pending.IsPending && pending.IsPresented);
        Assert.Equal(new CuePlaybackIntent(true, false), pending.Complete(request));
        Assert.False(pending.IsPending);
    }

    [Fact]
    public void ExplicitResumeWhileRestoringOverridesOlderPauseWithoutDiscardingTheNewCue()
    {
        var pending = new PendingCuePlayback();
        var request = pending.Begin();
        pending.SetPaused(true); pending.SetPaused(false);
        Assert.Equal(new CuePlaybackIntent(false, false), pending.Complete(request));
    }

    [Fact]
    public void NewCueInvalidatesOldCompletionAndStartsWithFreshPlaybackIntent()
    {
        var pending = new PendingCuePlayback();
        var old = pending.Begin(); pending.RequestRestart(); pending.SetPaused(true);
        var latest = pending.Begin();
        Assert.False(pending.MarkPresented(old)); Assert.Null(pending.Complete(old));
        Assert.True(pending.Matches(latest));
        Assert.Equal(new CuePlaybackIntent(false, false), pending.Complete(latest));
    }

    [Fact]
    public void RestartBeforeNativeStartSurvivesTheStageChangeAndLaterPauseWins()
    {
        var pending = new PendingCuePlayback();
        var request = pending.Begin(); pending.MarkPresented(request);
        pending.SetPaused(true); pending.RequestRestart();
        Assert.Equal(new CuePlaybackIntent(false, true), pending.Intent);
        pending.SetPaused(true);
        Assert.Equal(new CuePlaybackIntent(true, true), pending.Complete(request));
    }

    [Fact]
    public void CloseCancelsPresentationAndPlaybackCallbacksTogether()
    {
        var pending = new PendingCuePlayback();
        var request = pending.Begin(); pending.MarkPresented(request); pending.Cancel();
        pending.SetPaused(true); pending.RequestRestart();
        Assert.Null(pending.Complete(request)); Assert.False(pending.IsPending);
        Assert.Equal(default, pending.Intent);
    }

    [Fact]
    public void RequestedSettingsSurviveWorkspaceEditsAndTheNativeStartupDelay()
    {
        var pending = new PendingCuePlayback();
        var transitions = new TransitionOptions { Kind = "Slide", DurationMilliseconds = 900,
            AudioFadeOutMilliseconds = 1700, AudioFadeInMilliseconds = 800 };
        var style = new BibleVisualStyle { FontFamily = "Georgia", Transition = "Cut", FontSize = 80 };
        var request = pending.Begin(transitions, style);
        transitions.Kind = "Zoom"; transitions.DurationMilliseconds = 100;
        transitions.AudioFadeOutMilliseconds = 0; transitions.AudioFadeInMilliseconds = 0;
        style.FontFamily = "Arial"; style.Transition = "Dissolve"; style.FontSize = 40;
        pending.MarkPresented(request); pending.SetPaused(true);
        Assert.Equal("Slide", pending.Transitions.Kind);
        Assert.Equal(900, pending.Transitions.DurationMilliseconds);
        Assert.Equal(1700, pending.Transitions.AudioFadeOutMilliseconds);
        Assert.Equal(800, pending.Transitions.AudioFadeInMilliseconds);
        Assert.Equal("Georgia", pending.BibleStyle.FontFamily);
        Assert.Equal("Cut", pending.BibleStyle.Transition);
        Assert.Equal(80, pending.BibleStyle.FontSize);
        var copiedTransitions = pending.Transitions; copiedTransitions.Kind = "Cut";
        var copiedStyle = pending.BibleStyle; copiedStyle.FontFamily = "Arial";
        Assert.Equal("Slide", pending.Transitions.Kind); Assert.Equal("Georgia", pending.BibleStyle.FontFamily);
        Assert.Equal(new CuePlaybackIntent(true, false), pending.Complete(request));
    }

    [Fact]
    public void ReplacementCapturesNewNormalizedSettingsWithoutChangingThePreviousSnapshot()
    {
        var pending = new PendingCuePlayback();
        var old = pending.Begin(new TransitionOptions { Kind = "Slide" }, new BibleVisualStyle { FontFamily = "Georgia" });
        var previous = pending.Transitions;
        var latest = pending.Begin(new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = -1 },
            new BibleVisualStyle { FontFamily = "Arial", FontSize = double.NaN });
        Assert.Null(pending.Complete(old)); Assert.True(pending.Matches(latest));
        Assert.Equal("Slide", previous.Kind);
        Assert.Equal("Cut", pending.Transitions.Kind); Assert.Equal(0, pending.Transitions.AudioFadeInMilliseconds);
        Assert.Equal("Arial", pending.BibleStyle.FontFamily); Assert.Equal(76, pending.BibleStyle.FontSize);
    }
}
