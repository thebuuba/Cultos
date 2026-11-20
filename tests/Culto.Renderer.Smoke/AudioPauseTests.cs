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
        output.SetGain(.98); Feed();
        var fineFade = new[] { .5f }.Concat(Read(64)).ToArray();
        Check(fineFade.Zip(fineFade.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .0001,
            "Small gain changes interpolate across samples instead of producing zipper noise");
        output.SetGain(1); Feed(); Read(480);
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
        Call("Flush", IntPtr.Zero, 0L); Feed(); Read(2400);
        var gap = new[] { .5f }.Concat(Read(480)).ToArray();
        Check(gap.Zip(gap.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .01 && Math.Abs(gap[^1]) < .00001,
            "PCM starvation releases to silence without a waveform cut");
        Feed();
        var refill = new[] { 0f }.Concat(Read(480)).ToArray();
        Check(refill.Zip(refill.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .01 && Math.Abs(refill[^1] - .5f) < .0001,
            "PCM refill restarts a short attack instead of jumping to full gain");
        Call("Flush", IntPtr.Zero, 0L); Feed();
        var flushed = new[] { .5f }.Concat(Read(480)).ToArray();
        Check(flushed.Zip(flushed.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .01,
            "A seek flush and immediate refill preserve sample continuity");
        Call("Pause", IntPtr.Zero, 0L); Read(480);
        Call("Flush", IntPtr.Zero, 0L); Call("Resume", IntPtr.Zero, 0L);
        Call("Enqueue", new float[960]);
        Check(Read(480).All(x => Math.Abs(x) < .000001), "Seeking while paused cannot replay a stale residual on resume");
        Feed(); Read(480);
        Call("Pause", IntPtr.Zero, 0L);
        var partialPause = Read(120);
        Call("Resume", IntPtr.Zero, 0L); Feed();
        var interrupted = partialPause.TakeLast(1).Concat(Read(480)).ToArray();
        Check(interrupted.Zip(interrupted.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .01,
            "Resume during a partially rendered pause release preserves sample continuity");
        Call("Flush", IntPtr.Zero, 0L); Feed(); Read(480);
        output.SetGain(1); output.SetMasterGain(1);
        output.BeginFadeOut(600, 7400, 0); Read(480);
        output.SetGain(.98);
        var tailGain = new[] { .5f }.Concat(Read(64)).ToArray();
        Check(tailGain.Zip(tailGain.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .0001,
            "Active Cathedral dry audio interpolates small gain changes");
        using var wetControl = new CathedralAudioOutput();
        using var wetChanged = new CathedralAudioOutput();
        float[] WetRead(CathedralAudioOutput provider, int frames)
        {
            var bytes = new byte[frames * 8]; provider.Read(bytes, 0, bytes.Length);
            return Enumerable.Range(0, frames).Select(i => BitConverter.ToSingle(bytes, i * 8)).ToArray();
        }
        foreach (var provider in new[] { wetControl, wetChanged })
        {
            provider.SetMasterGain(1); provider.SetGain(0);
            typeof(CathedralAudioOutput).GetMethod("Enqueue", flags)!.Invoke(provider, new object[] { Enumerable.Repeat(.1f, 96000).ToArray() });
            provider.BeginFadeOut(600, 7400, 100);
            WetRead(provider, 9600);
        }
        wetChanged.SetMasterGain(.5);
        var referenceWet = WetRead(wetControl, 32); var changedWet = WetRead(wetChanged, 32);
        Check(Math.Abs(referenceWet[0]) > .001, "Wet-only reference contains audible room decay");
        Check(Math.Abs(changedWet[0] - referenceWet[0]) / Math.Abs(referenceWet[0]) < .01,
            "Cathedral room master gain begins its change gradually instead of clicking");
        using var tailDry = new CathedralAudioOutput();
        tailDry.SetGain(1); tailDry.SetMasterGain(1);
        void TailFeed(float value) => typeof(CathedralAudioOutput).GetMethod("Enqueue", flags)!.Invoke(tailDry,
            new object[] { Enumerable.Repeat(value, 960).ToArray() });
        TailFeed(.5f); tailDry.BeginFadeOut(600, 7400, 0); WetRead(tailDry, 480);
        var tailGap = new[] { .5f }.Concat(WetRead(tailDry, 480)).ToArray();
        Check(tailGap.Zip(tailGap.Skip(1), (a, b) => Math.Abs(b - a)).Max() < .01,
            "Active Cathedral dry starvation releases instead of cutting the waveform");
        TailFeed(.5f); WetRead(tailDry, 480);
        typeof(CathedralAudioOutput).GetMethod("Pause", flags)!.Invoke(tailDry, new object[] { IntPtr.Zero, 0L });
        tailDry.SetGain(0); tailDry.SetMasterGain(0); WetRead(tailDry, 480);
        typeof(CathedralAudioOutput).GetMethod("Resume", flags)!.Invoke(tailDry, new object[] { IntPtr.Zero, 0L });
        TailFeed(0);
        Check(WetRead(tailDry, 480).All(x => Math.Abs(x) < .000001),
            "A paused Cathedral tail cannot replay its old dry sample on resume");
        using (var engine = ProjectionSurface.CreatePlaybackEngine())
        using (var mixer = new ProjectionMixer())
        {
            var leaf = new ProjectionSurface(sharedVlc: () => engine);
            var pcm = new CathedralAudioOutput();
            typeof(ProjectionSurface).GetField("_player", flags)!.SetValue(leaf, new LibVLCSharp.Shared.MediaPlayer(engine));
            typeof(ProjectionSurface).GetField("_cathedralAudio", flags)!.SetValue(leaf, pcm);
            typeof(ProjectionSurface).GetField("_audioGain", flags)!.SetValue(leaf, 1d);
            typeof(ProjectionMixer).GetField("_audioOutgoing", flags)!.SetValue(mixer, leaf);
            typeof(ProjectionMixer).GetField("_audioBlend", flags)!.SetValue(mixer, new TransitionOptions { AudioFadeOutMilliseconds = 6000 });
            ((System.Diagnostics.Stopwatch)typeof(ProjectionMixer).GetField("_audioClock", flags)!.GetValue(mixer)!).Start();
            pcm.BeginFadeOut(6000, 7400, 57); mixer.Volume = 70;
            foreach (var transport in new[] { .5, 0d, 1d })
            {
                typeof(ProjectionMixer).GetField("_transportGain", flags)!.SetValue(mixer, transport);
                typeof(ProjectionMixer).GetMethod("ApplyVolumes", flags)!.Invoke(mixer, null);
                var master = (double)typeof(CathedralAudioOutput).GetField("_master", flags)!.GetValue(pcm)!;
                Check(Math.Abs(master - Math.Pow(.7, 3) * transport) < .00001,
                    "Active outgoing Cathedral wet bus follows transport gain " + transport);
            }
        }
        Check(!output.HasError, "Pause/resume PCM remains healthy");
        using var primed = new CathedralAudioOutput();
        typeof(CathedralAudioOutput).GetField("_clock", flags)!.SetValue(primed, (Func<long>)(() => 0));
        primed.SetGain(1);
        void PrimeFeed(int frames) => typeof(CathedralAudioOutput).GetMethod("Enqueue", flags)!.Invoke(primed,
            new object[] { Enumerable.Repeat(.5f, frames * 2).ToArray() });
        PrimeFeed(1114);
        Check(WetRead(primed, 480).All(x => x == 0), "A clocked decoder waits beyond its first AAC packet");
        PrimeFeed(1115);
        Check(WetRead(primed, 480).All(x => x == 0), "Priming retains queued samples until the reserve is complete");
        PrimeFeed(1114);
        var start = WetRead(primed, 2400);
        Check(start[0] < .01 && Math.Abs(start[^1] - .5f) < .0001, "Primed decoder enters through its native startup attack");
        WetRead(primed, 2400);
        PrimeFeed(1114); primed.EndInput();
        Check(WetRead(primed, 1400).Any(x => x > .1), "EOF drains a short final packet instead of waiting for a full reserve");
    }
}
