using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    // Native Volume is fixed at 100 for the callback sink; inspect its requested
    // PCM envelope here. TransportOutputTests separately measures emitted audio.
    private static int RequestedAudioVolume(ProjectionSurface surface)
    {
        surface = surface.ActiveSurface;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        if (surface.CathedralAudio is { } pcm)
        {
            var gain = (double)typeof(CathedralAudioOutput).GetField("_gain", fields)!.GetValue(pcm)!;
            return (int)Math.Round(Math.Cbrt(gain) * 100);
        }
        return ((MediaPlayer)typeof(ProjectionSurface).GetField("_player", fields)!.GetValue(surface)!).Volume;
    }

    private static async Task RunTransportFadeChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true)
        {
            Volume = 70, Loop = true,
            Transitions = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 }
        };
        var host = new Window { Content = output, Width = 640, Height = 400, Topmost = true };
        host.Show(); host.Activate();
        output.PlaybackError += message => Results.Add("Playback error: " + message);
        async Task Until(Func<bool> predicate, string label)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(10);
            Check(predicate(), label);
        }
        try
        {
            output.ShowCue(new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") });
            await Until(() => !output.IsTransitioning && output.IsPlaying, "Transport fixture starts");
            var player = (MediaPlayer)typeof(ProjectionSurface).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output.ActiveSurface)!;
            await Until(() => RequestedAudioVolume(output) == 70, "Transport fixture settles at master volume");
            output.SetPausedWithFade(true);
            Check(output.IsPaused && player.IsPlaying && RequestedAudioVolume(output) > 0, "Pause intent is immediate while the decoder releases audio");
            await Until(() => RequestedAudioVolume(output) > 0 && RequestedAudioVolume(output) < 70, "Pause passes through intermediate gain");
            await Until(() => !player.IsPlaying && RequestedAudioVolume(output) == 0, "Pause finishes silent");
            output.SetPausedWithFade(false);
            await Until(() => player.IsPlaying && RequestedAudioVolume(output) > 0 && RequestedAudioVolume(output) < 70, "Resume passes through intermediate gain");
            await Until(() => RequestedAudioVolume(output) == 70, "Resume restores master volume");
            output.SeekWithFade(.5);
            await Until(() => RequestedAudioVolume(output) > 0 && RequestedAudioVolume(output) < 70, "Seeking fades before changing position");
            await Until(() => output.Position > 1.3 && RequestedAudioVolume(output) == 70, "Seeking restores playback at its new position");
            output.SeekWithFade(.2); output.SeekWithFade(.6); output.SetPausedWithFade(true);
            output.Seek(.3);
            await Until(() => !player.IsPlaying && RequestedAudioVolume(output) == 0, "Pause supersedes queued seeks");
            output.SeekWithFade(.25);
            await Task.Delay(250);
            Check(output.IsPaused && !player.IsPlaying && RequestedAudioVolume(output) == 0, "Seeking while paused stays silent");
            output.SetPausedWithFade(false); await Task.Delay(40); output.SetPausedWithFade(true);
            await Until(() => !player.IsPlaying && RequestedAudioVolume(output) == 0, "Rapid resume and pause keep the latest intent");
            output.SetPausedWithFade(false);
            await Until(() => RequestedAudioVolume(output) == 70, "Rapid commands do not leave audio muted");
            output.SeekWithFade(.7);
            output.Volume = 35;
            await Until(() => RequestedAudioVolume(output) == 35, "Changing volume during a seek preserves the new master level");
            output.SeekWithFade(.2);
            output.ShowCue(new Cue { Kind = CueKind.Text, Body = "Replacement" });
            await Task.Delay(400);
            Check(!output.IsPlaying && output.AudioCue is null, "Replacing a cue cancels transport callbacks");
            Check(output.Volume == 35, "Transport fades preserve the operator volume");
            output.TransitionTo(new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") });
            await Until(() => !output.IsTransitioning && output.IsPlaying, "Background fixture starts");
            output.TransitionVisual(new Cue { Kind = CueKind.Text, Body = "Reading" });
            await Until(() => !output.IsTransitioning && output.HasBackgroundMedia, "Reading retains background media");
            var foreground = output.DisplayedCue!.Id;
            player = (MediaPlayer)typeof(ProjectionSurface).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output.ActiveSurface)!;
            output.SetPausedWithFade(true);
            await Until(() => RequestedAudioVolume(output) > 0 && RequestedAudioVolume(output) < 35, "Background pause passes through intermediate gain");
            await Until(() => !player.IsPlaying && RequestedAudioVolume(output) == 0, "Background audio pauses with a fade");
            output.SetPausedWithFade(false);
            await Until(() => RequestedAudioVolume(output) == 35, "Background audio resumes at the chosen volume");
            output.SeekWithFade(.4);
            await Until(() => output.Position >= 1.1 && RequestedAudioVolume(output) == 35, "Background audio seeks with a fade");
            Check(output.DisplayedCue!.Id == foreground, "Background transport preserves the foreground");
            output.SetPausedWithFade(true);
            output.TransitionVisual(new Cue { Kind = CueKind.Text, Body = "Next reading" });
            await Until(() => !output.IsTransitioning && !player.IsPlaying, "Visual changes preserve pending background pause");
            Check(output.IsPaused, "Paused background intent survives a visual change");
            output.SetPausedWithFade(false);
            await Until(() => RequestedAudioVolume(output) == 35, "Background resumes after its visual changes");
            output.Volume = 0;
            output.SetPausedWithFade(true); await Task.Delay(550); output.SetPausedWithFade(false);
            await Task.Delay(550);
            output.SeekWithFade(.3); await Task.Delay(750);
            Check(RequestedAudioVolume(output) == 0 && output.Volume == 0, "Muted transport never restores audible volume");
        }
        finally { host.Close(); }
    }
}
