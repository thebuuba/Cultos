using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Culto.Core;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using LibVLCSharp.WPF;

internal static partial class Program
{
    static readonly string Folder = Path.GetFullPath("tests/Culto.Renderer.Smoke/fixtures");
    static readonly List<string> Results = [];
    static string ResultPath => Environment.GetEnvironmentVariable("CULTO_RENDERER_RESULT") ?? Path.Combine(Folder, "result.txt");
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--capture-source")) { RunCaptureSource(); return; }
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try { if (args.Contains("--cathedral")) await RunCathedralChecks(); else if (args.Contains("--move-video-cadence")) await RunMoveVideoCadenceChecks(); else if (args.Contains("--live-media")) await RunLiveMediaMoveChecks(); else if (args.Contains("--live-layers")) await RunLiveLayerMoveChecks();
                else if (args.Contains("--layer-pool")) await RunLayerSourcePoolChecks();
                else if (args.Contains("--transition-audit")) await RunTransitionAuditChecks();
                else if (args.Contains("--native-move")) await RunNativeMoveChecks();
                else if (args.Contains("--audio-isolation")) await RunAudioIsolationChecks();
                else if (args.Contains("--audio-crossfade")) await RunAudioCrossfadeCaptureChecks();
                else if (args.Contains("--youtube-gate")) await RunYouTubeGateChecks();
                else if (args.Contains("--capture-recovery")) await RunWindowCaptureRecoveryChecks();
                else if (args.Contains("--capture-local")) await RunLocalCaptureChecks();
                else if (args.Contains("--capture-to-file")) await RunLocalCaptureChecks(switchToFile: true);
                else if (args.Contains("--persistent-media")) await RunPersistentMediaChecks();
                else if (args.Contains("--split-media")) await RunSplitMediaChecks();
                else if (args.Contains("--ended-media")) await RunEndedMediaChecks();
                else if (args.Contains("--window-capture")) await RunWindowCaptureChecks();
                else if (args.Contains("--capture-integration")) await RunWindowCaptureIntegrationChecks();
                else await Run(args.Contains("--audio-fades"), args.Contains("--backdrops"), args.Contains("--bible-styles"), args.Contains("--global-transitions"), args.Contains("--dissolves"), args.Contains("--smooth-transitions")); File.WriteAllLines(ResultPath, Results); app.Shutdown(0); }
            catch (Exception ex) { File.WriteAllLines(ResultPath, Results.Append(ex.ToString())); app.Shutdown(1); }
        };
        app.Run();
    }
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Results.Add("PASS: " + name);
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject value)
    {
        yield return value;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(value, i))) yield return child;
    }
    static async Task Run(bool audioOnly = false, bool backdropsOnly = false, bool bibleStylesOnly = false, bool globalTransitionsOnly = false, bool dissolvesOnly = false, bool smoothOnly = false)
    {
        if (smoothOnly) { await RunSmoothTransitionChecks(); return; }
        using var output = new ProjectionSurface();
        using var preview = new ProjectionSurface(true);
        var window = new ProjectionWindow(output) { Width = 960, Height = 600, Left = 20, Top = 20 };
        var display = DisplayService.GetDisplays().First(d => d.IsPrimary);
        DisplayService.PlaceWindow(window, display, fullscreen: false);
        window.Show();
        var previewWindow = new Window { Content = preview, Width = 480, Height = 300, Left = 1000, Top = 20 };
        previewWindow.Show();
        // Projection normally avoids stealing focus. The smoke host must stay visible:
        // WPF can throttle animation rendering when another desktop app occludes it.
        window.Topmost = true;
        window.Activate();
        if (dissolvesOnly)
        {
            await RunDissolveChecks(output,
                new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") });
            window.Close(); previewWindow.Close(); return;
        }
        if (globalTransitionsOnly)
        {
            await RunGlobalTransitionChecks(output, preview,
                new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") });
            window.Close(); previewWindow.Close(); return;
        }
        if (bibleStylesOnly)
        {
            await RunBibleStyleChecks(output, preview);
            window.Close(); previewWindow.Close(); return;
        }
        if (backdropsOnly)
        {
            await RunBackdropTransitionChecks(output, preview,
                new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") });
            window.Close(); previewWindow.Close(); return;
        }
        if (audioOnly)
        {
            await RunAudioFadeChecks(output, preview,
                new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv") },
                new Cue { Kind = CueKind.Audio, FilePath = Path.Combine(Folder, "audio.wav") });
            window.Close(); previewWindow.Close(); return;
        }
        var errors = new List<string>(); output.PlaybackError += errors.Add;
        await Task.Delay(100);
        Check(Descendants(output).OfType<TextBlock>().All(b => b.Text != "Vista previa"), "Empty output contains no preview label");
        Check(GetWindowRect(new WindowInteropHelper(window).Handle, out var windowRect) && windowRect.Left >= display.Left && windowRect.Top >= display.Top && windowRect.Right <= display.Left + display.Width && windowRect.Bottom <= display.Top + display.Height, "Windowed output fits selected physical display");
        var verse = new Cue { Kind = CueKind.Bible, Body = string.Join(" ", Enumerable.Repeat("Texto sintético de prueba para verificar el ajuste de un pasaje extenso.", 12)), Reference = "Prueba 1:1", Version = "Versión sintética — atribución de prueba" };
        output.ShowCue(verse); await Task.Delay(300);
        var blocks = Descendants(output).OfType<TextBlock>().ToArray();
        Check(blocks.Any(b => b.Text == verse.Body && b.TextWrapping == TextWrapping.Wrap), "Long verse wraps");
        var verseBody = blocks.Single(b => b.Text == verse.Body);
        Check(verseBody.TransformToAncestor(output).TransformBounds(new Rect(0, 0, verseBody.ActualWidth, verseBody.ActualHeight)).Width > output.ActualWidth * .8, "Long verse uses projection width");
        foreach (var block in blocks.Where(b => !string.IsNullOrEmpty(b.Text)))
        {
            var bounds = block.TransformToAncestor(output).TransformBounds(new Rect(0, 0, block.ActualWidth, block.ActualHeight));
            Check(bounds.Top >= 0 && bounds.Bottom <= output.ActualHeight, $"Text and citation stay inside surface ({bounds}; output {output.ActualWidth}x{output.ActualHeight}; font {block.FontSize})");
        }
        Check(blocks.Any(b => b.Text.Contains(verse.Reference)) && blocks.Any(b => b.Text.Contains(verse.Version)), "Reference and version visible");
        CaptureService.CaptureDisplay(DisplayService.GetDisplays().First(d => d.IsPrimary), Path.Combine(Folder, "text.png"));
        output.ShowCue(new Cue { Kind = CueKind.Image, FilePath = Path.Combine(Folder, "panorama.png") });
        await Task.Delay(250);
        Check(Descendants(output).OfType<System.Windows.Controls.Image>().Any(i => i.Source != null && i.Stretch == Stretch.Uniform), "Image keeps aspect ratio");
        CaptureService.CaptureDisplay(DisplayService.GetDisplays().First(d => d.IsPrimary), Path.Combine(Folder, "image.png"));
        var video = new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv"), Title = "MKV sintético" };
        output.ShowCue(video); preview.ShowCue(video); preview.Volume = 100;
        for (int i = 0; i < 40 && (output.Duration <= 0 || output.Position < .3); i++) await Task.Delay(100);
        Check(output.Duration >= 2.8 && output.Duration <= 3.5, "Duration in seconds");
        Check(output.Position > .2 && output.Position < 3.5, "Elapsed position in seconds");
        var previewPlayer = Descendants(preview).OfType<VideoView>().Single().MediaPlayer!;
        Check(preview.Volume == 0 && (previewPlayer.Mute || previewPlayer.AudioTrack == -1), "Preview has no audible track");
        output.ShowCue(video, blackout: true); await Task.Delay(250);
        double pausedPosition = output.Position;
        Check(!output.IsPlaying, "Blackout pauses video");
        output.ShowCue(video); await Task.Delay(350);
        Check(!output.IsPlaying && Math.Abs(output.Position - pausedPosition) < .2, "Restoring blackout keeps position paused");
        output.SetPaused(false); await Task.Delay(250); Check(output.IsPlaying, "Explicit play resumes");
        output.ShowCue(video, hideText: true); await Task.Delay(200); output.ShowCue(video); await Task.Delay(150);
        Check(!output.IsPlaying, "Background restoration remains paused");
        output.Seek(.5); await Task.Delay(200); Check(Math.Abs(output.Position - 1.5) < .5, "Seek uses fraction");
        output.Restart(); await Task.Delay(300); Check(output.Position < 1, "Restart returns to beginning");
        CaptureService.CaptureDisplay(DisplayService.GetDisplays().First(d => d.IsPrimary), Path.Combine(Folder, "video.png"));
        using (var capture = new System.Drawing.Bitmap(Path.Combine(Folder, "video.png")))
            Check(capture.Width == display.Width && capture.Height == display.Height, "Capture dimensions match physical selected display");
        // Native playback startup varies with machine load. Observe a real end
        // and a second Playing event rather than assuming both occur at 3.4 s.
        var outputPlayer = (LibVLCSharp.Shared.MediaPlayer)typeof(ProjectionSurface)
            .GetField("_player", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(output)!;
        int ended = 0, started = 0;
        EventHandler<EventArgs> onEnd = (_, _) => System.Threading.Interlocked.Increment(ref ended);
        EventHandler<EventArgs> onPlay = (_, _) => System.Threading.Interlocked.Increment(ref started);
        outputPlayer.EndReached += onEnd; outputPlayer.Playing += onPlay;
        try
        {
            output.Loop = true; output.Restart();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline && !(System.Threading.Volatile.Read(ref ended) > 0 && System.Threading.Volatile.Read(ref started) >= 2 && output.IsPlaying))
                await Task.Delay(50);
            Check(ended > 0 && started >= 2 && output.IsPlaying, $"Media loops after end (ends={ended}, starts={started}, state={outputPlayer.State}, position={output.Position:0.00})");
        }
        finally { output.Loop = false; outputPlayer.EndReached -= onEnd; outputPlayer.Playing -= onPlay; }
        var audio = new Cue { Kind = CueKind.Audio, FilePath = Path.Combine(Folder, "audio.wav"), Title = "Audio sintético" };
        output.ShowCue(audio, blackout: true); output.ShowCue(audio); await Task.Delay(150);
        Check(!output.IsPlaying, "Initially blacked-out media stays paused on restore");
        output.SetPaused(false); await Task.Delay(400);
        Check(output.IsPlaying && output.Duration > 2.8, "Audio cue plays after explicit resume");
        output.SetPaused(true);
        output.ShowCue(new Cue { Kind = CueKind.Image, FilePath = Path.Combine(Folder, "missing.png") });
        Check(errors.Count == 1, "Invalid media reports error");
        File.WriteAllText(Path.Combine(Folder, "corrupt.png"), "invalid image data");
        output.ShowCue(new Cue { Kind = CueKind.Image, FilePath = Path.Combine(Folder, "corrupt.png") });
        Check(errors.Count == 2, "Corrupt image reports recoverable error");
        await RunDissolveChecks(output, video);
        await RunAudioFadeChecks(output, preview, video, audio);
        await RunBackdropTransitionChecks(output, preview, video);
        await RunBibleStyleChecks(output, preview);
        await RunGlobalTransitionChecks(output, preview, video);
        window.Close(); previewWindow.Close();
    }
    static async Task RunDissolveChecks(ProjectionSurface output, Cue video)
    {
        Cue Solid(string name, byte red, byte blue)
        {
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { blue, 0, red, 255 }, 4);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            var path = Path.Combine(Folder, name + ".png"); using var stream = File.Create(path); encoder.Save(stream);
            return new Cue { Kind = CueKind.Image, FilePath = path, Title = name };
        }
        byte[] Center()
        {
            output.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)output.ActualWidth, (int)output.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(output);
            var pixels = new byte[4]; bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixels, 4, 0);
            return pixels;
        }
        var red = Solid("dissolve-red", 255, 0); var blue = Solid("dissolve-blue", 0, 255);
        output.ShowCue(red);
        Check(Center()[2] > 240, "Dissolve starts from the rendered outgoing image");
        output.TransitionTo(blue);
        Check(output.IsTransitioning, "Dissolve retains the outgoing image immediately");
        // WPF starts its animation clock on a render tick, not when BeginAnimation
        // returns. Observe an actual blended frame instead of assuming a wall-clock midpoint.
        var overlay = (Image)typeof(ProjectionSurface).GetField("_dissolve", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(output)!;
        var diagnostics = new List<string>();
        var blendDeadline = DateTime.UtcNow.AddSeconds(2);
        var mixed = Center();
        while (!(mixed[0] > 20 && mixed[2] > 20) && output.IsTransitioning && DateTime.UtcNow < blendDeadline)
        {
            diagnostics.Add($"{mixed[0]}/{mixed[2]},opacity={overlay.Opacity:0.000}");
            await Task.Delay(15);
            mixed = Center();
        }
        Check(mixed[0] > 20 && mixed[2] > 20, $"Dissolve blends real output pixels (blue={mixed[0]}, red={mixed[2]}; samples={string.Join("; ", diagnostics)})");
        var finishDeadline = DateTime.UtcNow.AddSeconds(2);
        while (output.IsTransitioning && DateTime.UtcNow < finishDeadline) await Task.Delay(15);
        var final = Center();
        Check(!output.IsTransitioning && final[0] > 240 && final[2] < 10, "Dissolve completes and releases the outgoing frame");
        output.TransitionTo(red); await Task.Delay(80);
        output.ShowCue(red, blackout: true); await Task.Delay(450);
        var black = Center();
        Check(!output.IsTransitioning && black[0] + black[1] + black[2] == 0, "Blackout cancels a dissolve immediately and cannot reveal old content");
        output.ShowCue(red);
        output.TransitionTo(video); output.SetPaused(false);
        Check(output.IsTransitioning && Center()[2] > 240, "Video loading retains the previous output frame");
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (output.IsTransitioning && DateTime.UtcNow < timeout) await Task.Delay(50);
        var rendered = Descendants(output).OfType<CompositedVideo>().Single();
        Check(!output.IsTransitioning && rendered.Source is not null && output.IsPlaying, "Native MKV frames enter and finish the output dissolve");
        output.TransitionTo(blue); await Task.Delay(60); output.TransitionTo(red);
        // A playing video now fades its audio out before the visual dissolve starts.
        var rapidDeadline = DateTime.UtcNow.AddSeconds(3);
        while (output.IsTransitioning && DateTime.UtcNow < rapidDeadline) await Task.Delay(25);
        Check(!output.IsTransitioning && Center()[2] > 240, "Rapid consecutive transitions finish on the newest cue");
        output.TransitionTo(video); output.ShowCue(video, blackout: true); await Task.Delay(550);
        Check(!output.IsTransitioning && !output.IsPlaying && Center().Take(3).All(x => x == 0), "Pending video frames cannot restore output after blackout");
    }
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
}
