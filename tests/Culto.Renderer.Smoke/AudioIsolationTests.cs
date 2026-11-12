using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Culto.Core;
using Culto.Desktop.Projection;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Cue = Culto.Core.Cue;

internal static partial class Program
{
    // Explicit native test. Records the Windows default render endpoint, never a microphone.
    // Keep unrelated applications silent: this deliberately measures the emitted audio, not
    // LibVLC's cached Volume property. All tones and recordings stay in the test fixture folder.
    private static async Task RunAudioIsolationChecks()
    {
        Directory.CreateDirectory(Folder);
        string firstPath = Path.Combine(Folder, "isolation-440.wav");
        string secondPath = Path.Combine(Folder, "isolation-880.wav");
        WriteIsolationTone(firstPath, 440);
        WriteIsolationTone(secondPath, 880);
        using var capture = new OutputAudioCapture("audio-isolation-loopback.wav");
        var results = new List<object>();
        var coverageChecks = new List<(string Phase, double Elapsed, double Captured)>();
        async Task<AudioSpectrum> Measure(string phase)
        {
            var clock = Stopwatch.StartNew();
            int start = capture.Count;
            await Task.Delay(1000);
            var segment = capture.Snapshot(start);
            var elapsedMilliseconds = clock.Elapsed.TotalMilliseconds;
            var capturedMilliseconds = segment.Length / 48d;
            var measurement = MeasureAudioSpectrum(segment);
            results.Add(new { phase, frames = segment.Length, tone440 = measurement.Tone440,
                tone880 = measurement.Tone880, rms = measurement.Rms, elapsedMilliseconds, capturedMilliseconds });
            coverageChecks.Add((phase, elapsedMilliseconds, capturedMilliseconds));
            return measurement;
        }
        async Task Playing(ProjectionSurface surface)
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while ((!surface.IsPlaying || surface.Position < .2) && DateTime.UtcNow < deadline) await Task.Delay(20);
            Check(surface.IsPlaying && surface.Position >= .2, "Native isolation tone starts");
            await Task.Delay(300);
        }
        var instant = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
        using var outgoing = new ProjectionSurface { Volume = 60, Transitions = instant };
        using var incoming = new ProjectionSurface { Volume = 0, Transitions = instant };
        try
        {
            outgoing.ShowCue(new Cue { Kind = CueKind.Audio, FilePath = firstPath, Title = "440 Hz isolation tone" });
            await Playing(outgoing);
            double baseline = (await Measure("outgoing alone")).Tone440;
            Check(baseline > .00001, "Loopback captures an audible outgoing reference tone");

            incoming.ShowCue(new Cue { Kind = CueKind.Audio, FilePath = secondPath, Title = "880 Hz muted preload" });
            await Playing(incoming);
            var preloadAudio = await Measure("incoming preloaded at zero");
            double preloaded = preloadAudio.Tone440;
            incoming.Volume = 60;
            await Task.Delay(250);
            var overlapAudio = await Measure("incoming audible at sixty");
            double overlapping = overlapAudio.Tone440;
            incoming.Volume = 0;
            await Task.Delay(250);
            var mutedAudio = await Measure("incoming muted again");
            double muted = mutedAudio.Tone440;
            await incoming.RetireAsync();
            await Task.Delay(250);
            var retiredAudio = await Measure("incoming retired");
            double retired = retiredAudio.Tone440;
            File.WriteAllText(Path.Combine(Folder, "audio-isolation-measurements.json"),
                JsonSerializer.Serialize(new { device = capture.DeviceName, baseline, preloaded, overlapping, muted, retired, phases = results },
                    new JsonSerializerOptions { WriteIndented = true }));
            foreach (var coverage in coverageChecks)
                Check(Math.Abs(coverage.Captured - coverage.Elapsed) <= 150,
                    $"{coverage.Phase} records the full wall-clock interval (captured {coverage.Captured:0} / {coverage.Elapsed:0} ms)");
            foreach (var phase in new[] { ("preloading", preloaded), ("overlapping", overlapping), ("muting", muted), ("retiring", retired) })
                Check(phase.Item2 >= baseline * .8 && phase.Item2 <= baseline * 1.2,
                    $"{phase.Item1} another decoder preserves outgoing emitted audio (ratio {phase.Item2 / baseline:0.000})");
            Check(overlapAudio.Tone880 > baseline * .25, "Incoming tone is emitted when given audible gain");
            foreach (var phase in new[] { ("preloaded", preloadAudio), ("muted", mutedAudio), ("retired", retiredAudio) })
                Check(phase.Item2.Tone880 < baseline * .05,
                    $"{phase.Item1} incoming decoder does not leak emitted audio (ratio {phase.Item2.Tone880 / baseline:0.000})");
        }
        finally
        {
            await capture.StopAsync();
        }
    }

    private static async Task RunAudioCrossfadeCaptureChecks()
    {
        Directory.CreateDirectory(Folder);
        string firstPath = Path.Combine(Folder, "isolation-440.wav");
        string secondPath = Path.Combine(Folder, "isolation-880.wav");
        WriteIsolationTone(firstPath, 440); WriteIsolationTone(secondPath, 880);
        using var capture = new OutputAudioCapture("audio-crossfade-loopback.wav");
        var measurements = new List<object>();
        var checks = new List<(int Duration, double Minimum, double Maximum, double Elapsed, double Captured)>();
        try
        {
            foreach (var duration in new[] { 250, 1200 })
            {
                using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 60 };
                var first = new Cue { Kind = CueKind.Audio, FilePath = firstPath, Title = "Outgoing 440 Hz" };
                var second = new Cue { Kind = CueKind.Audio, FilePath = secondPath, Title = "Incoming 880 Hz" };
                output.ShowCue(first);
                var deadline = DateTime.UtcNow.AddSeconds(8);
                while ((!output.IsPlaying || output.Position < .5) && DateTime.UtcNow < deadline) await Task.Delay(20);
                Check(output.IsPlaying && output.Position >= .5, "Crossfade reference tone starts");
                await Task.Delay(300);
                var baselineStart = capture.Count;
                await Task.Delay(600);
                var baseline = MeasureAudioSpectrum(capture.Snapshot(baselineStart)).Tone440;
                Check(baseline > .00001, "Crossfade has an audible loopback reference");
                output.Transitions = new TransitionOptions { DurationMilliseconds = duration,
                    AudioFadeInMilliseconds = duration, AudioFadeOutMilliseconds = duration };
                var transitionClock = Stopwatch.StartNew();
                var transitionStart = capture.Count;
                output.TransitionTo(second);
                deadline = DateTime.UtcNow.AddSeconds(8);
                while (output.IsTransitioning && DateTime.UtcNow < deadline) await Task.Delay(10);
                Check(!output.IsTransitioning && output.DisplayedCue?.Id == second.Id,
                    $"Native {duration} ms crossfade finishes");
                // Include decoder disposal and enough emitted audio to expose late native
                // volume/mute events; these have previously looked correct to getter tests.
                await Task.Delay(600);
                var segment = capture.Snapshot(transitionStart);
                var elapsedMilliseconds = transitionClock.Elapsed.TotalMilliseconds;
                var capturedMilliseconds = segment.Length / 48d;
                var final = MeasureAudioSpectrum(segment.TakeLast(14400).ToArray()).Tone880;
                Check(final > .00001, "Incoming tone remains audible after decoder retirement");
                var blocks = new List<object>();
                double minimum = double.MaxValue, maximum = 0;
                const int blockSize = 960; // 20 ms of the real output, including loading.
                for (int start = 0; start + blockSize <= segment.Length; start += blockSize)
                {
                    var spectrum = MeasureAudioSpectrum(segment.AsSpan(start, blockSize));
                    double outgoing = spectrum.Tone440 / baseline, incoming = spectrum.Tone880 / final;
                    double total = outgoing + incoming;
                    minimum = Math.Min(minimum, total); maximum = Math.Max(maximum, total);
                    blocks.Add(new { milliseconds = start / 48d, outgoing, incoming, total, rms = spectrum.Rms });
                }
                measurements.Add(new { duration, baseline, final, minimum, maximum, elapsedMilliseconds, capturedMilliseconds, blocks });
                checks.Add((duration, minimum, maximum, elapsedMilliseconds, capturedMilliseconds));
            }
            File.WriteAllText(Path.Combine(Folder, "audio-crossfade-measurements.json"),
                JsonSerializer.Serialize(new { device = capture.DeviceName, measurements }, new JsonSerializerOptions { WriteIndented = true }));
            foreach (var check in checks)
            {
                // WASAPI can omit packets while a device is silent. Sample-index-only
                // analysis could otherwise hide that gap. Allow only the recorder's
                // 100 ms buffering plus scheduling jitter at the two observation ends.
                Check(Math.Abs(check.Captured - check.Elapsed) <= 150,
                    $"{check.Duration} ms crossfade records the full wall-clock interval (captured {check.Captured:0} / {check.Elapsed:0} ms)");
                Check(check.Minimum >= .55, $"{check.Duration} ms emitted crossfade has no deep audio hole (minimum {check.Minimum:0.000})");
                Check(check.Maximum <= 1.45, $"{check.Duration} ms emitted crossfade has no unintended volume burst (maximum {check.Maximum:0.000})");
            }
        }
        finally { await capture.StopAsync(); }
    }

    private readonly record struct AudioSpectrum(double Tone440, double Tone880, double Rms);
    private static AudioSpectrum MeasureAudioSpectrum(ReadOnlySpan<float> samples)
    {
        double sin440 = 0, cos440 = 0, sin880 = 0, cos880 = 0, squares = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double angle = 2 * Math.PI * 440 * i / 48000;
            sin440 += samples[i] * Math.Sin(angle); cos440 += samples[i] * Math.Cos(angle);
            sin880 += samples[i] * Math.Sin(angle * 2); cos880 += samples[i] * Math.Cos(angle * 2);
            squares += samples[i] * samples[i];
        }
        return samples.IsEmpty ? default : new(2 * Math.Sqrt(sin440 * sin440 + cos440 * cos440) / samples.Length,
            2 * Math.Sqrt(sin880 * sin880 + cos880 * cos880) / samples.Length, Math.Sqrt(squares / samples.Length));
    }

    private sealed class OutputAudioCapture : IDisposable
    {
        private readonly MMDeviceEnumerator _devices = new();
        private readonly MMDevice _device;
        private readonly WasapiLoopbackCapture _capture;
        private readonly WaveFileWriter _recording;
        private readonly List<float> _samples = [];
        private readonly object _gate = new();
        private readonly TaskCompletionSource<Exception?> _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string DeviceName => _device.FriendlyName;
        public int Count { get { lock (_gate) return _samples.Count; } }
        public float[] Snapshot(int start) { lock (_gate) return _samples.Skip(start).ToArray(); }
        public OutputAudioCapture(string filename)
        {
            _device = _devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            _capture = new WasapiLoopbackCapture(_device) { WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2) };
            _recording = new WaveFileWriter(Path.Combine(Folder, filename), _capture.WaveFormat);
            _capture.DataAvailable += (_, args) =>
            {
                lock (_gate)
                {
                    _recording.Write(args.Buffer, 0, args.BytesRecorded);
                    for (int i = 0; i + 7 < args.BytesRecorded; i += 8)
                        _samples.Add((BitConverter.ToSingle(args.Buffer, i) + BitConverter.ToSingle(args.Buffer, i + 4)) / 2);
                }
            };
            _capture.RecordingStopped += (_, args) => _stopped.TrySetResult(args.Exception);
            _capture.StartRecording();
        }
        public async Task StopAsync()
        {
            _capture.StopRecording();
            var error = await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (error is not null) throw new InvalidOperationException("WASAPI loopback recording failed.", error);
        }
        public void Dispose()
        {
            _capture.Dispose(); _recording.Dispose(); _device.Dispose(); _devices.Dispose();
        }
    }

    private static void WriteIsolationTone(string path, int frequency)
    {
        const int rate = 48000;
        using var writer = new WaveFileWriter(path, new WaveFormat(rate, 16, 2));
        var buffer = new byte[rate * 4];
        for (int second = 0; second < 20; second++)
        {
            for (int sample = 0; sample < rate; sample++)
            {
                var value = (short)Math.Round(short.MaxValue * .15 * Math.Sin(2 * Math.PI * frequency * sample / rate));
                buffer[sample * 4] = buffer[sample * 4 + 2] = (byte)(value & 255);
                buffer[sample * 4 + 1] = buffer[sample * 4 + 3] = (byte)(value >> 8);
            }
            writer.Write(buffer, 0, buffer.Length);
        }
    }
}
