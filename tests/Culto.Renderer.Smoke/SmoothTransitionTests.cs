using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    // Native/manual suite: compile normally; run explicitly with --smooth-transitions.
    // The fixture contains a moving test pattern and an audible synthetic tone.
    private static async Task RunSmoothTransitionChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 60, Loop = true };
        var window = new Window { Content = output, Width = 960, Height = 600, Topmost = true };
        window.Show(); window.Activate();
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        MediaPlayer Player(ProjectionSurface surface) => (MediaPlayer)typeof(ProjectionSurface)
            .GetField("_player", fields)!.GetValue(surface.ActiveSurface)!;
        CompositedVideo Frames(ProjectionSurface surface) => (CompositedVideo)typeof(ProjectionSurface)
            .GetField("_compositedVideo", fields)!.GetValue(surface.ActiveSurface)!;
        async Task Until(Func<bool> condition, string failure, int milliseconds = 10000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
            Check(condition(), failure);
        }
        Cue Video(string name) => new() { Kind = CueKind.Video, Title = name, FilePath = Path.Combine(Folder, "test.mkv") };
        async Task Start(Cue cue)
        {
            output.ShowCue(cue); output.SetPaused(false);
            await Until(() => !output.IsPreparingPlaybackEngine,
                "Cold native initialization completes without blocking the test dispatcher", 30000);
            await Until(() => output.IsPlaying && output.Position > .15 && Frames(output).Source is not null,
                "Synthetic video is playing before smooth transition checks");
        }
        try
        {
            var first = Video("First live video");
            var second = Video("Second live video");
            await Start(first);
            output.Transitions = new TransitionOptions { DurationMilliseconds = 1200,
                AudioFadeOutMilliseconds = 1200, AudioFadeInMilliseconds = 1200 };
            var outgoing = output.ActiveSurface;
            var oldPlayer = Player(outgoing);
            var oldFrames = Frames(outgoing);
            int framesPresented = 0;
            void OnFrame() => framesPresented++;
            oldFrames.FramePresented += OnFrame;
            try
            {
                output.TransitionTo(second);
                Check(oldPlayer.IsPlaying && Descendants(output).Contains(oldFrames),
                    "Loading incoming video keeps the outgoing live decoder visible and running");
                await Until(() => output.DisplayedCue?.Id == second.Id && output.IsTransitioning,
                    "Incoming video starts its blend once a frame is ready");
                var incoming = output.ActiveSurface;
                var newPlayer = Player(incoming);
                Check(!ReferenceEquals(oldPlayer, newPlayer), "A video change uses separate overlapping decoders");
                await Until(() => outgoing.Opacity is > .2 and < .8 && oldPlayer.Volume is > 5 and < 55
                    && newPlayer.Volume is > 5 and < 55,
                    "Image and both audio envelopes advance together during the same blend", 2000);
                var initialFrames = framesPresented;
                var oldPosition = oldPlayer.Time;
                await Until(() => framesPresented >= initialFrames + 3 && oldPlayer.Time != oldPosition,
                    "Outgoing frames and playback time keep advancing during the visual blend", 1000);
                Check(output.IsTransitioning && outgoing.Opacity > 0 && oldPlayer.IsPlaying
                    && Descendants(output).Contains(oldFrames),
                    "The outgoing video stays live in the compositor instead of becoming a frozen snapshot");
                await Until(() => !output.IsTransitioning, "Smooth video transition completes");
                Check(output.DisplayedCue?.Id == second.Id && output.IsPlaying,
                    "Completed transition leaves only the requested video playing");
            }
            finally { oldFrames.FramePresented -= OnFrame; }

            output.Transitions = new TransitionOptions();
            var beforeBlack = output.ActiveSurface;
            var retainedPlayer = Player(beforeBlack);
            output.TransitionToBackdrop(second, blackout: true, hideText: false);
            await Until(() => !output.IsTransitioning, "Black backdrop completes its transition");
            var retainedPosition = output.Position;
            Check(output.IsPaused && !retainedPlayer.IsPlaying
                && ReferenceEquals(retainedPlayer, Player(output)),
                "Black backdrop retains the same decoder and pauses it after the fade");
            output.TransitionToBackdrop(second, blackout: false, hideText: true);
            await Until(() => !output.IsTransitioning, "Switching from black to background completes");
            Check(ReferenceEquals(retainedPlayer, Player(output)) && output.IsPaused
                && Math.Abs(output.Position - retainedPosition) < .2,
                "Switching between backdrops retains the original paused video");
            output.TransitionToBackdrop(second, blackout: false, hideText: false);
            await Until(() => !output.IsTransitioning, "Restoring from black completes its transition");
            Check(ReferenceEquals(retainedPlayer, Player(output)) && output.IsPaused && !output.IsPlaying
                && Math.Abs(output.Position - retainedPosition) < .2,
                "Restoring black preserves the original decoder, position and paused state");
            output.SetPaused(false);
            await Until(() => output.IsPlaying && Player(output).Volume == 60,
                "Explicit resume restarts retained playback and restores audio");

            var errors = new List<string>(); output.PlaybackError += errors.Add;
            var beforeFailure = output.ActiveSurface;
            var beforeFailurePlayer = Player(output);
            output.TransitionTo(new Cue { Kind = CueKind.Video, Title = "Missing incoming video",
                FilePath = Path.Combine(Folder, "missing-smooth-" + Guid.NewGuid().ToString("N") + ".mkv") });
            await Until(() => errors.Count == 1 && !output.IsTransitioning,
                "Failed incoming media reports one error and ends its pending transition");
            Check(output.DisplayedCue?.Id == second.Id && ReferenceEquals(output.ActiveSurface, beforeFailure)
                && ReferenceEquals(Player(output), beforeFailurePlayer) && output.IsPlaying && Player(output).Volume == 60,
                "Incoming failure preserves the previous live video and restores its audio level");

            var superseded = Video("Superseded incoming");
            var newest = new Cue { Kind = CueKind.Text, Title = "Newest request", Body = "Latest request wins" };
            output.TransitionTo(superseded);
            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Intermediate request" });
            output.TransitionTo(newest);
            await Until(() => !output.IsTransitioning, "Rapid requests settle without leaving a pending transition");
            Check(output.DisplayedCue?.Id == newest.Id && !output.IsPlaying,
                "Only the newest rapid request remains on output");
            await Task.Delay(400);
            Check(output.DisplayedCue?.Id == newest.Id && !output.IsTransitioning,
                "Late decoder callbacks cannot replace the newest request");

            var transportA = Video("Transport A");
            var transportB = Video("Transport B");
            var transportC = Video("Queued transport C");
            await Start(transportA);
            output.Transitions = new TransitionOptions { DurationMilliseconds = 3000,
                AudioFadeOutMilliseconds = 250, AudioFadeInMilliseconds = 250 };
            output.TransitionTo(transportB);
            await Until(() => output.DisplayedCue?.Id == transportB.Id && output.Position > .45
                && output.IsTransitioning, "Long A-to-B blend has advanced before queuing C");
            var transportPlayer = Player(output);
            output.TransitionTo(transportC);
            output.SetPaused(true);
            Check(output.IsPaused && transportPlayer.IsPlaying,
                "Pause updates the queued cue state without pausing the currently blending video");
            output.SetPaused(false);
            Check(!output.IsPaused && transportPlayer.IsPlaying,
                "Resume updates the queued cue state while the current blend continues");
            output.SetPaused(true);
            var transportPosition = transportPlayer.Time;
            output.Restart();
            Check(!output.IsPaused, "Restart clears the queued cue pause request");
            output.Seek(.95);
            await Task.Delay(250);
            Check(output.DisplayedCue?.Id == transportB.Id && output.IsTransitioning
                && ReferenceEquals(Player(output), transportPlayer)
                && transportPlayer.Time >= transportPosition - 100 && transportPlayer.Time < 2200,
                "Restart and seek for pending C do not restart or seek currently blending B");
            await Until(() => !output.IsTransitioning && output.DisplayedCue?.Id == transportC.Id && output.IsPlaying,
                "Queued C becomes the settled output after transport commands");
            Check(!output.IsPaused && output.IsPlaying, "Queued restart leaves C playing when its turn arrives");

            output.Transitions = new TransitionOptions();
            var reselected = Video("Reselect current video");
            await Start(reselected);
            await Until(() => output.Position > .6, "Current video advances before being selected again");
            output.SetPaused(true);
            await Until(() => !output.IsPlaying, "Current video pauses before reselecting it");
            var reselectedPlayer = Player(output);
            var reselectedPosition = output.Position;
            output.TransitionTo(reselected);
            await Until(() => output.IsPlaying && !output.IsPaused, "Selecting the current video resumes playback");
            await Task.Delay(250);
            Check(!output.IsTransitioning && ReferenceEquals(Player(output), reselectedPlayer)
                && output.Position >= reselectedPosition - .1,
                "Selecting the current cue keeps its decoder and playback position");

            output.Loop = false;
            await Start(Video("Natural ending"));
            await Until(() => !output.IsPlaying && output.IsPaused,
                "A naturally ended video is reported as paused by the smooth compositor");
            output.Restart();
            await Until(() => output.IsPlaying && !output.IsPaused,
                "Restarting an ended video clears its paused state");
        }
        finally { window.Close(); }
    }
}
