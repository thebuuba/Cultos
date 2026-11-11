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

internal static class Program
{
    static readonly string Folder = Path.GetFullPath("tests/Culto.Renderer.Smoke/fixtures");
    static readonly List<string> Results = [];
    [STAThread]
    static void Main()
    {
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try { await Run(); File.WriteAllLines(Path.Combine(Folder, "result.txt"), Results); app.Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Folder, "result.txt"), ex.ToString()); app.Shutdown(1); }
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
    static async Task Run()
    {
        using var output = new ProjectionSurface();
        using var preview = new ProjectionSurface(true);
        var window = new ProjectionWindow(output) { Width = 960, Height = 600, Left = 20, Top = 20 };
        var display = DisplayService.GetDisplays().First(d => d.IsPrimary);
        DisplayService.PlaceWindow(window, display, fullscreen: false);
        window.Show();
        var previewWindow = new Window { Content = preview, Width = 480, Height = 300, Left = 1000, Top = 20 };
        previewWindow.Show();
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
        output.Loop = true; output.Restart(); await Task.Delay(3400);
        Check(output.IsPlaying && output.Position < 1.5, "Media loops after end"); output.Loop = false;
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
        window.Close(); previewWindow.Close();
    }
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
}
