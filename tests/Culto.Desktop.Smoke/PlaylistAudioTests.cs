using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using NAudio.CoreAudioApi;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    private static void RunPlaylistAudioTests(MainWindow window)
    {
        var folder = Path.Combine(Environment.CurrentDirectory, ".tools", "playlist-audio"); Directory.CreateDirectory(folder);
        var fixture = Path.Combine(folder, "tone-video.mp4");
        if (!File.Exists(fixture))
        {
            var start = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=teal:s=320x180:r=25", "-f", "lavfi", "-i", "sine=frequency=997:sample_rate=48000", "-t", "12", "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-y", fixture }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!; var error = process.StandardError.ReadToEnd(); process.WaitForExit();
            Check(process.ExitCode == 0, "Could not generate audio/video fixture: " + error);
        }
        using var devices = new MMDeviceEnumerator();
        using var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        double Peak()
        {
            device.AudioSessionManager.RefreshSessions();
            var peak = 0d; var sessions = device.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];
                if (session.GetProcessID == Environment.ProcessId) peak = Math.Max(peak, session.AudioMeterInformation.MasterPeakValue);
            }
            return peak;
        }
        void Audible(ProjectionSurface output, string phase)
        {
            var peak = 0d; var end = DateTime.UtcNow.AddSeconds(8); DateTime? readyAt = null;
            while (DateTime.UtcNow < end)
            {
                var activePlayer = Field<MediaPlayer?>(output, "_player");
                var surface = output.ActiveSurface;
                var routedAudio = surface.GetType().GetField("_cathedralAudio", Private)!.GetValue(surface);
                var nativeReady = routedAudio is not null ? !(bool)routedAudio.GetType().GetProperty("HasError")!.GetValue(routedAudio)! : activePlayer?.Mute == false && activePlayer.Volume > 0;
                var ready = !output.IsTransitioning && output.IsPlaying && nativeReady
                    && !Field<bool>(output, "_awaitingAudioStart");
                if (ready)
                {
                    readyAt ??= DateTime.UtcNow; peak = Math.Max(peak, Peak());
                    if (DateTime.UtcNow - readyAt.Value > TimeSpan.FromMilliseconds(350) && peak > .0001) break;
                }
                else { readyAt = null; peak = 0; }
                PumpFor(40);
            }
            var player = Field<MediaPlayer?>(output, "_player");
            var cathedral = output.ActiveSurface.GetType().GetField("_cathedralAudio", Private)!.GetValue(output.ActiveSurface);
            if (cathedral is not null) Console.WriteLine("PCM peak=" + cathedral.GetType().GetProperty("InputPeak")!.GetValue(cathedral));
            Console.WriteLine($"AUDIO {phase}: peak={peak:0.000000} playing={output.IsPlaying} transition={output.IsTransitioning} position={output.Position:0.00} master={output.Volume} native={player?.Volume} mute={player?.Mute} awaiting={Field<bool>(output,"_awaitingAudioStart")} gain={Field<double>(output,"_audioGain"):0.00} mix={Field<double>(output,"_mixGain"):0.00}");
            Check(readyAt is not null && DateTime.UtcNow - readyAt.Value > TimeSpan.FromMilliseconds(350)
                && peak > .0001 && output.IsPlaying && !output.IsTransitioning, phase + " lost emitted audio");
            Check(peak < .1, phase + " produced an unexpected gain burst or invalid PCM format");
        }
        var workspace = Field<WorkspaceData>(window, "_workspace"); var sessionState = Field<PresentationSession>(window, "_session");
        var items = Field<ListBox>(window, "_videoListItems"); var volume = (Slider)window.FindName("VolumeSlider"); volume.Value = 35;
        Cue Clip(string title) => new() { Kind = CueKind.Video, FilePath = fixture, Title = title };
        var effects = Environment.GetCommandLineArgs().Contains("--cathedral-playlist") ? new[] { "Cathedral" } : new[] { "Dissolve", "Move" };
        foreach (var effect in effects)
        Test("Playlist keeps emitted audio through add/play/advance/exit with " + effect, () =>
        {
            try
            {
            workspace.Transitions = new TransitionOptions { Kind = effect == "Cathedral" ? "Dissolve" : effect,
                AudioFadeEffect = effect == "Cathedral" ? "Cathedral" : "Normal", CathedralTailMilliseconds = 1200,
                DurationMilliseconds = 950, AudioFadeOutMilliseconds = 2800, AudioFadeInMilliseconds = 2550 };
            var external = Clip("External baseline"); Call(window, "PrepareCue", external, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output"); Audible(output, "external baseline " + effect);
            var a = Clip("Playlist A"); var b = Clip("Playlist B");
            Call(window, "AppendToVideoQueue", a); Call(window, "AppendToVideoQueue", b);
            Audible(output, "after append " + effect);
            var list = workspace.VideoCollections.Single(); items.SelectedItem = list.Items[0]; Call(window, "PlaySelectedVideoListItem");
            Audible(output, "playlist A " + effect);
            var expected = list.Items[1].Id; output.Seek(.98);
            Until(() => sessionState.Live?.Id == expected, "Playlist did not automatically advance"); Audible(output, "automatic playlist B " + effect);
            var later = Clip("Outside playlist"); Call(window, "PrepareCue", later, null); Call(window, "ProjectPrepared"); Audible(output, "outside playlist " + effect);
            Call(window, "PrepareCue", Clip("Silent preview"), null); PumpFor(200); Audible(output, "after silent preview " + effect);
            foreach (var index in new[] { 0, 1, 0, 1 })
            { items.SelectedItem = list.Items[index]; Call(window, "PlaySelectedVideoListItem"); PumpFor(80); }
            Audible(output, "after rapid playlist changes " + effect);
            output.SetPaused(true); PumpFor(100); output.SetPaused(false); Audible(output, "after pause/resume " + effect);
            Call(window, "PrepareCue", Clip("After rapid playlist"), null); Call(window, "ProjectPrepared"); Audible(output, "exit rapid playlist " + effect);
            output.Seek(.98); Until(() => output.HasEnded || sessionState.IsBlackout, "Outside video did not end");
            Until(() => sessionState.IsBlackout && !output.IsTransitioning, "End of video did not settle on black");
            items.SelectedItem = list.Items[0]; Call(window, "PlaySelectedVideoListItem"); Audible(output, "restart after EOF " + effect);
            }
            finally { Call(window, "CloseOutput"); workspace.VideoCollections.Clear(); Call(window, "RefreshVideoLists", new object?[] { null }); }
        });
        volume.Value = 0; Call(window, "CloseOutput");
    }
}
