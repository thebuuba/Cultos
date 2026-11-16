using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Culto.Core;
using Culto.Desktop.Projection;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Cue = Culto.Core.Cue;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    // Exercises a live output-only refresh without changing the Windows default
    // or writing endpoint/session volume. Real USB hot-plug still needs hardware QA.
    private static async Task RunAudioDeviceRebindChecks()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, fields)!.GetValue(owner)!;
        async Task Until(Func<bool> condition, string message)
        {
            var timeout = Stopwatch.StartNew();
            while (!condition() && timeout.ElapsedMilliseconds < 8000) await Task.Delay(20);
            Check(condition(), message);
        }
        using var devices = new MMDeviceEnumerator();
        using var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        var windowsVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
        var windowsMuted = device.AudioEndpointVolume.Mute;
        var path = Path.Combine(Path.GetTempPath(), "CultoAudioRebind-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            // Silent fixture allows native sink tests without audible tones.
            using (var writer = new WaveFileWriter(path, new WaveFormat(48000, 16, 2)))
                writer.Write(new byte[48000 * 4 * 20], 0, 48000 * 4 * 20);
            foreach (var cathedral in new[] { false, true })
            {
                using var surface = new ProjectionSurface
                {
                    Volume = 37,
                    Transitions = new TransitionOptions { AudioFadeEffect = cathedral ? "Cathedral" : "None",
                        AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 }
                };
                surface.ShowCue(new Cue { Kind = CueKind.Audio, FilePath = path, Title = "Silent output refresh" });
                await Until(() => surface.IsPlaying && surface.Position > .3, "Native audio starts before refresh");
                var player = Field<MediaPlayer>(surface, "_player");
                var follower = cathedral
                    ? Field<DefaultAudioDeviceFollower>(Field<DefaultAudioWaveOutput>(Field<CathedralAudioOutput>(surface, "_cathedralAudio"), "_output"), "_follower")
                    : Field<DefaultAudioDeviceFollower>(surface, "_audioDeviceFollower");
                var position = surface.Position;
                follower.Invalidate(); follower.Refresh();
                await Task.Delay(650);
                Check(ReferenceEquals(player, Field<MediaPlayer>(surface, "_player")) && surface.IsPlaying
                    && surface.Position >= position && surface.Position < position + 2,
                    $"{(cathedral ? "PCM" : "DirectSound")} refresh retains decoder and playing position");
                Check(surface.Volume == 37 && (cathedral || player.Volume == 37 && !player.Mute), "Refresh retains app gain");
                surface.SetPaused(true);
                await Task.Delay(150);
                position = surface.Position;
                follower.Invalidate(); follower.Refresh();
                await Task.Delay(650);
                Check(surface.IsPaused && !surface.IsPlaying && Math.Abs(surface.Position - position) < .2,
                    "Output refresh preserves pause and paused position");
                surface.SetPaused(false);
                await Until(() => surface.IsPlaying && surface.Position > position + .2, "Playback resumes after paused refresh");
                if (cathedral) Check(!Field<CathedralAudioOutput>(surface, "_cathedralAudio").HasError, "PCM sink refresh does not permanently fail the provider");
            }
            Check(device.AudioEndpointVolume.MasterVolumeLevelScalar == windowsVolume && device.AudioEndpointVolume.Mute == windowsMuted,
                "Output refresh leaves Windows master volume and mute untouched");
        }
        finally { File.Delete(path); }
    }
}
