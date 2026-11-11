using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    static async Task RunAudioFadeChecks(ProjectionSurface output, ProjectionSurface preview, Cue video, Cue audio)
    {
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        MediaPlayer Player(ProjectionSurface surface) => (MediaPlayer)typeof(ProjectionSurface).GetField("_player", fields)!.GetValue(surface)!;
        bool Silent(ProjectionSurface surface) => surface.IsPaused
            && (double)typeof(ProjectionSurface).GetField("_audioGain", fields)!.GetValue(surface)! == 0
            && (Player(surface).Volume == 0 || (Player(surface).Volume < 0 && !surface.IsPlaying
                && Player(surface).State is LibVLCSharp.Shared.VLCState.NothingSpecial or LibVLCSharp.Shared.VLCState.Stopped));
        string? MediaKey(ProjectionSurface surface) => (string?)typeof(ProjectionSurface).GetField("_mediaKey", fields)!.GetValue(surface);
        bool Current(ProjectionSurface surface, Cue cue) => MediaKey(surface)?.StartsWith(cue.Id.ToString() + "|", StringComparison.Ordinal) == true;
        Cue Clip(string title, bool useVideo = false) => new()
        {
            Title = title, Kind = useVideo ? CueKind.Video : CueKind.Audio,
            FilePath = useVideo ? video.FilePath : audio.FilePath
        };
        async Task UntilAudio(Func<bool> condition, string failure)
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
            if (!condition()) throw new Exception("FAIL: " + failure);
        }
        async Task StartClip(Cue cue, int volume = 80)
        {
            output.ShowCue(null); output.Volume = volume; output.ShowCue(cue);
            await UntilAudio(() => Current(output, cue) && output.IsPlaying && Player(output).Volume == volume,
                "Fixture did not start and settle at the requested native volume");
        }

        output.Loop = false;
        var old = Clip("Audio saliente"); var incoming = Clip("Audio entrante");
        await StartClip(old);
        output.TransitionTo(incoming);
        Check(Current(output, old), "Audio transition retains the outgoing clip while fading it out");
        var outgoingVolumes = new List<int>(); var incomingVolumes = new List<int>();
        await UntilAudio(() =>
        {
            var nativeVolume = Player(output).Volume;
            if (Current(output, old)) outgoingVolumes.Add(nativeVolume);
            if (Current(output, incoming)) incomingVolumes.Add(nativeVolume);
            return Current(output, incoming) && output.IsPlaying && nativeVolume == 80;
        }, "Incoming audio never reached the requested native volume");
        Check(outgoingVolumes.Any(v => v > 0 && v < 80), "Outgoing native audio passes through intermediate volume before replacement");
        Check(incomingVolumes.Any(v => v > 0 && v < 80), "Incoming native audio fades through intermediate volume to the target");

        old = Clip("Audio antes de cambiar volumen"); incoming = Clip("Audio con volumen ajustado");
        await StartClip(old);
        output.TransitionTo(incoming);
        await UntilAudio(() => Current(output, incoming) && output.IsPlaying && Player(output).Volume is > 0 and < 80,
            "Incoming audio did not expose a fade interval for volume adjustment");
        output.Volume = 35;
        Check(Player(output).Volume is >= 0 and <= 35, "Changing target volume during a fade immediately respects the new limit");
        await UntilAudio(() => output.IsPlaying && Player(output).Volume == 35, "Fade restored an obsolete volume target");
        Check(output.Volume == 35 && Player(output).Volume == 35, "Incoming fade finishes at the latest requested volume");

        old = Clip("Audio antes de cambios rápidos");
        var skipped = Clip("Audio reemplazado"); var latest = Clip("Último audio");
        await StartClip(old, 60);
        output.TransitionTo(skipped); await Task.Delay(35); output.TransitionTo(latest);
        await UntilAudio(() => Current(output, latest) && output.IsPlaying && Player(output).Volume == 60,
            "Rapid replacement did not settle on the latest cue");
        await Task.Delay(500);
        Check(Current(output, latest) && output.IsPlaying && Player(output).Volume == 60,
            "A cancelled audio transition cannot replace or silence the newest cue later");

        old = Clip("Audio antes de negro"); incoming = Clip("Video interrumpido por negro", useVideo: true);
        await StartClip(old, 60);
        output.TransitionTo(incoming); await Task.Delay(35);
        output.ShowCue(incoming, blackout: true);
        Check(Silent(output), $"Blackout immediately silences native audio during a pending transition (volume={Player(output).Volume}, mute={Player(output).Mute}, state={Player(output).State})");
        await Task.Delay(650);
        Check(!output.IsPlaying && Silent(output) && !output.IsTransitioning,
            "Pending audio and video callbacks cannot restore playback after blackout");
        output.ShowCue(incoming); await Task.Delay(120);
        Check(!output.IsPlaying && Silent(output), "Restoring blackout keeps media paused and silent");
        output.SetPaused(false);
        await UntilAudio(() => output.IsPlaying && Player(output).Volume == 60, "Explicit resume failed after an interrupted transition");
        Check(Current(output, incoming), "Explicit resume plays the requested clip after blackout");

        old = Clip("Audio antes de pausa"); incoming = Clip("Audio pendiente al pausar");
        await StartClip(old, 60);
        output.TransitionTo(incoming); await Task.Delay(35); output.SetPaused(true);
        await Task.Delay(500);
        Check(Current(output, incoming) && !output.IsPlaying && Silent(output),
            "Pause during an outgoing fade commits the requested cue paused without residual audio");
        output.SetPaused(false);
        var resumedVolumes = new List<int>();
        await UntilAudio(() =>
        {
            resumedVolumes.Add(Player(output).Volume);
            return output.IsPlaying && Player(output).Volume == 60;
        }, "Paused pending cue failed to resume at target volume");
        Check(resumedVolumes.Any(v => v > 0 && v < 60), "Explicit resume fades native audio back in");
        output.SetPaused(true);

        preview.Volume = 100;
        preview.TransitionTo(Clip("Vista previa silenciosa", useVideo: true));
        await UntilAudio(() => preview.IsPlaying, "Preview fixture failed to start");
        await Task.Delay(500);
        Check(preview.Volume == 0 && Player(preview).Volume <= 0 && (Player(preview).Mute || Player(preview).AudioTrack == -1),
            "Preview remains inaudible even when a transition and target-volume change request audio");
        preview.SetPaused(true);

        var pending = new ProjectionSurface { Width = 320, Height = 180, Volume = 60 };
        try
        {
            pending.Measure(new Size(320, 180)); pending.Arrange(new Rect(0, 0, 320, 180));
            pending.ShowCue(Clip("Audio a cerrar"));
            await UntilAudio(() => pending.IsPlaying && Player(pending).Volume == 60, "Disposal fixture failed to start");
            pending.TransitionTo(Clip("Audio pendiente al cerrar"));
            pending.Dispose();
            await Task.Delay(650);
            Check(!pending.IsPlaying && pending.Position == 0 && pending.Duration == 0 && !pending.IsTransitioning,
                "Disposing a pending audio transition cannot restart playback or retain a dissolve");
        }
        finally { pending.Dispose(); }
    }
}
