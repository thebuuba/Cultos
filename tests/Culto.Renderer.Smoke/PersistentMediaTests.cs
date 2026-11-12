using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    private static async Task RunPersistentMediaChecks()
    {
        // This suite measures native volume over several foreground changes.
        // Extend the repository's synthetic clip so audio EOF cannot drain its
        // native output while the test is still adjusting that output's gain.
        var directory = Path.Combine(Path.GetTempPath(), "CultoPersistentMedia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var videoPath = Path.Combine(directory, "persistent.mkv");
        var start = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-stream_loop", "9", "-i",
            Path.Combine(Folder, "test.mkv"), "-c", "copy", "-t", "30", videoPath }) start.ArgumentList.Add(argument);
        using (var fixture = Process.Start(start)!)
        {
            var errors = await fixture.StandardError.ReadToEndAsync();
            await fixture.WaitForExitAsync();
            Check(fixture.ExitCode == 0, "A thirty-second synthetic native fixture is available: " + errors);
        }
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 60, Loop = true };
        var window = new Window { Content = output, Width = 800, Height = 450, Topmost = true };
        window.Show(); window.Activate();
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var keep = typeof(ProjectionSurface).GetProperty("KeepMediaAudio");
        var retained = typeof(ProjectionSurface).GetProperty("RetainedMediaCue");
        var background = typeof(ProjectionSurface).GetProperty("HasBackgroundMedia");
        MediaPlayer Player(ProjectionSurface source) => (MediaPlayer)typeof(ProjectionSurface).GetField("_player", fields)!.GetValue(source)!;
        bool Background() => (bool)background!.GetValue(output)!;
        async Task Until(Func<bool> predicate, string label, int timeout = 10000)
        {
            var clock = Stopwatch.StartNew();
            while (!predicate() && clock.ElapsedMilliseconds < timeout) await Task.Delay(10);
            Check(predicate(), label);
        }
        try
        {
            Check(keep is not null && retained is not null && background is not null,
                "Persistent media mode exposes playback retention and return APIs");
            Check(!(bool)keep!.GetValue(output)!, "Persistent audio is opt-in");
            keep.SetValue(output, true);
            output.Transitions = new TransitionOptions { DurationMilliseconds = 250,
                AudioFadeOutMilliseconds = 1200, AudioFadeInMilliseconds = 250 };
            var video = new Cue { Kind = CueKind.Video, Title = "Persistent synthetic video", FilePath = videoPath };
            output.TransitionTo(video);
            await Until(() => !output.IsPreparingPlaybackEngine && !output.IsTransitioning && output.IsPlaying && output.Position > .2,
                "Persistent video starts normally", 30000);
            var original = output.ActiveSurface;
            var player = Player(original);
            await Until(() => player.Volume == 60 && !player.Mute, "Persistent video reaches native master volume");
            var copy = (Cue)retained!.GetValue(output)!;
            copy.FilePath = "modified copy";
            Check(((Cue)retained.GetValue(output)!).FilePath == video.FilePath, "Returned media cue is a defensive copy");

            async Task Overlay(Cue? foreground, bool black = false, bool hidden = false)
            {
                output.Seek(.02); await Task.Delay(150);
                var position = player.Time;
                if (black || hidden) output.TransitionToBackdrop(foreground ?? video, black, hidden);
                else output.TransitionTo(foreground!);
                bool audibleThroughout = true;
                var clock = Stopwatch.StartNew();
                while (output.IsTransitioning && clock.ElapsedMilliseconds < 4000)
                {
                    audibleThroughout &= player.IsPlaying && player.Volume == 60 && !player.Mute;
                    await Task.Delay(10);
                }
                await Task.Delay(150);
                Check(!output.IsTransitioning && Background() && ReferenceEquals(output.ActiveSurface, original),
                    $"{(black ? "Black" : hidden ? "Logo" : foreground!.Kind)} foreground retains the live video source");
                Check(audibleThroughout && player.IsPlaying && player.Volume == 60 && !player.Mute && player.Time >= position + 200,
                    "Foreground transition preserves native audio volume and advancing media time");
                Check(!output.IsPaused && output.IsPlaying, "Hidden media remains playing in transport state");
                var sameFile = new Cue { Kind = CueKind.Video, Title = "Copied video cue", FilePath = Path.Combine(directory, ".", "persistent.mkv") };
                var returnPosition = player.Time;
                output.TransitionTo(sameFile);
                bool returnAudible = true;
                while (output.IsTransitioning)
                {
                    returnAudible &= player.IsPlaying && player.Volume == 60 && !player.Mute;
                    await Task.Delay(10);
                }
                Check(!Background() && ReferenceEquals(output.ActiveSurface, original) && ReferenceEquals(Player(output.ActiveSurface), player)
                    && player.Time >= returnPosition - 100 && returnAudible,
                    "Returning through a copied cue reuses the decoder without restarting or fading its audio");
            }
            await Overlay(new Cue { Kind = CueKind.Text, Body = "Foreground text" });
            await Overlay(new Cue { Kind = CueKind.Bible, Body = "Foreground verse", Reference = "Synthetic 1:1" });
            await Overlay(new Cue { Kind = CueKind.Image, FilePath = Path.Combine(Folder, "panorama.png") });
            await Overlay(video, black: true);
            await Overlay(video, hidden: true);

            output.TransitionToBackdrop(video, blackout: true, hideText: false);
            await Until(() => !output.IsTransitioning && Background(), "Failure recovery starts with a black foreground and live background");
            int errors = 0; output.PlaybackError += _ => errors++;
            output.TransitionTo(new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "missing-persistent-video.mkv") });
            await Until(() => errors == 1 && !output.IsTransitioning, "Missing replacement reports an error without losing the black foreground");
            Check(Background() && player.IsPlaying && player.Volume == 60 && !player.Mute && output.DisplayedBlackout,
                "Failed replacement preserves the hidden live decoder and its audible volume");
            output.TransitionTo(video);
            await Until(() => !output.IsTransitioning && !Background(), "Retained source can return after a failed replacement");

            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "First overlay" });
            var latest = new Cue { Kind = CueKind.Image, FilePath = Path.Combine(Folder, "panorama.png") };
            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Superseded overlay" });
            output.TransitionTo(latest);
            await Until(() => !output.IsTransitioning && output.DisplayedCue?.Id == latest.Id,
                "Rapid foreground requests retain only the newest overlay");
            Check(Background() && ReferenceEquals(output.ActiveSurface, original) && player.IsPlaying && player.Volume == 60,
                "Replacing foreground overlays keeps one original live media source");
            output.Volume = 35;
            await Until(() => Math.Abs(player.Volume - 35) <= 1 && !player.Mute,
                $"Master volume updates the retained native player behind an image (native={player.Volume}, child={original.Volume}, master={output.Volume})", 500);
            output.Volume = 60;
            output.TransitionTo(new Cue { Kind = CueKind.Audio, FilePath = Path.Combine(Folder, "audio.wav") });
            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Cancel incoming audio" });
            output.TransitionToBackdrop(video, blackout: true, hideText: false);
            await Until(() => !output.IsTransitioning && output.DisplayedBlackout,
                "A pending replacement can be superseded by a black foreground");
            Check(Background() && ReferenceEquals(output.ActiveSurface, original) && player.IsPlaying && player.Volume == 60,
                "Cancelling a replacement keeps the original background audible");
            output.TransitionTo(video);
            await Until(() => !output.IsTransitioning && !Background(), "Original media returns after rapid cancellation");

            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Release background" });
            await Until(() => !output.IsTransitioning && Background(), "Video is retained before disabling audio persistence");
            var setterClock = Stopwatch.StartNew(); keep.SetValue(output, false);
            Check(setterClock.ElapsedMilliseconds < 100 && player.IsPlaying && player.Volume > 0,
                "Disabling persistence schedules its audio fade without waiting on the dispatcher");
            await Until(() => player.Volume is > 5 and < 59, "Disabling persistence fades the native background volume", 1500);
            await Until(() => !Background() && retained.GetValue(output) is null,
                "Disabled background retires after its configured fade", 3000);
            Check(setterClock.ElapsedMilliseconds >= 1000 && setterClock.ElapsedMilliseconds < 2500,
                "Disabling persistence honors the configured 1200 ms audio fade");

            keep.SetValue(output, true);
            output.TransitionTo(video);
            await Until(() => !output.IsTransitioning && output.IsPlaying, "Media can start after background retirement");
            var replaced = Player(output.ActiveSurface);
            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Replace pinned source" });
            await Until(() => !output.IsTransitioning && Background(), "Replacement begins behind a foreground");
            var audio = new Cue { Kind = CueKind.Audio, FilePath = Path.Combine(Folder, "audio.wav") };
            output.TransitionTo(audio);
            await Until(() => output.DisplayedCue?.Id == audio.Id && output.IsTransitioning, "Different media starts its replacement crossfade");
            var replacement = Player(output.ActiveSurface);
            await Until(() => replaced.Volume is > 0 and < 60 && replacement.Volume is > 0 and <= 60,
                "Pinned source and replacement use the configured audio crossfade", 1500);
            await Until(() => !output.IsTransitioning && !Background() && output.IsPlaying,
                "Replacement retires the old pinned source and leaves one live medium");
            output.Seek(.1); await Task.Delay(150);
            var audioPosition = replacement.Time;
            output.TransitionTo(new Cue { Kind = CueKind.Text, Body = "Audio-only background" });
            await Until(() => !output.IsTransitioning && Background(), "Audio-only media can remain behind foreground text");
            Check(replacement.IsPlaying && replacement.Volume == 60 && replacement.Time > audioPosition,
                "Audio-only background keeps advancing at native master volume");
            output.TransitionTo(new Cue { Kind = CueKind.Audio, FilePath = audio.FilePath });
            await Until(() => !output.IsTransitioning && !Background(), "Copied audio cue returns from the background");
            Check(ReferenceEquals(Player(output.ActiveSurface), replacement) && replacement.IsPlaying && replacement.Volume == 60,
                "Returning audio also reuses the original live decoder");
        }
        finally { output.Dispose(); window.Close(); Directory.Delete(directory, recursive: true); }
    }
}
