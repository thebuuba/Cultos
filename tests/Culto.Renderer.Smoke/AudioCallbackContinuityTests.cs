using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Culto.Desktop.Projection;
using LibVLCSharp.Shared;

internal static partial class Program
{
    private static void DetachCallbackDevice(CathedralAudioOutput pcm)
    {
        var sink = typeof(CathedralAudioOutput).GetField("_output", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((IDisposable?)sink.GetValue(pcm))?.Dispose(); sink.SetValue(pcm, null);
    }

    private static async Task RunTransportPcmChecks()
    {
        var folder = Path.GetFullPath(".tools/audio-noise");
        Directory.CreateDirectory(folder);
        var wav = Path.Combine(folder, "resampler-reference.wav"); WriteIsolationTone(wav, 440);
        var aac = Path.Combine(folder, "resampler-reference.m4a");
        var encode = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-y", "-i", wav, "-ar", "44100", "-c:a", "aac", "-b:a", "192k", aac }) encode.ArgumentList.Add(arg);
        using (var process = Process.Start(encode)!) { var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); Check(process.ExitCode == 0, "AAC transport fixture generated: " + await error); }
        var video = Path.Combine(folder, "transport-reference.mp4");
        var mux = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-y", "-stream_loop", "-1", "-i", Path.Combine(Folder, "test.mkv"), "-i", Path.Combine(folder, "resampler-reference.m4a"), "-map", "0:v:0", "-map", "1:a:0", "-t", "20", "-vf", "scale=1920:1080", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "28", "-c:a", "copy", video }) mux.ArgumentList.Add(arg);
        using (var process = Process.Start(mux)!) { var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); Check(process.ExitCode == 0, "Video transport fixture generated: " + await error); }
        var wma = Path.Combine(folder, "transport-reference.wma");
        var wmaEncode = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-v", "error", "-y", "-i", wav, "-ar", "44100", "-c:a", "wmav2", wma }) wmaEncode.ArgumentList.Add(arg);
        using (var process = Process.Start(wmaEncode)!) { var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); Check(process.ExitCode == 0, "WMA transport fixture generated: " + await error); }
        var failures = new List<string>();
        foreach (var path in new[] { wav, aac, wma, video })
        {
            using var surface = new ProjectionSurface(smoothTransitions: true) { Volume = 0,
                Transitions = new Culto.Core.TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
            var host = new System.Windows.Window { Content = surface, Width = 960, Height = 580, Topmost = true }; host.Show(); host.Activate();
            var samples = new List<float>(); var gate = new object(); var stop = false;
            Task? consumer = null;
            try
            {
                surface.ShowCue(new Culto.Core.Cue { Kind = path == video ? Culto.Core.CueKind.Video : Culto.Core.CueKind.Audio, FilePath = path });
                var deadline = DateTime.UtcNow.AddSeconds(25);
                while ((!surface.IsPlaying || surface.ActiveSurface.CathedralAudio is null) && DateTime.UtcNow < deadline) await Task.Delay(10);
                Check(surface.IsPlaying && surface.ActiveSurface.CathedralAudio is not null, "Silent transport fixture starts " + Path.GetExtension(path));
                var pcm = surface.ActiveSurface.CathedralAudio!; DetachCallbackDevice(pcm);
                consumer = Task.Run(async () =>
                {
                    var bytes = new byte[480 * 8]; var clock = Stopwatch.StartNew(); var frames = 0;
                    while (!System.Threading.Volatile.Read(ref stop))
                    {
                        if (clock.Elapsed.TotalSeconds * 48000 < frames) { await Task.Delay(1); continue; }
                        pcm.Read(bytes, 0, bytes.Length);
                        lock (gate) for (var i = 0; i < 480; i++) samples.Add(BitConverter.ToSingle(bytes, i * 8));
                        frames += 480;
                    }
                });
                surface.Volume = 100; await Task.Delay(800);
                int start; lock (gate) start = samples.Count;
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    surface.SetPausedWithFade(true); await Task.Delay(650);
                    surface.SetPausedWithFade(false); await Task.Delay(750);
                    surface.SeekWithFade(.15 + cycle * .1); await Task.Delay(800);
                }
                surface.SetPausedWithFade(true); await Task.Delay(650);
                System.Threading.Volatile.Write(ref stop, true); await consumer; consumer = null;
                var captured = samples.Skip(start).ToArray();
                var peak = captured.Max(x => Math.Abs(x));
                var second = Enumerable.Range(2, captured.Length - 2).Max(i => Math.Abs(captured[i] - 2 * captured[i - 1] + captured[i - 2])) / peak;
                Results.Add($"METRIC transport PCM {Path.GetExtension(path)}: second-difference={second:F5}, peak={peak:F5}");
                File.WriteAllBytes(Path.Combine(folder, "transport-pcm-" + Path.GetExtension(path)[1..] + ".f32"), System.Runtime.InteropServices.MemoryMarshal.AsBytes(captured.AsSpan()).ToArray());
                if (peak < .001 || second >= .015 || pcm.HasError) failures.Add(Path.GetExtension(path));
            }
            finally { System.Threading.Volatile.Write(ref stop, true); if (consumer is not null) await consumer; host.Close(); }
        }
        Check(failures.Count == 0, "Actual pause/resume/seek PCM remains continuous: " + string.Join(", ", failures));
    }

    private static async Task RunAudioCallbackContinuityChecks()
    {
        // Exercise the actual VLC callback/PCM queue without mixing Chrome or
        // other Windows sessions into the recording. Pull in device-sized blocks.
        var folder = Path.GetFullPath(".tools/audio-noise"); Directory.CreateDirectory(folder);
        var wav = Path.Combine(folder, "resampler-reference.wav");
        WriteIsolationTone(wav, 440);
        var paths = new[] { wav, Path.Combine(folder, "resampler-reference.m4a") }.Where(File.Exists);
        var failures = new List<string>();
        foreach (var path in paths)
        foreach (var blockFrames in new[] { 480, 2880 })
        {
            using var engine = ProjectionSurface.CreatePlaybackEngine();
            using var player = new MediaPlayer(engine);
            using var pcm = new CathedralAudioOutput();
            pcm.Attach(player, () => engine.Clock);
            DetachCallbackDevice(pcm);
            pcm.SetGain(1);
            using var media = new Media(engine, path, FromType.FromPath);
            Check(player.Play(media), "Callback continuity source starts " + Path.GetExtension(path));
            var samples = await Task.Run(async () =>
            {
                var recorded = new List<float>();
                var bytes = new byte[blockFrames * 8];
                var elapsed = Stopwatch.StartNew(); var frames = 0;
                while (frames < 48000 * 5)
                {
                    if (elapsed.Elapsed.TotalSeconds * 48000 < frames) { await Task.Delay(1); continue; }
                    pcm.Read(bytes, 0, bytes.Length);
                    for (var i = 0; i < blockFrames; i++) recorded.Add(BitConverter.ToSingle(bytes, i * 8));
                    frames += blockFrames;
                }
                return recorded.Skip(48000 * 2).Take(48000 * 2).ToArray();
            });
            player.Stop();
            var residual = MeasureToneResidual(samples);
            var peak = samples.Max(x => Math.Abs(x));
            var second = Enumerable.Range(2, samples.Length - 2).Max(i => Math.Abs(samples[i] - 2 * samples[i - 1] + samples[i - 2])) / peak;
            Results.Add($"METRIC callback {Path.GetExtension(path)} block={blockFrames}: residual={residual:F2}dB second-difference={second:F5}");
            File.WriteAllBytes(Path.Combine(folder, $"callback-{Path.GetExtension(path)[1..]}-{blockFrames}.f32"), System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
            if (residual >= -55 || second >= .015 || pcm.HasError) failures.Add(Path.GetExtension(path) + "/" + blockFrames);
        }
        Check(failures.Count == 0, "Real callback queue preserves uninterrupted PCM at device pull sizes: " + string.Join(", ", failures));
    }
}
