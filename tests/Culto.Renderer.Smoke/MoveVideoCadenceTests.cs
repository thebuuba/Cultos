using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    static async Task RunMoveVideoCadenceChecks()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using (var sample = new CompositedVideo { Source = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgr32, null,
            new byte[] { 10, 20, 30, 0, 10, 20, 30, 0, 10, 20, 30, 0, 10, 20, 30, 0 }, 8) })
        {
            var canvas = Enumerable.Repeat((byte)127, 64).ToArray();
            Check(sample.CopyFrame(canvas, 4, 4) && canvas[0] == 0 && canvas[3] == 255 && canvas[20] == 10 && canvas[23] == 255,
                "Direct video pixels preserve centered black letterboxing and opaque RV32 alpha");
            Check(!sample.CopyFrame(new byte[4], 1, 1), "A larger video frame requests the compositor fallback instead of freezing or overflowing");
            sample.Source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { 40, 50, 60, 0 }, 4);
            Check(sample.CopyFrame(canvas, 4, 4) && canvas[20] == 40 && canvas[24] == 0 && canvas[27] == 255,
                "Smaller video frames clear old pixels from letterboxing");
            int width = System.Numerics.Vector<uint>.Count + 1;
            var packed = Enumerable.Range(0, width).SelectMany(i => new byte[] { (byte)i, 20, 30, (byte)(i * 7) }).ToArray();
            sample.Source = BitmapSource.Create(width, 1, 96, 96, PixelFormats.Bgr32, null, packed, width * 4);
            var opaque = new byte[packed.Length];
            Check(sample.CopyFrame(opaque, width, 1) && Enumerable.Range(0, width).All(i =>
                opaque[i * 4] == i && opaque[i * 4 + 1] == 20 && opaque[i * 4 + 2] == 30 && opaque[i * 4 + 3] == 255),
                "Packed opacity conversion preserves RGB across vector blocks and the final partial block");
        }
        var fixture = Path.GetFullPath(".tools/move-cadence-1080-60fps.mp4");
        if (!File.Exists(fixture))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fixture)!);
            var start = new ProcessStartInfo("ffmpeg") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=1920x1080:rate=60", "-t", "10", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "25", "-pix_fmt", "yuv420p", "-y", fixture }) start.ArgumentList.Add(argument);
            using var generator = Process.Start(start)!; var error = await generator.StandardError.ReadToEndAsync(); await generator.WaitForExitAsync();
            Check(generator.ExitCode == 0, "Moving Full-HD fixture can be generated: " + error);
        }
        Check(File.Exists(fixture), "Full-HD moving-video fixture exists");
        foreach (var width in new[] { 640, 1280, 1920 })
        {
            using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 0, Loop = true,
                Transitions = new TransitionOptions { Kind = "Move", DurationMilliseconds = 3000, AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
            var window = new Window { Content = output, Width = width, Height = width * 9d / 16, WindowStyle = WindowStyle.None, Topmost = true };
            var errors = new List<string>(); output.PlaybackError += errors.Add;
            window.Show(); window.UpdateLayout();
            try
            {
                async Task Until(Func<bool> ready)
                {
                    var end = DateTime.UtcNow.AddSeconds(40);
                    while (!ready() && errors.Count == 0 && DateTime.UtcNow < end) await Task.Delay(20);
                    Check(ready() && errors.Count == 0, "Cadence scenario reaches native stage: " + string.Join(" | ", errors));
                }
                output.ShowCue(new Cue { Kind = CueKind.Video, FilePath = fixture, Title = "Warm video A" });
                await Until(() => output.IsPlaying && output.Position > .4 && !output.IsTransitioning);
                if (width == 1920)
                {
                    var video = output.ActiveSurface.LiveVideoPixels!;
                    int presented = 0;
                    Action onFrame = () => presented++;
                    video.FramePresented += onFrame;
                    var decoderClock = Stopwatch.StartNew();
                    await Task.Delay(1800);
                    double decoderFps = presented / decoderClock.Elapsed.TotalSeconds;
                    video.FramePresented -= onFrame;
                    Check(decoderFps is >= 45 and <= 75,
                        $"Full-HD 60 fps file reaches the program image at its original cadence ({decoderFps:0.0} fps)");
                }
                output.TransitionTo(new Cue { Kind = CueKind.Video, FilePath = fixture, Title = "Warm video B" });
                var mixer = typeof(ProjectionSurface).GetField("_mixer", flags)!.GetValue(output)!;
                object? move = null;
                await Until(() => (move = mixer.GetType().GetField("_liveMove", flags)!.GetValue(mixer)) is not null
                    && (bool)move.GetType().GetProperty("Started")!.GetValue(move)!);
                var moveType = move!.GetType();
                var captureTimer = (DispatcherTimer)moveType.GetField("_capture", flags)!.GetValue(move)!;
                Check(captureTimer.Interval.TotalMilliseconds is >= 33 and <= 34,
                    "Live Move refreshes moving inputs at the lighter 30 fps cadence");
                var capture = moveType.GetMethod("Capture", flags)!;
                captureTimer.Stop();
                var samples = new List<double>(); var allocation = new List<long>();
                for (var i = 0; i < 12; i++)
                {
                    var bytes = GC.GetAllocatedBytesForCurrentThread(); var clock = Stopwatch.StartNew();
                    capture.Invoke(move, new object[] { false });
                    samples.Add(clock.Elapsed.TotalMilliseconds); allocation.Add(GC.GetAllocatedBytesForCurrentThread() - bytes);
                    await Task.Delay(25);
                }
                var ordered = samples.Order().ToArray();
                double copyMilliseconds = 0, mapMilliseconds = 0;
                foreach (var source in ((System.Collections.IDictionary)moveType.GetField("_sources", flags)!.GetValue(move)!).Values)
                {
                    var sourceType = source!.GetType();
                    if (sourceType.GetProperty("Video")?.GetValue(source) is not CompositedVideo video) continue;
                    var pixels = (byte[])sourceType.GetProperty("Pixels")!.GetValue(source)!;
                    var clock = Stopwatch.StartNew();
                    video.CopyFrame(pixels, (int)sourceType.GetProperty("Width")!.GetValue(source)!, (int)sourceType.GetProperty("Height")!.GetValue(source)!);
                    copyMilliseconds += clock.Elapsed.TotalMilliseconds; clock.Restart();
                    ((SharedVideoFrame)sourceType.GetProperty("Map")!.GetValue(source)!).Write(pixels);
                    mapMilliseconds += clock.Elapsed.TotalMilliseconds;
                }
                Results.Add($"METRIC: copy={copyMilliseconds:0.00}ms mailbox={mapMilliseconds:0.00}ms");
                Results.Add($"METRIC: live-video Capture {width}x{width * 9 / 16} median={ordered[ordered.Length / 2]:0.00}ms max={ordered[^1]:0.00}ms allocated={allocation.Average() / 1024 / 1024:0.00}MiB/frame");
                Check(allocation.Average() < 100_000, "Repeated live-video transfer reuses pixels instead of allocating full-frame screenshots");
                await Until(() => !output.IsTransitioning);
                Check(output.IsPlaying && errors.Count == 0, "Warm video continues after measured Move");
                if (width == 1920)
                {
                    output.TransitionTo(new Cue { Kind = CueKind.Video, FilePath = fixture, Title = "Warm video cadence" });
                    var images = 0; ImageSource? previous = null; var clock = Stopwatch.StartNew(); var last = 0d; var gaps = new List<double>();
                    var end = DateTime.UtcNow.AddSeconds(40);
                    while (output.IsTransitioning && errors.Count == 0 && DateTime.UtcNow < end)
                    {
                        var overlay = Descendants(output).OfType<Image>().FirstOrDefault(image => Panel.GetZIndex(image) == 1000 && image.Visibility == Visibility.Visible);
                        if (overlay?.Source is { } current && !ReferenceEquals(previous, current))
                        { var now = clock.Elapsed.TotalMilliseconds; if (previous is not null) gaps.Add(now - last); last = now; previous = current; images++; }
                        await Task.Delay(10);
                    }
                    var sorted = gaps.Order().ToArray();
                    Results.Add($"METRIC: Full-HD native Move presented={images} elapsed={clock.Elapsed.TotalSeconds:0.00}s medianGap={(sorted.Length > 0 ? sorted[sorted.Length / 2] : 0):0.0}ms maxGap={(sorted.Length > 0 ? sorted[^1] : 0):0.0}ms");
                    Check(images >= 30 && !output.IsTransitioning && errors.Count == 0, "Full-HD video-to-video Move advances continuously without fallback");
                    double fps = gaps.Count / (gaps.Sum() / 1000);
                    Check(fps is >= 24 and <= 40, $"Full-HD live Move uses the lighter 30 fps cadence ({fps:0.0} fps)");
                }
            }
            finally { output.Dispose(); window.Close(); }
        }
    }
}
