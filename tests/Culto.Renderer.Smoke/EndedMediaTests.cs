using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    private static async Task RunEndedMediaChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true)
        {
            Volume = 20, Loop = false,
            BackgroundImagePath = Path.Combine(Folder, "panorama.png"),
            Transitions = new TransitionOptions { Kind = "Cut", DurationMilliseconds = 0,
                AudioFadeOutMilliseconds = 0, AudioFadeInMilliseconds = 0 }
        };
        var window = new Window { Content = output, Width = 800, Height = 450, Topmost = true };
        window.Show(); window.Activate();
        async Task Until(Func<bool> predicate, string label, int timeout = 10000)
        {
            var clock = Stopwatch.StartNew();
            while (!predicate() && clock.ElapsedMilliseconds < timeout) await Task.Delay(10);
            Check(predicate(), label);
        }
        try
        {
            var hymn = new Cue { Kind = CueKind.Video, Title = "Hymn from worship order", FilePath = Path.Combine(Folder, "test.mkv") };
            output.TransitionTo(hymn);
            await Until(() => !output.IsTransitioning && output.IsPlaying, "Hymn starts before a one-shot visual action", 30000);
            var player = (MediaPlayer)typeof(ProjectionSurface).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(output.ActiveSurface)!;
            int restarts = 0;
            player.Playing += (_, _) => Interlocked.Increment(ref restarts);
            await Until(() => output.HasEnded, "Native hymn reaches its natural end");
            output.TransitionVisual(hymn, blackout: true, hideText: false);
            await Until(() => !output.IsTransitioning && output.DisplayedBlackout, "Finished hymn automatically transitions to black");
            output.TransitionVisual(hymn, blackout: false, hideText: true);
            await Until(() => !output.IsTransitioning && output.DisplayedHidden, "Logo follows the automatic black screen");
            output.Volume = 80;
            var reading = new Cue { Kind = CueKind.Bible, Title = "Reading from worship order", Body = "Synthetic verse", Reference = "Test 1:1" };
            output.TransitionVisual(reading);
            await Until(() => !output.IsTransitioning && output.DisplayedCue?.Id == reading.Id, "Bible reading replaces Logo");
            // Committing a cue from the worship order applies its unpaused intent.
            output.SetPaused(false);
            await Task.Delay(350);
            Check(Volatile.Read(ref restarts) == 0 && !output.IsPlaying,
                "Projecting the reading never resumes or restarts the ended hymn");
            Check(!output.HasBackgroundMedia && output.RetainedMediaCue is null,
                "Ended hymn is not retained as persistent background audio");

            output.TransitionTo(hymn);
            await Until(() => !output.IsTransitioning && output.IsPlaying, "Hymn can be explicitly projected again after ending");
            var backgroundSurface = output.ActiveSurface;
            output.TransitionVisual(reading);
            await Until(() => !output.IsTransitioning && output.HasBackgroundMedia,
                "A genuinely playing hymn continues behind a Bible reading");
            await Until(() => backgroundSurface.HasEnded, "Background hymn also reaches its natural end");
            Check(!output.HasBackgroundMedia && output.RetainedMediaCue is null,
                "Background hymn stops being offered as retained audio at EOF");
            output.SetPaused(false);
            output.TransitionVisual(new Cue { Kind = CueKind.Bible, Body = "Next verse", Reference = "Test 1:2" });
            await Until(() => !output.IsTransitioning, "Next Bible verse follows an ended background hymn");
            Check(!output.IsPlaying && !output.HasBackgroundMedia && output.RetainedMediaCue is null,
                "Subsequent foreground transport cannot revive a hymn that ended in the background");
        }
        finally { output.Dispose(); window.Close(); }
    }
}
