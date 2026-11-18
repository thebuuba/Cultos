using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static async Task RunAudioPauseOutputChecks()
    {
        var diagnostics = Path.GetFullPath(".tools/audio-pause");
        Directory.CreateDirectory(diagnostics);
        var path = Path.Combine(diagnostics, "pause-reference.wav");
        WriteIsolationTone(path, 440);
        foreach (var fade in new[] { 0, 600 })
        {
            using var capture = new OutputAudioCapture(Path.Combine(diagnostics, $"pause-output-{fade}.wav"));
            await Task.Delay(300);
            var background = capture.Snapshot(0);
            if (background.Any(x => Math.Abs(x) > .0001))
            {
                using var devices = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                using var device = devices.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Console);
                var sessions = device.AudioSessionManager.Sessions;
                for (var i = 0; i < sessions.Count; i++)
                {
                    using var session = sessions[i];
                    Results.Add($"Audio session PID={session.GetProcessID}, peak={session.AudioMeterInformation.MasterPeakValue:F5}");
                }
                Check(false, "Pause output measurement needs a silent endpoint before the reference starts");
            }
            using var output = new ProjectionSurface { Volume = 65, Transitions = new TransitionOptions
            { AudioFadeEffect = "Cathedral", AudioFadeInMilliseconds = fade, AudioFadeOutMilliseconds = 550 } };
            var host = new Window { Content = output, Width = 480, Height = 300, ShowActivated = false };
            host.Show();
            try
            {
                output.ShowCue(new Cue { Kind = CueKind.Audio, FilePath = path });
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while ((!output.IsPlaying || output.Position < 1) && DateTime.UtcNow < deadline) await Task.Delay(20);
                Check(output.IsPlaying && output.Position >= 1, "Native pause fixture starts");
                var start = capture.Count;
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    await Task.Delay(337);
                    output.SetPaused(true);
                    await Task.Delay(400);
                    Check(output.IsPaused && !output.IsPlaying, "Native decoder pauses");
                    output.SetPaused(false);
                    await Task.Delay(850);
                    Check(output.IsPlaying, "Native decoder resumes");
                }
                var samples = capture.Snapshot(start);
                var peak = samples.Max(x => Math.Abs(x));
                var step = samples.Zip(samples.Skip(1), (a, b) => Math.Abs(b - a)).Max();
                Check(peak > .0001 && step < peak * .2,
                    $"Actual output has no waveform cuts across pause/resume, fade={fade} ms (relative step={step / peak:F4})");
                Check(output.CathedralAudio is { HasError: false }, "Native PCM provider stays healthy across pause/resume");
                output.SetPaused(true);
            }
            finally { host.Close(); await capture.StopAsync(); }
        }
    }

    private static void RunAudioPauseChecks()
    {
        using var output = new CathedralAudioOutput();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Call(string method, params object[] args) => typeof(CathedralAudioOutput).GetMethod(method, flags)!.Invoke(output, args);
        float[] Read(int frames)
        {
            var bytes = new byte[frames * 8];
            Check(output.Read(bytes, 0, bytes.Length) == bytes.Length, "PCM pause keeps the device supplied");
            return Enumerable.Range(0, frames).Select(i => BitConverter.ToSingle(bytes, i * 8)).ToArray();
        }
        // A constant signal makes any boundary discontinuity unambiguous. Feed
        // the real PCM provider without an endpoint, so this check is silent.
        void Feed() => Call("Enqueue", Enumerable.Repeat(.5f, 4800).ToArray());
        output.SetMasterGain(1); output.SetGain(1); Feed();
        var steady = Read(960);
        Check(Math.Abs(steady[^1] - .5f) < .0001, "PCM reaches the requested gain");
        output.SetMasterGain(0); output.SetGain(0);
        Call("Pause", IntPtr.Zero, 0L);
        var pause = Read(480);
        var pauseJump = steady.TakeLast(1).Concat(pause).Zip(pause, (a, b) => Math.Abs(b - a)).Max();
        Check(pauseJump < .01 && Math.Abs(pause[^1]) < .00001, $"Pause reaches silence without an abrupt waveform cut (step={pauseJump:F4})");
        Check(Read(480).All(x => x == 0), "Paused PCM remains silent");
        Call("Resume", IntPtr.Zero, 0L);
        output.SetMasterGain(1); output.SetGain(1); Feed();
        var resume = Read(480);
        var resumeJump = new[] { 0f }.Concat(resume).Zip(resume, (a, b) => Math.Abs(b - a)).Max();
        Check(resumeJump < .01 && Math.Abs(resume[^1] - .5f) < .0001, $"Resume restores gain without an abrupt waveform start (step={resumeJump:F4})");
        for (var cycle = 0; cycle < 8; cycle++)
        {
            Feed();
            output.SetGain(0);
            // The device can pull between the UI mute request and VLC's pause
            // callback; neither that race nor a second pause may introduce a step.
            var beforeCallback = Read(24);
            Call("Pause", IntPtr.Zero, 0L);
            var first = Read(24);
            Call("Pause", IntPtr.Zero, 0L);
            var rest = Read(480);
            var release = new[] { .5f }.Concat(beforeCallback).Concat(first).Concat(rest).ToArray();
            Check(release.Zip(release.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .01,
                "Repeated pause and partial device buffers retain a continuous release");
            Call("Resume", IntPtr.Zero, 0L);
            output.SetGain(1);
            Check(Read(480).All(x => x == 0), "Waiting for resumed decoder samples stays silent");
            Feed();
            var next = Read(480);
            Check(next[0] < .01 && Math.Abs(next[^1] - .5f) < .0001, "Delayed resume keeps the short attack");
        }
        Check(!output.HasError, "Pause/resume PCM remains healthy");
    }
}
