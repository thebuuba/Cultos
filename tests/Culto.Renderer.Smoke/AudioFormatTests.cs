using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Culto.Desktop.Projection;
using LibVLCSharp.Shared;

internal static partial class Program
{
    private static async Task RunAudioFormatChecks()
    {
        using (var timingEngine = ProjectionSurface.CreatePlaybackEngine())
        using (var timingPlayer = new MediaPlayer(timingEngine))
        using (var timingOutput = new CathedralAudioOutput())
        {
            long now = 1_000_000;
            timingOutput.Attach(timingPlayer, () => now); DetachCallbackDevice(timingOutput); timingOutput.SetGain(1);
            var packet = Enumerable.Range(0, 7200 * 2).Select(i => (short)(5000 * Math.Sin(2 * Math.PI * 440 * (i / 2) / 48000))).ToArray();
            var pointer = Marshal.AllocHGlobal(packet.Length * sizeof(short));
            try
            {
                Marshal.Copy(packet, 0, pointer, packet.Length);
                typeof(CathedralAudioOutput).GetMethod("Play", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(timingOutput, [IntPtr.Zero, pointer, 7200u, 1_200_000L]);
                timingOutput.EndInput();
                var bytes = new byte[2880 * 8]; timingOutput.Read(bytes, 0, bytes.Length);
                Check(bytes.All(b => b == 0), "Decoded audio is held until its timestamp, including already-drained clips");
                now = 1_140_000; timingOutput.Read(bytes, 0, bytes.Length);
                Check(bytes.Any(b => b != 0), "Audio begins at its timestamp minus device latency");
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        var folder = Path.GetFullPath(".tools/audio-formats"); Directory.CreateDirectory(folder);
        var tone = Path.Combine(folder, "reference.wav"); WriteIsolationTone(tone, 440);
        var formats = new (string Extension, string Codec)[] { ("wma", "wmav2"), ("mp3", "libmp3lame"), ("wav", "pcm_s16le"),
            ("flac", "flac"), ("ogg", "libvorbis"), ("m4a", "aac"), ("aac", "aac"), ("aif", "pcm_s16be"),
            ("aiff", "pcm_s16be"), ("opus", "libopus"), ("oga", "libvorbis"), ("m4b", "aac") };
        var failed = new List<string>();
        var cases = formats.Select(f => (f.Extension, f.Codec, Rate: f.Extension == "opus" ? 48000 : 44100, Channels: 2))
            .Concat(new[] { ("wma", "wmav2", 22050, 1), ("wma", "wmav2", 32000, 2), ("wma", "wmav2", 48000, 2) });
        foreach (var (extension, codec, rate, channels) in cases)
        {
            var label = extension + "/" + rate + "/" + channels;
            var path = Path.Combine(folder, "tone-" + rate + "-" + channels + "." + extension);
            var encode = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-v", "error", "-y", "-i", tone, "-t", "8", "-ar", rate.ToString(), "-ac", channels.ToString(), "-c:a", codec, path }) encode.ArgumentList.Add(arg);
            using (var process = Process.Start(encode)!) { var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); Check(process.ExitCode == 0, "Generated " + extension + ": " + await error); }
            using var engine = ProjectionSurface.CreatePlaybackEngine();
            using var player = new MediaPlayer(engine);
            using var pcm = new CathedralAudioOutput();
            pcm.Attach(player, () => engine.Clock); DetachCallbackDevice(pcm); pcm.SetGain(1);
            using var media = new Media(engine, path, FromType.FromPath);
            Check(player.Play(media), "Decoder starts " + extension);
            var captured = await Task.Run(async () =>
            {
                const int block = 2880; var bytes = new byte[block * 8]; var values = new List<float>();
                var clock = Stopwatch.StartNew(); var frames = 0;
                while (frames < 48000 * 4)
                {
                    if (clock.Elapsed.TotalSeconds * 48000 < frames) { await Task.Delay(1); continue; }
                    pcm.Read(bytes, 0, bytes.Length);
                    for (var i = 0; i < block; i++) values.Add(BitConverter.ToSingle(bytes, i * 8));
                    frames += block;
                }
                return values.Skip(48000).Take(48000 * 2).ToArray();
            });
            player.Stop();
            var peak = captured.Max(v => Math.Abs(v));
            var residual = MeasureToneResidual(captured);
            var zeros = captured.Count(v => Math.Abs(v) < 0.00001) / (double)captured.Length;
            Results.Add($"METRIC {label}: peak={peak:F4} residual={residual:F2}dB zero={zeros:P2} underruns={pcm.UnderrunCount} callback={pcm.LargestCallbackFrames} error={pcm.LastError}");
            File.WriteAllLines(ResultPath, Results);
            if (peak < .01 || residual > -35 || zeros > .01 || pcm.UnderrunCount > 0 || pcm.HasError) failed.Add(label);
        }
        Check(failed.Count == 0, "Supported formats have continuous clean PCM: " + string.Join(", ", failed));
    }
}
