using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static async Task RunTransportOutputChecks()
    {
        var directory = Path.GetFullPath(".tools/transport-output");
        Directory.CreateDirectory(directory);
        var tone = Path.Combine(directory, "reference.wav");
        WriteIsolationTone(tone, 440);
        using var capture = new OutputAudioCapture(Path.Combine(directory, "pause-resume-seek.wav"));
        using var output = new ProjectionSurface(smoothTransitions: true)
        {
            Volume = 70,
            Transitions = new TransitionOptions { Kind = "Cut", AudioFadeEffect = Environment.GetEnvironmentVariable("CULTO_TRANSPORT_EFFECT") ?? "Normal", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 }
        };
        var duringBlend = Environment.GetEnvironmentVariable("CULTO_TRANSPORT_DURING_BLEND") == "1";
        var activeTail = duringBlend || Environment.GetEnvironmentVariable("CULTO_TRANSPORT_ACTIVE_TAIL") == "1";
        if (activeTail) output.Transitions = new TransitionOptions { Kind = "Dissolve", DurationMilliseconds = 300,
            AudioFadeEffect = "Cathedral", AudioFadeInMilliseconds = duringBlend ? 6000 : 600, AudioFadeOutMilliseconds = duringBlend ? 6000 : 550,
            CathedralIntensityPercent = 57, CathedralTailMilliseconds = 7400 };
        var host = new Window { Content = output, Width = 640, Height = 400, Topmost = true };
        host.Show(); host.Activate();
        try
        {
            await Task.Delay(350);
            var baseline = capture.Snapshot(0);
            Results.Add($"METRIC baseline peak={baseline.Select(x => Math.Abs(x)).DefaultIfEmpty().Max():F6}");
            using (var devices = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            using (var device = devices.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Console))
            {
                var sessions = device.AudioSessionManager.Sessions;
                for (var index = 0; index < sessions.Count; index++)
                {
                    using var session = sessions[index];
                    Results.Add($"SESSION pid={session.GetProcessID} peak={session.AudioMeterInformation.MasterPeakValue:F6}");
                }
            }
            Check(baseline.All(x => Math.Abs(x) < .001), "Loopback baseline is silent before the reference starts");
            output.ShowCue(new Cue { Kind = CueKind.Audio, FilePath = tone });
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while ((!output.IsPlaying || output.Position < .5 || output.IsTransitioning) && DateTime.UtcNow < deadline) await Task.Delay(10);
            Check(output.IsPlaying && output.Position >= .5, "Output reference starts");
            await Task.Delay(300);
            if (activeTail)
            {
                var incoming = new Cue { Kind = CueKind.Audio, FilePath = tone, Title = "Next reference" };
                output.TransitionTo(incoming);
                deadline = DateTime.UtcNow.AddSeconds(15);
                while ((output.DisplayedCue?.Id != incoming.Id || (!duringBlend && output.IsTransitioning)) && DateTime.UtcNow < deadline) await Task.Delay(10);
                Check(output.DisplayedCue?.Id == incoming.Id && (duringBlend ? output.IsTransitioning : !output.IsTransitioning),
                    duringBlend ? "Transport is exercised during an unfinished Cathedral blend" : "Reference changes cue with a long Cathedral tail still audible");
            }
            var start = capture.Count;
            var perceptibleRelease = true;
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await Task.Delay(173);
                output.SetPausedWithFade(true);
                await Task.Delay(200);
                perceptibleRelease &= output.IsPlaying;
                await Task.Delay(450);
                Check(!output.IsPlaying, "Pause finishes after its release");
                output.SetPausedWithFade(false);
                await Task.Delay(750);
                output.SeekWithFade(.15 + cycle * .1);
                await Task.Delay(800);
            }
            output.SetPausedWithFade(true); await Task.Delay(650);
            var samples = capture.Snapshot(start);
            var peak = samples.Max(x => Math.Abs(x));
            var secondDifference = Enumerable.Range(2, samples.Length - 2)
                .Max(i => Math.Abs(samples[i] - 2 * samples[i - 1] + samples[i - 2]));
            Results.Add($"METRIC transport: peak={peak:F6}, relative-second-difference={secondDifference / peak:F5}");
            Check(peak > .001, "Transport capture contains audible reference audio");
            Check(perceptibleRelease, "Pause release remains audible beyond 200 ms");
            Check(secondDifference / peak < .015, "Emitted pause/resume/seek waveform has no abrupt gain steps or clicks");
        }
        finally { host.Close(); await capture.StopAsync(); }
    }
}
