using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    static async Task RunCathedralChecks()
    {
        await Task.Run(() =>
        {
            using var processor = new CathedralReverbProcessor(48000, 2, 2500);
            var block = new float[960]; block[0] = block[1] = .5f; processor.Process(block, 0, block.Length);
            double early = 0, late = 0;
            for (var tick = 0; tick < 300; tick++)
            {
                Array.Clear(block); processor.Process(block, 0, block.Length);
                if (!block.All(float.IsFinite)) throw new Exception("Cathedral reverb produced a non-finite sample");
                var energy = block.Sum(v => (double)v * v);
                if (tick < 100) early += energy; else if (tick > 200) late += energy;
            }
            Results.Add($"METRIC Cathedral impulse: early={early:0.000000} late={late:0.000000}");
            Check(early > .000001 && late < early, "Cathedral emits a finite decaying tail after input ends");
        });
        using var capture = new OutputAudioCapture("cathedral-loopback.wav");
        using (var demo = await CathedralAudioOutput.PlayPreviewAsync(1800, 50, CancellationToken.None))
        {
            var tailStart = capture.Count; await Task.Delay(400);
            var energy = MeasureAudioSpectrum(capture.Snapshot(tailStart)).Rms;
            Check(!demo.HasError && energy > .000001, "Cathedral preview emits a real reverberation tail: " + demo.LastError);
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (!demo.TailComplete && DateTime.UtcNow < deadline) await Task.Delay(30);
            Check(demo.TailComplete, "Cathedral preview tail finishes in bounded time");
        }
        var path = Path.Combine(Folder, "cathedral-440.wav"); WriteIsolationTone(path, 440);
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 60,
            Transitions = new TransitionOptions { AudioFadeEffect = "Cathedral", AudioFadeOutMilliseconds = 300,
                AudioFadeInMilliseconds = 100, CathedralTailMilliseconds = 1800, CathedralIntensityPercent = 50 } };
        var window = new Window { Content = output, Width = 640, Height = 400, Topmost = true }; window.Show();
        try
        {
            var cue = new Cue { Kind = CueKind.Audio, FilePath = path, Title = "Catedral audio" };
            output.ShowCue(cue);
            var end = DateTime.UtcNow.AddSeconds(10);
            while ((!output.IsPlaying || output.IsTransitioning || output.Position < .5) && DateTime.UtcNow < end) await Task.Delay(30);
            var start = capture.Count; await Task.Delay(400);
            Check(output.IsPlaying && MeasureAudioSpectrum(capture.Snapshot(start)).Tone440 > .00001, "Catedral route keeps normal file audio audible before departure");
            output.TransitionTo(new Cue { Kind = CueKind.Text, Title = "Nuevo", Body = "Nueva escena" });
            while (output.IsTransitioning && DateTime.UtcNow < end) await Task.Delay(20);
            start = capture.Count; await Task.Delay(400);
            var tail = MeasureAudioSpectrum(capture.Snapshot(start)).Rms;
            Check(!output.IsTransitioning && tail > .000001, "Outgoing cathedral tail survives decoder/visual retirement");
            output.Volume = 0; await Task.Delay(200); start = capture.Count; await Task.Delay(300);
            Check(MeasureAudioSpectrum(capture.Snapshot(start)).Rms < .00001, "Master mute also silences the detached cathedral tail");
            output.Volume = 60; output.ShowCue(cue); await Task.Delay(1200);
            output.SetPaused(true); await Task.Delay(200); output.SetPaused(false); await Task.Delay(700);
            start = capture.Count; await Task.Delay(300);
            Check(output.IsPlaying && MeasureAudioSpectrum(capture.Snapshot(start)).Tone440 > .00001, "Catedral pause/resume restores dry audio");
            output.Restart(); await Task.Delay(800); start = capture.Count; await Task.Delay(300);
            Check(output.IsPlaying && MeasureAudioSpectrum(capture.Snapshot(start)).Tone440 > .00001, "Catedral restart opens a fresh dry audio stream");
            output.TransitionToBackdrop(cue, blackout: true, hideText: false);
            end = DateTime.UtcNow.AddSeconds(8); while (output.IsTransitioning && DateTime.UtcNow < end) await Task.Delay(20);
            start = capture.Count; await Task.Delay(350);
            Check(MeasureAudioSpectrum(capture.Snapshot(start)).Rms > .000001, "Retained video pause keeps its cathedral tail audible over blackout");
            output.TransitionTo(cue); await Task.Delay(1000); start = capture.Count; await Task.Delay(300);
            Check(output.IsPlaying && MeasureAudioSpectrum(capture.Snapshot(start)).Tone440 > .00001, "Restoring a retained Catedral file restores dry audio");
        }
        finally { output.Dispose(); window.Close(); await capture.StopAsync(); }
    }
}
