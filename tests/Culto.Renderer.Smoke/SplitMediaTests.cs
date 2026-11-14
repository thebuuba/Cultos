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
    private static async Task RunSplitMediaChecks()
    {
        await RunSplitLoadingChecks();
        var visual = typeof(ProjectionSurface).GetMethod("TransitionVisual");
        var audio = typeof(ProjectionSurface).GetMethod("PlayAudioKeepingVisual");
        var audioCue = typeof(ProjectionSurface).GetProperty("AudioCue");
        Check(visual is not null && audio is not null && audioCue is not null,
            "One-shot visual and audio actions expose independent channel APIs");
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 60 };
        var window = new Window { Content = output, Width = 800, Height = 450, Topmost = true };
        window.Show(); window.Activate();
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        MediaPlayer Player(ProjectionSurface source) => (MediaPlayer)typeof(ProjectionSurface).GetField("_player", fields)!.GetValue(source)!;
        async Task Until(Func<bool> predicate, string label, int timeout = 15000)
        {
            var clock = Stopwatch.StartNew();
            while (!predicate() && clock.ElapsedMilliseconds < timeout) await Task.Delay(10);
            Check(predicate(), label);
        }
        void Visual(Cue? cue, bool black = false, bool logo = false) => visual!.Invoke(output, [cue, black, logo]);
        void Audio(Cue cue) => audio!.Invoke(output, [cue]);
        Cue? Sounding() => (Cue?)audioCue!.GetValue(output);
        var video = new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") };
        var reading = new Cue { Kind = CueKind.Bible, Body = "Preserved reading", Reference = "Test 1:1" };
        var image = new Cue { Kind = CueKind.Image, FilePath = Path.Combine(Folder, "panorama.png") };
        try
        {
            output.Transitions = new TransitionOptions { DurationMilliseconds = 100, AudioFadeOutMilliseconds = 650, AudioFadeInMilliseconds = 650 };
            foreach (var mode in new[] { "Bible", "Image", "Logo", "Black" })
            {
                if (mode == "Bible") output.TransitionTo(reading);
                else if (mode == "Image") output.TransitionTo(image);
                else output.TransitionToBackdrop(null, mode == "Black", mode == "Logo");
                await Until(() => !output.IsTransitioning, mode + " visual settles");
                var displayed = output.DisplayedCue?.Id;
                Audio(video);
                bool preserved = true;
                var clock = Stopwatch.StartNew();
                while ((output.IsTransitioning || Sounding() is null) && clock.ElapsedMilliseconds < 30000)
                {
                    preserved &= output.DisplayedCue?.Id == displayed && output.DisplayedBlackout == (mode == "Black") && output.DisplayedHidden == (mode == "Logo");
                    await Task.Delay(10);
                }
                Check(preserved && Sounding()?.Id == video.Id && output.IsPlaying && output.HasBackgroundMedia,
                    mode + " remains visible throughout hidden video preparation and playback");
                var original = output.ActiveSurface; var player = Player(original);
                var position = player.Time;
                Visual(reading);
                await Until(() => !output.IsTransitioning, "One-shot reading transition settles");
                Check(ReferenceEquals(original, output.ActiveSurface) && player.IsPlaying && player.Time >= position && player.Volume == 60,
                    "Shift visual preserves the existing live decoder and full native audio volume");
                output.TransitionTo(video);
                await Until(() => !output.IsTransitioning && !output.HasBackgroundMedia, "Sounding file is revealed");
                Check(ReferenceEquals(original, output.ActiveSurface) && player.Time >= position,
                    "Reveal reuses the same decoder at its current position");
                Visual(mode == "Image" ? image : mode == "Bible" ? reading : null, mode == "Black", mode == "Logo");
                await Until(() => !output.IsTransitioning, mode + " Shift visual settles");
                Check(ReferenceEquals(original, output.ActiveSurface) && player.IsPlaying && player.Volume == 60,
                    mode + " Shift visual retains sounding decoder without fading");
                Visual(image);
                await Until(() => !output.IsTransitioning && output.HasBackgroundMedia, "Image takes over while audio continues");
                var replacement = new Cue { Kind = CueKind.Audio, FilePath = Path.Combine(Folder, "audio.wav") };
                Audio(replacement);
                await Until(() => Sounding()?.Id == replacement.Id && output.IsTransitioning, "Hidden replacement starts crossfade");
                var nextSurface = output.ActiveSurface;
                var next = Player(nextSurface);
                await Until(() => player.Volume is > 0 and < 60 && next.Volume is > 0 and < 60,
                    "Hidden file replacement crossfades both native players");
                await Until(() => !output.IsTransitioning, "Hidden replacement completes");
                Check(output.DisplayedCue?.Id == image.Id && output.HasBackgroundMedia && Sounding()?.Id == replacement.Id,
                    "Replacing audio keeps the same visible image");
                var errors = 0; output.PlaybackError += _ => errors++;
                Audio(new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "missing-split.mkv") });
                await Until(() => errors > 0 && !output.IsTransitioning, "Invalid audio replacement reports failure");
                Check(output.DisplayedCue?.Id == image.Id && ReferenceEquals(output.ActiveSurface, nextSurface) && Sounding()?.Id == replacement.Id,
                    "Failed input preserves the image and existing audio source");
                output.TransitionTo(reading);
                await Until(() => !output.IsTransitioning && !output.HasBackgroundMedia, "Normal projection releases background audio");
                Check(Sounding() is null && !output.IsPlaying,
                    "One-shot actions do not enable persistent audio for a later normal projection");
            }
            Audio(video);
            await Until(() => !output.IsTransitioning && output.HasBackgroundMedia, "Background starts before a normal blackout");
            output.TransitionToBackdrop(null, blackout: true, hideText: false);
            await Until(() => !output.IsTransitioning && !output.HasBackgroundMedia, "Normal blackout releases background audio");
            Check(output.DisplayedBlackout && Sounding() is null, "Normal backdrop also replaces both channels");
            Audio(new Cue { Kind = CueKind.Audio, FilePath = Path.Combine(Folder, "audio.wav") });
            await Until(() => !output.IsTransitioning && output.HasBackgroundMedia, "Background starts before natural EOF");
            var ending = output.ActiveSurface;
            await Until(() => ending.HasEnded, "Independent audio channel reaches natural EOF", 30000);
            Check(Sounding() is null && !output.HasBackgroundMedia, "Ended independent channel is not offered as sounding audio");
            Visual(reading);
            await Until(() => !output.IsTransitioning, "Shift visual follows ended audio");
            output.SetPaused(false);
            await Task.Delay(200);
            Check(!output.IsPlaying && Sounding() is null && !output.HasBackgroundMedia,
                "A later visual action and transport cannot restart ended independent audio");
        }
        finally { output.Dispose(); window.Close(); }
    }
    private static async Task RunSplitLoadingChecks()
    {
        foreach (var normalProjection in new[] { true, false })
        {
            using var engineGate = new System.Threading.ManualResetEventSlim();
            using var mixer = new ProjectionMixer(() =>
            {
                engineGate.Wait();
                return ProjectionSurface.CreatePlaybackEngine();
            });
            var window = new Window { Content = mixer, Width = 800, Height = 450, Topmost = true };
            window.Show(); window.Activate();
            try
            {
                mixer.Transitions = new TransitionOptions { Kind = "Cut", DurationMilliseconds = 0,
                    AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
                var reading = new Cue { Kind = CueKind.Bible, Body = "Keep this reading during loading" };
                mixer.TransitionTo(reading);
                var video = new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") };
                mixer.PlayAudioKeepingVisual(video);
                Check(mixer.IsPreparingPlaybackEngine, "Hidden file preparation is held before decoder creation");
                if (normalProjection) mixer.TransitionTo(video);
                else mixer.SetPaused(true);
                engineGate.Set();
                var clock = Stopwatch.StartNew();
                while (mixer.IsTransitioning && clock.ElapsedMilliseconds < 30000) await Task.Delay(10);
                Check(!mixer.IsTransitioning, "Held file preparation and transition complete");
                if (normalProjection)
                    Check(mixer.DisplayedCue?.Id == video.Id && !mixer.HasBackgroundMedia && mixer.IsPlaying,
                        "Normal projection supersedes a loading audio-only request for the same file");
                else
                    Check(mixer.DisplayedCue?.Id == reading.Id && mixer.HasBackgroundMedia && mixer.IsPaused && !mixer.IsPlaying,
                        "Pause requested during hidden file loading is applied before it becomes background audio");
            }
            finally { engineGate.Set(); mixer.Dispose(); window.Close(); }
        }
    }

}
