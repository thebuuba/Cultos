using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static async Task RunVideoAspectChecks()
    {
        var path = Environment.GetEnvironmentVariable("CULTO_ASPECT_VIDEO") ?? Path.Combine(Folder, "test.mkv");
        using var surface = new ProjectionSurface(smoothTransitions: true) { Volume = 0, Width = 1920, Height = 1080 };
        var host = new Window { Content = surface, Width = 960, Height = 540, Topmost = true };
        host.Show(); host.Activate();
        try
        {
            surface.ShowCue(new Cue { Kind = CueKind.Video, FilePath = path });
            await Task.Delay(10000);
            var video = (CompositedVideo)typeof(ProjectionSurface).GetField("_compositedVideo", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(surface.ActiveSurface)!;
            Check(video.FrameSize.Width > 0, $"Video produced a decoded frame: {path}, playing={surface.IsPlaying}, position={surface.Position}, size={video.ActualWidth}x{video.ActualHeight}");
            Check(Math.Abs((double)video.FrameSize.Width / video.FrameSize.Height - 16d / 9) < .01,
                $"16:9 source retains its aspect ratio after decoding ({video.FrameSize.Width}x{video.FrameSize.Height})");
        }
        finally { host.Close(); }
    }
}
