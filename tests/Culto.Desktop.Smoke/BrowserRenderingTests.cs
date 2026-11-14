using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunBrowserRenderingTests(MainWindow owner)
    {
        Test("Browser scene transitions, blackout restoration, reload and retirement", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "culto-browser-scene-" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(path, "<html><body><div style='width:240px;height:180px;background:lime'></div></body></html>");
            using var output = new ProjectionSurface(smoothTransitions: true)
            { Transitions = new TransitionOptions { Kind = "Move", DurationMilliseconds = 100 } };
            var host = new Window { Owner = owner, Width = 350, Height = 230, Content = output, ShowInTaskbar = false };
            var cue = new Cue { Title = "HTML scene", Kind = CueKind.BrowserSource,
                Browser = new BrowserSourceOptions { Location = path, IsLocalFile = true, Width = 320, Height = 180 } };
            try
            {
                host.Show(); output.TransitionTo(cue);
                Until(() => output.IsPlaying && !output.IsTransitioning, "Browser scene never became live");
                Check(output.PresentedTransitions.Kind == "Dissolve", "Browser Move did not use its live transition fallback");
                var browser = Field<BrowserSourceView>(output.ActiveSurface, "_browserSource");
                output.TransitionTo(cue);
                Check(ReferenceEquals(browser, Field<BrowserSourceView>(output.ActiveSurface, "_browserSource")), "Selecting the same scene recreated its browser");
                output.ShowCue(cue, blackout: true);
                Check(!output.IsPlaying, "Blackout left the browser scene playing");
                output.ShowCue(cue);
                Until(() => output.IsPlaying, "Revealing the browser did not resume its live scene");
                output.ReloadBrowserSource(cue.Id);
                Until(() => output.IsPlaying, "Scene reload did not recover");
                output.TransitionTo(new Cue { Title = "Text", Body = "Scene retired" });
                Until(() => !output.IsTransitioning, "Browser retirement transition did not finish");
                Check(!browser.IsReady, "Retired browser scene was not disposed");
            }
            finally { host.Close(); File.Delete(path); }
        });
        Test("Browser local HTML, CSS, animation, transparency and VisualBrush mirror", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "culto-browser-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "style.css"), "#title{position:absolute;width:120px;height:80px;background:rgb(255,0,0)}");
            var path = Path.Combine(directory, "title.html");
            File.WriteAllText(path, "<html><head><link rel='stylesheet' href='style.css'></head><body><div id='title'></div><script>setInterval(()=>document.getElementById('title').style.left=(Date.now()%1000<500?'0':'120px'),80)</script></body></html>");
            using var browser = new BrowserSourceView();
            var stage = new Grid { Width = 320, Height = 180, Background = Brushes.Blue };
            stage.Children.Add(browser);
            var mirror = new System.Windows.Shapes.Rectangle { Width = 320, Height = 180, Fill = new VisualBrush(stage) };
            var panel = new StackPanel(); panel.Children.Add(stage); panel.Children.Add(mirror);
            var host = new Window { Owner = owner, Width = 350, Height = 410, Content = panel, ShowInTaskbar = false };
            try
            {
                string? error = null; browser.Error += message => error = message;
                browser.SetSource(new BrowserSourceOptions { Location = path, IsLocalFile = true, Width = 320, Height = 180, CustomCss = "html,body { background:transparent!important;margin:0;overflow:hidden } #title { background:rgb(0,255,0)!important }" }, true);
                host.Show();
                Until(() => browser.IsReady || error is not null, "Browser did not deliver its first HTML frame");
                Check(error is null, "Browser load failed: " + error);
                byte[] Capture(FrameworkElement element)
                {
                    var image = new RenderTargetBitmap(320, 180, 96, 96, PixelFormats.Pbgra32); image.Render(element);
                    var pixels = new byte[320 * 180 * 4]; image.CopyPixels(pixels, 320 * 4, 0); return pixels;
                }
                bool HasGreen(byte[] p) => Enumerable.Range(0,p.Length/4).Count(i => p[i*4+1] > 200 && p[i*4] < 30) > 1000;
                Until(() => HasGreen(Capture(mirror)), "Custom CSS or mirrored Chromium pixels missing");
                var first = Capture(stage);
                Check(first[(150*320+300)*4] > 200, "Transparent HTML did not preserve blue background");
                Until(() => !Capture(stage).SequenceEqual(first), "Local HTML animation did not update rendered WPF frames");
                browser.Reload(); Until(() => browser.IsReady, "Browser reload did not recover");
                browser.SetMuted(true);
            }
            finally { host.Close(); Directory.Delete(directory, true); }
        });
    }
}
