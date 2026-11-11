using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

internal static partial class Program
{
    static async Task RunBackdropTransitionChecks(ProjectionSurface output, ProjectionSurface preview, Cue video)
    {
        var transition = typeof(ProjectionSurface).GetMethod("TransitionToBackdrop");
        var background = typeof(ProjectionSurface).GetProperty("BackgroundImagePath");
        if (transition is null || background is null)
            throw new Exception("FAIL: Renderer does not support animated black and configured-image backdrops");
        void Backdrop(Cue? live, bool blackout, bool hideText) => transition.Invoke(output, new object?[] { live, blackout, hideText });
        void Background(string? path) => background.SetValue(output, path);
        var directory = Path.Combine(Path.GetTempPath(), "CultoBackdropSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        object? Field(ProjectionSurface surface, string name) => typeof(ProjectionSurface).GetField(name, fields)!.GetValue(surface);
        MediaPlayer Player(ProjectionSurface surface) => (MediaPlayer)Field(surface, "_player")!;
        string Solid(string name, byte red, byte green, byte blue)
        {
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { blue, green, red, 255 }, 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(directory, name + ".png");
            using var stream = File.Create(path); encoder.Save(stream); return path;
        }
        byte[] Center()
        {
            output.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)output.ActualWidth, (int)output.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(output);
            var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            return pixel;
        }
        async Task UntilBackdrop(Func<bool> condition, string failure)
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(15);
            if (!condition()) throw new Exception("FAIL: " + failure);
        }
        var redPath = Solid("red", 255, 0, 0); var greenPath = Solid("church", 0, 255, 0);
        var red = new Cue { Title = "Contenido rojo", Kind = CueKind.Image, FilePath = redPath };
        try
        {
            output.SetPaused(true); output.ShowCue(red); Background(greenPath);
            Backdrop(null, blackout: true, hideText: false);
            Check(output.IsTransitioning && Center()[2] > 240, "Black backdrop retains the actual outgoing frame before dissolving");
            var reds = new List<byte>();
            await UntilBackdrop(() => { reds.Add(Center()[2]); return !output.IsTransitioning; }, "Black backdrop dissolve did not finish");
            Check(reds.Any(value => value > 10 && value < 245) && Center().Take(3).All(value => value == 0),
                "Black backdrop blends real pixels and finishes fully black without a live cue");

            Backdrop(null, blackout: false, hideText: true);
            await UntilBackdrop(() => !output.IsTransitioning, "Church backdrop dissolve did not finish");
            var church = Center();
            Check(church[1] > 240 && church[0] < 10 && church[2] < 10,
                "Church backdrop renders the configured image without a live cue");

            Background(Path.Combine(directory, "missing.png"));
            Backdrop(null, blackout: false, hideText: true);
            await UntilBackdrop(() => !output.IsTransitioning, "Fallback backdrop dissolve did not finish");
            var fallback = Center();
            Check(Math.Abs(fallback[2] - 16) < 3 && Math.Abs(fallback[1] - 23) < 3 && Math.Abs(fallback[0] - 34) < 3,
                "Unavailable church image falls back to the normal background instead of stale image or black");

            Background(greenPath);
            Backdrop(null, blackout: true, hideText: false); await Task.Delay(45);
            Backdrop(null, blackout: false, hideText: true);
            await UntilBackdrop(() => !output.IsTransitioning, "Latest backdrop did not finish");
            await Task.Delay(450);
            Check(Center()[1] > 240, "A replaced black transition cannot override the latest church backdrop");
            Backdrop(null, blackout: true, hideText: false); await Task.Delay(45);
            output.ShowCue(red); await Task.Delay(500);
            Check(!output.IsTransitioning && Center()[2] > 240 && Center()[1] < 10,
                "An immediate content selection cancels an unfinished backdrop dissolve");

            var live = new Cue { Title = "Video conservado", Kind = CueKind.Video, FilePath = video.FilePath };
            var prepared = new Cue { Title = "Vista previa independiente", Kind = CueKind.Video, FilePath = video.FilePath };
            output.Volume = 70; output.ShowCue(live); output.SetPaused(false); preview.ShowCue(prepared);
            await UntilBackdrop(() => output.IsPlaying && output.Position > .25 && Player(output).Volume == 70 && preview.IsPlaying,
                "Live and preview video fixtures failed to start");
            var media = Field(output, "_media"); var previewMedia = Field(preview, "_media");
            var before = output.Position; var volumes = new List<int>();
            Backdrop(live, blackout: true, hideText: false);
            await UntilBackdrop(() => { volumes.Add(Player(output).Volume); return !output.IsTransitioning; }, "Video-to-black transition did not finish");
            var pausedPosition = output.Position;
            Check(volumes.Any(value => value > 0 && value < 70) && Player(output).Volume == 0,
                "Animated blackout fades the real outgoing video audio before becoming silent");
            Check(output.IsPaused && !output.IsPlaying && ReferenceEquals(media, Field(output, "_media"))
                && pausedPosition >= before - .1 && pausedPosition < before + 1,
                "Animated blackout preserves the same video media and pauses near its existing position");
            Check(ReferenceEquals(previewMedia, Field(preview, "_media")) && preview.IsPlaying && preview.Volume == 0,
                "Changing the live backdrop preserves the independent muted preview");
            await Task.Delay(200);
            Backdrop(live, blackout: false, hideText: true);
            await UntilBackdrop(() => !output.IsTransitioning, "Paused video-to-church transition did not finish");
            Check(!output.IsPlaying && Player(output).Volume == 0 && Math.Abs(output.Position - pausedPosition) < .2 && Center()[1] > 240,
                "Switching between backdrops keeps the underlying video paused at its position");
            Backdrop(live, blackout: false, hideText: false);
            await UntilBackdrop(() => !output.IsTransitioning, "Restoring paused live content did not finish");
            Check(!output.IsPlaying && output.IsPaused && Player(output).Volume == 0 && ReferenceEquals(media, Field(output, "_media"))
                && Math.Abs(output.Position - pausedPosition) < .2,
                "Restoring content reveals the same paused video without restarting audio");
            output.SetPaused(false);
            await UntilBackdrop(() => output.IsPlaying && Player(output).Volume == 70, "Restored video failed to resume explicitly");
            Check(output.Position >= pausedPosition - .15, "Explicit play after backdrop resumes the retained position instead of restarting");

            var beforeRapidRestore = output.Position;
            Backdrop(live, blackout: true, hideText: false);
            await Task.Delay(35);
            Backdrop(live, blackout: false, hideText: false);
            await UntilBackdrop(() => !output.IsTransitioning, "Rapid backdrop restoration did not finish");
            var rapidRestorePosition = output.Position;
            Check(output.IsPaused && !output.IsPlaying && Player(output).Volume == 0 && ReferenceEquals(media, Field(output, "_media"))
                && rapidRestorePosition >= beforeRapidRestore - .15 && rapidRestorePosition < beforeRapidRestore + 1,
                "Restoring before the outgoing fade finishes still retains the same video paused and silent");
            await Task.Delay(500);
            Check(output.IsPaused && !output.IsPlaying && Player(output).Volume == 0 && ReferenceEquals(media, Field(output, "_media"))
                && Math.Abs(output.Position - rapidRestorePosition) < .2 && !output.IsTransitioning,
                "Cancelled backdrop callbacks cannot resume or unmute rapidly restored video later");

            output.Restart();
            await UntilBackdrop(() => output.IsPlaying && Player(output).Volume == 70, "Play-during-restore fixture did not start");
            Backdrop(live, blackout: true, hideText: false); await Task.Delay(35);
            Backdrop(live, blackout: false, hideText: false);
            output.SetPaused(false);
            Check(!output.IsPaused, "Explicit play overrides the paused intent of a pending backdrop restoration");
            await UntilBackdrop(() => !output.IsTransitioning && output.IsPlaying && Player(output).Volume == 70,
                "Explicit play during pending restoration was discarded");
            await Task.Delay(250);
            Check(output.IsPlaying && !output.IsPaused && Player(output).Volume == 70 && ReferenceEquals(media, Field(output, "_media")),
                "A pending restore cannot pause retained media after explicit play");

            output.Restart();
            await UntilBackdrop(() => output.IsPlaying && output.Position > .8 && Player(output).Volume == 70,
                "Restart-during-restore fixture did not advance");
            Backdrop(live, blackout: true, hideText: false); await Task.Delay(35);
            Backdrop(live, blackout: false, hideText: false);
            output.Restart();
            Check(!output.IsPaused, "Explicit restart overrides the paused intent of a pending backdrop restoration");
            await UntilBackdrop(() => !output.IsTransitioning && output.IsPlaying && Player(output).Volume == 70,
                "Explicit restart during pending restoration was discarded");
            Check(output.Position < 1.25 && ReferenceEquals(media, Field(output, "_media")),
                "Restart during pending restoration rewinds the retained media instead of just resuming");

            Backdrop(live, blackout: true, hideText: false); await Task.Delay(35);
            output.Restart(); output.SetPaused(false);
            await UntilBackdrop(() => !output.IsTransitioning, "Suppressed pending backdrop did not finish");
            Check(output.IsPaused && !output.IsPlaying && Player(output).Volume == 0 && Center().Take(3).All(value => value == 0),
                "Play and restart cannot unsuppress a pending black backdrop");
        }
        finally
        {
            output.SetPaused(true); preview.SetPaused(true); Background(null); output.ShowCue(null);
            File.Delete(redPath); File.Delete(greenPath); Directory.Delete(directory);
        }
    }
}
