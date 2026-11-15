using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    static Task RunLayerSourcePoolChecks()
    {
        using var pool = new LayerSourcePool();
        using var outgoing = new LayerSceneCanvas(pool);
        using var incoming = new LayerSceneCanvas(pool);
        var source = new Cue { Kind = CueKind.Text, Body = "Fuente compartida" };
        var scene = new SceneComposition { Width = 1280, Height = 720,
            Layers = [new SceneLayer { SourceId = "shared", Source = source, Width = 1280, Height = 720 }] };
        outgoing.SetScene(scene);
        outgoing.ApplyPlayback(80, false, true);
        var original = pool.Entries.Single().Surface;
        Check(original.Volume == 80 && !original.IsPaused, "layer pool applies outgoing playback");
        incoming.SetScene(scene.Copy());
        incoming.ApplyPlayback(60, false, false);
        outgoing.ApplyPlayback(20, true, true);
        Check(pool.Entries.Count() == 1 && ReferenceEquals(pool.Entries.Single().Surface, original), "shared layer has exactly one live source");
        Check(original.Volume == 60 && !original.IsPaused, "paused outgoing owner cannot silence shared incoming source");
        var changed = scene.Copy();
        changed.Layers[0].X = 120;
        incoming.SetScene(changed);
        Check(ReferenceEquals(pool.Entries.Single().Surface, original), "layer transform changes preserve running source");
        incoming.ApplyPlayback(0, false, false);
        Check(original.Volume == 0 && !original.IsPaused, "zero output gain keeps shared visual running");
        incoming.ApplyPlayback(60, true, false);
        Check(original.IsPaused, "shared source pauses only when every owner pauses");
        outgoing.Dispose();
        Check(pool.Entries.Count() == 1, "retiring outgoing scene retains shared incoming source");
        incoming.Dispose();
        Check(!pool.Entries.Any(), "last scene releases its source");

        using var standalone = new ProjectionSurface();
        var sceneCue = new Cue { Kind = CueKind.Scene, Scene = scene };
        standalone.ShowCue(sceneCue);
        var host = new Grid { Width = 640, Height = 360 };
        host.Children.Add(standalone);
        host.Measure(new Size(640, 360)); host.Arrange(new Rect(0, 0, 640, 360)); host.UpdateLayout();
        var rendered = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(host);
        var pixel = new byte[4];
        rendered.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
        Check(pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 0 && pixel[3] == 255, "scene background covers live source depot");
        standalone.ShowCue(sceneCue, blackout: true);
        host.UpdateLayout();
        rendered = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(host);
        rendered.CopyPixels(new Int32Rect(320, 180, 1, 1), pixel, 4, 0);
        Check(pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 0 && pixel[3] == 255, "blackout covers live scene depot");
        return Task.CompletedTask;
    }
}
