using System;
using System.Collections.Generic;
using System.IO;
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
    static async Task RunLiveLayerMoveChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 0, Loop = true,
            Transitions = new TransitionOptions { Kind = "Move", DurationMilliseconds = 1800, AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
        var errors = new List<string>(); output.PlaybackError += errors.Add;
        var window = new Window { Content = output, Width = 656, Height = 400, Topmost = true }; window.Show();
        var folder = Path.Combine(Path.GetTempPath(), "CultoLiveLayer-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var page = Path.Combine(folder, "source.html");
        File.WriteAllText(page, "<html><body style='margin:0;background:red'><script>let n=0;setInterval(()=>document.body.style.background=(++n%2?'red':'blue'),160)</script></body></html>");
        var source = new Cue { Kind = CueKind.BrowserSource, Title = "Fuente viva", Browser = new BrowserSourceOptions { Location = page, IsLocalFile = true, Width = 320, Height = 180 } };
        var first = new Cue { Kind = CueKind.Scene, Title = "Capas A", Scene = new SceneComposition { Width = 320, Height = 180, Layers =
            [new SceneLayer { SourceId = "shared-browser", Source = source, X = 0, Y = 50, Width = 120, Height = 70 }] } };
        var second = first.Copy(newId: true); second.Title = "Capas B"; second.Scene!.Layers[0].X = 180;
        async Task Until(Func<bool> ready, string failure, int timeout = 15000)
        { var end = DateTime.UtcNow.AddMilliseconds(timeout); while (!ready() && errors.Count == 0 && DateTime.UtcNow < end) await Task.Delay(20); Check(ready() && errors.Count == 0, failure + " " + string.Join(" | ", errors)); }
        try
        {
            output.ShowCue(first);
            await Until(() => output.IsPlaying && !output.IsTransitioning, "Layered browser scene starts and remains live");
            var sharedSurface = output.ActiveSurface.GetLiveLayers().Single().Surface;
            output.TransitionTo(second);
            await Until(() => output.DisplayedCue?.Id == second.Id, "Incoming layered scene is selected");
            Check(ReferenceEquals(sharedSurface, output.ActiveSurface.GetLiveLayers().Single().Surface), "Shared browser source keeps the same running renderer across scenes");
            var colors = new HashSet<string>(); var centers = new List<double>();
            var end = DateTime.UtcNow.AddSeconds(20);
            while (output.IsTransitioning && errors.Count == 0 && DateTime.UtcNow < end)
            {
                foreach (var overlay in Descendants(output).OfType<Image>().Where(image => image.Visibility == Visibility.Visible && Panel.GetZIndex(image) == 1000))
                {
                    if (overlay.Source is not BitmapSource frame) continue;
                    var pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4]; frame.CopyPixels(pixels, frame.PixelWidth * 4, 0);
                    long xTotal = 0; int count = 0;
                    for (int y = 0; y < frame.PixelHeight; y += 3)
                    for (int x = 0; x < frame.PixelWidth; x += 3)
                    {
                        var i = (y * frame.PixelWidth + x) * 4;
                        if (pixels[i + 2] > 200 && pixels[i] < 40 && pixels[i + 1] < 40) { colors.Add("red"); xTotal += x; count++; }
                        if (pixels[i] > 200 && pixels[i + 2] < 40 && pixels[i + 1] < 40) { colors.Add("blue"); xTotal += x; count++; }
                    }
                    if (count > 10) centers.Add(xTotal / (double)count);
                }
                await Task.Delay(35);
            }
            Check(errors.Count == 0 && !output.IsTransitioning, "Native live-layer Move finishes without falling back " + string.Join(" | ", errors));
            Check(colors.Count == 2, "Browser pixels keep changing inside the real native Move animation");
            Check(centers.Count > 4 && centers.Max() - centers.Min() > output.ActualWidth * .3, "Matched live source moves between the two scene positions");
            Check(output.PresentedTransitions.Kind == "Move", "Live browser Move is not replaced with dissolve");
            Check(ReferenceEquals(sharedSurface, output.ActiveSurface.GetLiveLayers().Single().Surface), "Completed Move does not restart shared browser");
            output.Transitions = new TransitionOptions { Kind = "Cut", AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 };
            output.TransitionToBackdrop(second, true, false);
            await Until(() => !output.IsTransitioning && output.DisplayedBlackout, "Layered scene can cut to black");
        }
        finally { window.Close(); }
    }
}
