using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    static async Task RunNativeMoveChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 0,
            Transitions = new TransitionOptions { Kind = "Move", DurationMilliseconds = 900, AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0,
                Move = new MoveOptions { In = new MoveItemOptions { Position = 72, Zoom = 100, Easing = 0 },
                    Out = new MoveItemOptions { Position = 68, Zoom = 100, Easing = 0 } } } };
        var errors = new List<string>(); output.PlaybackError += errors.Add;
        var window = new Window { Content = output, Width = 640, Height = 400, Topmost = true };
        output.Width = 621; output.Height = 359; // Exercise OBS width and height alignment independently of window chrome.
        window.Show(); window.UpdateLayout();
        var folder = Path.Combine(Path.GetTempPath(), "CultoMoveTest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        Cue Image(string name, byte r, byte b)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null,
                Enumerable.Range(0, 256).SelectMany(_ => new byte[] { b, 0, r, 255 }).ToArray(), 64)));
            var path = Path.Combine(folder, name + ".png"); using (var file = File.Create(path)) encoder.Save(file);
            return new Cue { Kind = CueKind.Image, FilePath = path, Title = name };
        }
        try
        {
            var red = Image("red", 255, 0); var blue = Image("blue", 0, 255);
            output.ShowCue(red); await Task.Delay(200); window.UpdateLayout();
            output.TransitionTo(blue);
            var overlays = new HashSet<ImageSource>();
            var deadline = DateTime.UtcNow.AddSeconds(40);
            while (output.IsTransitioning && errors.Count == 0 && DateTime.UtcNow < deadline)
            {
                foreach (var image in Descendants(output).OfType<System.Windows.Controls.Image>())
                    if (image.Visibility == Visibility.Visible && image.Source is BitmapSource source && source.PixelWidth >= 620) overlays.Add(source);
                await Task.Delay(20);
            }
            Check(errors.Count == 0, "Move engine reports no error: " + string.Join(" | ", errors));
            Check(!output.IsTransitioning, "Native Move finishes rather than freezing the outgoing frame");
            Check(overlays.Count > 3, "Native Move delivers multiple animation frames at an unaligned viewport size");
            bool Direction(ImageSource source, bool blueOnLeft)
            {
                var frame = (BitmapSource)source; var left = new byte[4]; var right = new byte[4];
                frame.CopyPixels(new Int32Rect(frame.PixelWidth / 4, frame.PixelHeight / 2, 1, 1), left, 4, 0);
                frame.CopyPixels(new Int32Rect(frame.PixelWidth * 3 / 4, frame.PixelHeight / 2, 1, 1), right, 4, 0);
                return blueOnLeft ? left[0] > 200 && left[2] < 30 && right[2] > 200 && right[0] < 30
                    : left[2] > 200 && left[0] < 30 && right[0] > 200 && right[2] < 30;
            }
            Check(overlays.Any(source => Direction(source, false)), "Configured native swipe enters from the right without shrinking");
            var bitmap = new RenderTargetBitmap((int)output.ActualWidth, (int)output.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            output.UpdateLayout(); bitmap.Render(output); var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            Check(pixel[0] > 200 && pixel[2] < 30, "Native Move displays its destination image");
            output.Transitions = output.Transitions with { Move = new MoveOptions
            { In = new MoveItemOptions { Position = 68, Zoom = 100, Easing = 0 }, Out = new MoveItemOptions { Position = 72, Zoom = 100, Easing = 0 } } };
            output.ShowCue(red); await Task.Delay(80); output.TransitionTo(blue); overlays.Clear();
            deadline = DateTime.UtcNow.AddSeconds(40);
            while (output.IsTransitioning && errors.Count == 0 && DateTime.UtcNow < deadline)
            {
                foreach (var image in Descendants(output).OfType<System.Windows.Controls.Image>())
                    if (image.Visibility == Visibility.Visible && image.Source is BitmapSource source && source.PixelWidth >= 620) overlays.Add(source);
                await Task.Delay(20);
            }
            Check(errors.Count == 0 && !output.IsTransitioning && overlays.Any(source => Direction(source, true)),
                "Changing saved Move settings reverses the actual native animation");
            output.Loop = true;
            output.TransitionTo(new Cue { Kind = CueKind.Video, FilePath = Path.Combine(Folder, "test.mkv"), Title = "Video Move" });
            deadline = DateTime.UtcNow.AddSeconds(40);
            while (output.IsTransitioning && errors.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(25);
            Check(errors.Count == 0 && !output.IsTransitioning && output.IsPlaying, "Configured Move into video finishes and returns to live playback");
            output.TransitionTo(blue);
            deadline = DateTime.UtcNow.AddSeconds(40);
            while (output.IsTransitioning && errors.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(25);
            Check(errors.Count == 0 && !output.IsTransitioning && output.DisplayedCue?.Id == blue.Id, "Configured Move exits video into its destination image");
            var mixer = typeof(ProjectionSurface).GetField("_mixer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(output)!;
            var moveClient = (IDisposable?)mixer.GetType().GetField("_liveMoveClient", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(mixer);
            Check(moveClient is not null, "Audit reaches the native Move worker");
            moveClient!.Dispose(); // Simulate a failed effect engine, not a failed media file.
            output.TransitionTo(red);
            deadline = DateTime.UtcNow.AddSeconds(10);
            while (output.IsTransitioning && DateTime.UtcNow < deadline) await Task.Delay(20);
            output.UpdateLayout();
            bitmap = new RenderTargetBitmap((int)output.ActualWidth, (int)output.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(output);
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            Check(errors.Count == 1 && pixel[2] > 200 && pixel[0] < 30, "Failed Move effect reports its error while keeping destination content visible");
            errors.Clear(); output.TransitionTo(blue);
            deadline = DateTime.UtcNow.AddSeconds(15);
            while (output.IsTransitioning && DateTime.UtcNow < deadline) await Task.Delay(20);
            Check(errors.Count == 0 && !output.IsTransitioning, "Next Move starts a fresh worker after an effect failure");
            for (var function = 1; function <= 10; function++)
            {
                output.Transitions = output.Transitions with { DurationMilliseconds = 150, Move = new MoveOptions
                { In = new MoveItemOptions { Position = 18, Zoom = function % 2 == 0 ? 200 : 0, Curve = function % 2 == 0 ? 2 : -2, Easing = 3, Function = function },
                    Out = new MoveItemOptions { Position = 34, Zoom = 100, Curve = .5, Easing = 3, Function = function } } };
                output.ShowCue(red); await Task.Delay(30); output.TransitionTo(blue);
                deadline = DateTime.UtcNow.AddSeconds(8);
                while (output.IsTransitioning && DateTime.UtcNow < deadline) await Task.Delay(15);
                Check(errors.Count == 0 && !output.IsTransitioning && output.DisplayedCue?.Id == blue.Id,
                    $"Native easing function {function} accepts configured zoom and curve extremes");
            }
            output.Transitions = output.Transitions with { DurationMilliseconds = 1500 };
            output.ShowCue(red); await Task.Delay(30); output.TransitionTo(blue); await Task.Delay(100);
            output.Transitions = output.Transitions with { Kind = "Cut" };
            output.TransitionTo(red); await Task.Delay(150);
            output.UpdateLayout();
            bitmap = new RenderTargetBitmap((int)output.ActualWidth, (int)output.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(output);
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            Check(errors.Count == 0 && !output.IsTransitioning && pixel[2] > 200 && pixel[0] < 30,
                "Cut interrupts native Move and stale frames cannot overwrite its destination");
        }
        finally { window.Close(); }
    }
}
