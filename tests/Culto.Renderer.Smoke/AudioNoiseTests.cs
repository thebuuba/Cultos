using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static async Task RunAudioResamplerChecks()
    {
        var diagnostics = Path.GetFullPath(".tools/audio-noise");
        Directory.CreateDirectory(diagnostics);
        var wav = Path.Combine(diagnostics, "resampler-reference.wav");
        var aac = Path.Combine(diagnostics, "resampler-reference.m4a");
        WriteIsolationTone(wav, 440);
        var conversion = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-v", "error", "-y", "-i", wav, "-ar", "44100", "-c:a", "aac", "-b:a", "192k", aac }) conversion.ArgumentList.Add(argument);
        using (var process = Process.Start(conversion)!)
        {
            var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
            Check(process.ExitCode == 0, "Resampler fixture is generated: " + await error);
        }
        var sources = new List<string> { wav, aac };
        var actualMedia = Environment.GetEnvironmentVariable("CULTO_NOISE_MEDIA");
        if (!string.IsNullOrWhiteSpace(actualMedia) && File.Exists(actualMedia)) sources.Add(actualMedia);
        foreach (var source in sources)
        {
            using var engine = ProjectionSurface.CreatePlaybackEngine();
            using var player = new LibVLCSharp.Shared.MediaPlayer(engine);
            var recorded = new List<float>();
            var gate = new object();
            // Same memory-output contract as Cathedral, without a Windows audio
            // sink: unrelated music cannot contaminate this comparison.
            player.SetAudioFormat("S16N", 48000, 2);
            player.SetAudioCallbacks((_, data, count, _) =>
            {
                var pcm = new short[checked((int)count * 2)];
                Marshal.Copy(data, pcm, 0, pcm.Length);
                lock (gate) for (var i = 0; i < pcm.Length; i += 2) recorded.Add((pcm[i] + pcm[i + 1]) / 65536f);
            }, null, null, null, null);
            player.SetVolumeCallback((_, _, _) => { });
            using var media = new Media(engine, source, FromType.FromPath);
            if (source == actualMedia) media.AddOption(":start-time=15");
            player.Volume = 100; player.Mute = false;
            Check(player.Play(media), "Memory decoder starts " + Path.GetExtension(source));
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (DateTime.UtcNow < deadline)
            {
                lock (gate) if (recorded.Count >= 48000 * 5) break;
                await Task.Delay(20);
            }
            player.Stop();
            float[] samples;
            lock (gate) samples = recorded.Skip(48000).Take(48000 * 3).ToArray();
            Check(samples.Length == 48000 * 3, "Memory decoder supplies three steady seconds " + Path.GetExtension(source));
            if (source == actualMedia)
            {
                File.WriteAllBytes(Path.Combine(diagnostics, "actual-media-callback.f32"), MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
                Results.Add("PASS: Actual media decoded through the corrected callback route for source comparison");
                continue;
            }
            var residual = MeasureToneResidual(samples);
            Results.Add($"METRIC memory {Path.GetExtension(source)}: residual={residual:F2}dB samples={samples.Length}");
            Check(residual < -55, "Resampling preserves fine waveform detail without added grain " + Path.GetExtension(source));
        }
    }

    private static async Task RunAudioNoiseChecks()
    {
        // Whole-endpoint loopback includes every application. Run this explicit
        // audible test with other media paused; --audio-resampler is isolated
        // and silent, so it remains valid while the user plays unrelated audio.
        var diagnostics = Path.GetFullPath(".tools/audio-noise");
        Directory.CreateDirectory(diagnostics);
        var wav = Path.Combine(diagnostics, "noise-reference.wav");
        WriteIsolationTone(wav, 440);
        var aac = Path.Combine(diagnostics, "noise-reference.m4a");
        var conversion = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "-v", "error", "-y", "-i", wav, "-ar", "44100", "-c:a", "aac", "-b:a", "192k", aac }) conversion.ArgumentList.Add(argument);
        using (var process = Process.Start(conversion)!)
        {
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Check(process.ExitCode == 0, "AAC/resampling noise fixture is generated: " + await error);
        }
        var video = Path.Combine(diagnostics, "noise-reference.mp4");
        var mux = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "-v", "error", "-y", "-stream_loop", "-1", "-i", Path.Combine(Folder, "test.mkv"), "-i", wav,
            "-map", "0:v:0", "-map", "1:a:0", "-t", "12", "-vf", "scale=1920:1080", "-c:v", "libx264", "-preset", "ultrafast",
            "-crf", "28", "-c:a", "aac", "-ar", "44100", "-b:a", "192k", video }) mux.ArgumentList.Add(argument);
        using (var process = Process.Start(mux)!)
        {
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Check(process.ExitCode == 0, "Full HD video noise fixture is generated: " + await error);
        }
        var measurements = new List<object>();
        var failed = false;
        foreach (var source in new[] { wav, aac, video })
        foreach (var cathedral in new[] { false, true })
        {
            var label = Path.GetExtension(source) + (cathedral ? " Cathedral" : " DirectSound");
            using var capture = new OutputAudioCapture(Path.Combine(diagnostics, "noise-" + Path.GetExtension(source)[1..] + (cathedral ? "-cathedral.wav" : "-direct.wav")));
            using var surface = new ProjectionSurface { Volume = 60,
                Transitions = new TransitionOptions { AudioFadeEffect = cathedral ? "Cathedral" : "Normal", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
            var host = new Window { Content = surface, Width = 640, Height = 400, ShowActivated = false, ShowInTaskbar = false };
            host.Show();
            surface.ShowCue(new Cue { Kind = source == video ? CueKind.Video : CueKind.Audio, FilePath = source, Title = "Low-level audio continuity reference" });
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while ((!surface.IsPlaying || surface.Position < 1) && DateTime.UtcNow < deadline) await Task.Delay(20);
            Check(surface.IsPlaying && surface.Position >= 1, label + " starts");
            await Task.Delay(300);
            var start = capture.Count;
            var clock = Stopwatch.StartNew();
            await Task.Delay(4000);
            var samples = capture.Snapshot(start);
            var elapsed = clock.Elapsed.TotalMilliseconds;
            var peak = samples.Length == 0 ? 0 : samples.Max(v => Math.Abs(v));
            var run = 0; var longest = 0; var jumps = 0; var maxStep = 0d;
            for (var i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) < .000001) { run++; longest = Math.Max(longest, run); } else run = 0;
                if (i == 0) continue;
                var step = Math.Abs(samples[i] - samples[i - 1]);
                maxStep = Math.Max(maxStep, step);
                if (step > peak * .25) jumps++;
            }
            var longestSilenceMs = longest / 48d;
            var capturedMs = samples.Length / 48d;
            var residualDb = MeasureToneResidual(samples);
            var pass = peak > .0001 && samples.All(float.IsFinite) && longestSilenceMs < 3 && jumps == 0
                && Math.Abs(capturedMs - elapsed) < 180 && residualDb < -55;
            measurements.Add(new { label, peak, longestSilenceMs, jumps, maxStep, capturedMs, elapsed, residualDb, pass });
            Results.Add($"METRIC {label}: peak={peak:F6} silence={longestSilenceMs:F3}ms jumps={jumps} step={maxStep:F6} residual={residualDb:F2}dB captured={capturedMs:F0}/{elapsed:F0}ms");
            if (!pass) { failed = true; Results.Add("FAIL continuity: " + label); }
            else Results.Add("PASS continuity: " + label);
            surface.SetPaused(true);
            host.Close();
            await capture.StopAsync();
            await Task.Delay(150);
        }
        File.WriteAllText(Path.Combine(diagnostics, "audio-noise-measurements.json"), JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true }));
        Check(!failed, "PCM and AAC playback preserve the reference waveform without added grain, jumps or silence holes");
    }

    private static double MeasureToneResidual(float[] samples)
    {
        double ss = 0, cc = 0, sc = 0, ys = 0, yc = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var angle = 2 * Math.PI * 440 * i / 48000;
            var s = Math.Sin(angle); var c = Math.Cos(angle);
            ss += s * s; cc += c * c; sc += s * c;
            ys += samples[i] * s; yc += samples[i] * c;
        }
        var determinant = ss * cc - sc * sc;
        if (determinant <= 0) return 0;
        var a = (ys * cc - yc * sc) / determinant;
        var b = (yc * ss - ys * sc) / determinant;
        double residual = 0, signal = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var angle = 2 * Math.PI * 440 * i / 48000;
            var reference = a * Math.Sin(angle) + b * Math.Cos(angle);
            residual += Math.Pow(samples[i] - reference, 2); signal += reference * reference;
        }
        return 10 * Math.Log10(Math.Max(residual, 1e-30) / Math.Max(signal, 1e-30));
    }
}
