using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static async Task RunLayerAnimationChecks()
    {
        using var pool = new LayerSourcePool();
        using var canvas = new LayerSceneCanvas(pool);
        var source = new Cue { Kind = CueKind.Text, Body = "Animated layer" };
        var scene = new SceneComposition { Width = 640, Height = 360, Layers = [new SceneLayer
        {
            SourceId = "animation-shared", Source = source, Width = 640, Height = 360, Opacity = .8,
            Animation = new SceneAnimationOptions { Entrance = "Rise", Exit = "SlideLeft", DelayMilliseconds = 80, DurationMilliseconds = 250 }
        }] };
        canvas.SetScene(scene);
        var visual = ((Canvas)canvas.Child).Children.OfType<Rectangle>().Single();
        Check(visual.Opacity == 0, "Animated layer prepares its hidden first frame before going live");
        var entry = pool.Entries.Single().Surface;
        var root = new Grid(); root.Children.Add(pool.Depot); root.Children.Add(canvas);
        var host = new Window { Content = root, Width = 640, Height = 400, Topmost = true };
        host.Show(); host.Activate();
        async Task Until(Func<bool> predicate, string label)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(10);
            Check(predicate(), label);
        }
        try
        {
            await Until(() => visual.Opacity is > .05 and < .75, "Entrance passes through intermediate opacity");
            var transforms = ((TransformGroup)visual.RenderTransform).Children;
            Check(transforms.OfType<TranslateTransform>().Single().Y > 0, "Rise enters from below using a render transform");
            await Until(() => Math.Abs(visual.Opacity - .8) < .001, "Entrance settles at the saved layer opacity");
            Check(ReferenceEquals(entry, pool.Entries.Single().Surface), "Layer animation does not recreate its source");
            canvas.StartAnimations();
            Check(visual.Opacity == 0, "Replay returns only the visual to its entrance frame");
            await Until(() => Math.Abs(visual.Opacity - .8) < .001, "Replayed entrance finishes");
            canvas.BeginExit();
            await Until(() => visual.Opacity is > .05 and < .7, "Exit passes through intermediate opacity");
            Check(transforms.OfType<TranslateTransform>().Single().X < 0, "Exit slides toward the configured edge");
            await Until(() => visual.Opacity < .001, "Exit completes hidden");
            Check(ReferenceEquals(entry, pool.Entries.Single().Surface), "Exit does not retire the shared source prematurely");
            var auto = scene.Copy(); auto.Layers[0].Animation = new SceneAnimationOptions { Entrance = "Fade", Exit = "Fade", DurationMilliseconds = 100, HoldMilliseconds = 100 };
            canvas.SetScene(auto); visual = ((Canvas)canvas.Child).Children.OfType<Rectangle>().Single();
            await Until(() => visual.Opacity > .7, "Scheduled animation reaches its hold");
            await Until(() => visual.Opacity < .001, "Scheduled exit hides the layer after its hold");
            canvas.BeginExit(); await Task.Delay(40);
            Check(visual.Opacity < .001, "Scene departure never resurrects an automatically hidden layer");
            canvas.Dispose();
            Check(!pool.Entries.Any(), "Disposing an animated canvas releases sources and animation callbacks");
        }
        finally { host.Close(); }
        await RunTemplateAppearanceChecks();
        await RunAnimatedMixerChecks();
    }

    private static async Task RunTemplateAppearanceChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 0 };
        var host = new Window { Content = output, Width = 976, Height = 580, Topmost = true }; host.Show(); host.Activate();
        try
        {
            output.ShowCue(new Cue { Kind = CueKind.Scene, Scene = SceneTemplates.Create("reading") });
            await Task.Delay(1600); host.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)output.ActualWidth, (int)output.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(output);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var file = System.IO.File.Create(System.IO.Path.GetFullPath(".tools/animated-reading-renderer.png"))) encoder.Save(file);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            var white = 0;
            for (var index = 0; index < pixels.Length; index += 4)
                if (pixels[index] > 180 && pixels[index + 1] > 180 && pixels[index + 2] > 180) white++;
            Check(white > 1000, "Animated reading template actually renders visible text in the compositor");
            foreach (var source in output.ActiveSurface.GetLiveLayers())
            {
                var clip = VisualTreeHelper.GetClip(source.Surface);
                Check(clip is null || clip.Bounds.Width >= source.Width - 1 && clip.Bounds.Height >= source.Height - 1,
                    $"Source viewport stays complete in a smaller output: {clip?.Bounds}, expected {source.Width}x{source.Height}");
            }
        }
        finally { host.Close(); }
    }

    private static async Task RunAnimatedMixerChecks()
    {
        using var output = new ProjectionSurface(smoothTransitions: true) { Volume = 0,
            Transitions = new TransitionOptions { Kind = "Dissolve", DurationMilliseconds = 100, AudioFadeInMilliseconds = 0, AudioFadeOutMilliseconds = 0 } };
        var errors = new System.Collections.Generic.List<string>(); output.PlaybackError += errors.Add;
        var host = new Window { Content = output, Width = 656, Height = 400, Topmost = true }; host.Show(); host.Activate();
        var video = new Cue { Kind = CueKind.Video, FilePath = System.IO.Path.Combine(Folder, "test.mkv") };
        var first = new Cue { Kind = CueKind.Scene, Scene = new SceneComposition { Width = 640, Height = 360, Layers = [
            new SceneLayer { SourceId = "animated-video", Source = video, Width = 640, Height = 360, Muted = true, Loop = true },
            new SceneLayer { SourceId = "animated-title", Source = new Cue { Kind = CueKind.Text, Body = "Welcome" }, Width = 640, Height = 360,
                Animation = new SceneAnimationOptions { Entrance = "Fade", Exit = "Fade", DurationMilliseconds = 650 } }
        ] } };
        async Task Until(Func<bool> predicate, string label)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!predicate() && errors.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
            Check(predicate() && errors.Count == 0, label + " " + string.Join(" | ", errors)
                + $" [cue={output.DisplayedCue?.Kind}, loading={output.IsPreparingPlaybackEngine}, transitioning={output.IsTransitioning}, layers={output.ActiveSurface.GetLiveLayers().Count}, position={output.Position}, paused={output.IsPaused}]");
        }
        Rectangle Title(LayerSceneCanvas canvas) => ((Canvas)canvas.Child).Children.OfType<Rectangle>().Last();
        try
        {
            output.ShowCue(first);
            await Until(() => output.ActiveSurface.SceneCanvas is not null && output.ActiveSurface.GetLiveLayers().First().Surface.Position > .2,
                "Animated scene starts its actual video background");
            var canvas = output.ActiveSurface.SceneCanvas!;
            await Until(() => Title(canvas).Opacity > .99, "Mixer starts entrance only after the scene becomes ready");
            var videoSurface = output.ActiveSurface.GetLiveLayers().First().Surface;
            Check(videoSurface.Loop && videoSurface.Volume == 0, "Background loop and mute apply independently of global playback");
            var position = videoSurface.Position;
            output.ReplaySceneAnimations();
            Check(Title(canvas).Opacity == 0, "Repeating animation resets the active scene visual");
            await Until(() => Title(canvas).Opacity > .99, "Repeated active scene entrance completes");
            Check(ReferenceEquals(videoSurface, output.ActiveSurface.GetLiveLayers().First().Surface) && videoSurface.Position > position,
                "Repeating animation keeps the running video and playback position");
            var second = first.Copy(newId: true);
            second.Scene!.Layers[1].SourceId = "animated-title-second";
            second.Scene.Layers[1].Source.Body = "Next scene";
            output.TransitionTo(second);
            await Until(() => output.DisplayedCue?.Id == second.Id, "Mixer activates the next animated scene");
            await Until(() => Title(canvas).Opacity is > .01 and < .9, "Outgoing layer exit runs during the scene transition");
            Check(output.IsTransitioning && output.PresentedTransitions.DurationMilliseconds >= 650,
                "Transition retains the outgoing scene long enough to finish its exit");
            await Until(() => !output.IsTransitioning && Title(output.ActiveSurface.SceneCanvas!).Opacity > .99,
                "Scene transition and incoming entrance finish together");
            Check(ReferenceEquals(videoSurface, output.ActiveSurface.GetLiveLayers().First().Surface),
                "Changing animated scenes retains their shared video decoder");
            await Task.Delay(3400);
            Check(videoSurface.IsPlaying && !videoSurface.HasEnded, "Background video continues beyond its first loop");

            var foreground = new Cue { Kind = CueKind.Scene, Scene = new SceneComposition { Width = 640, Height = 360,
                Layers = [second.Scene.Layers[1].Copy()] } };
            output.TransitionVisual(foreground);
            await Until(() => !output.IsTransitioning && output.DisplayedCue?.Id == foreground.Id,
                "Text-only animated scene preserves previous background media");
            Check(output.HasBackgroundMedia, "Retained background remains available during foreground animations");
            var foregroundCanvas = Descendants(output).OfType<LayerSceneCanvas>().Single();
            output.ReplaySceneAnimations();
            Check(Title(foregroundCanvas).Opacity == 0, "Replay targets the visible scene when media is retained behind it");
            await Until(() => Title(foregroundCanvas).Opacity > .99, "Foreground replay completes with retained media");
            Check(ReferenceEquals(videoSurface, output.ActiveSurface.GetLiveLayers().First().Surface) && videoSurface.IsPlaying,
                "Foreground replay leaves retained media running");

            output.Transitions = output.Transitions with { Kind = "Move", DurationMilliseconds = 900 };
            var destination = first.Copy(newId: true);
            output.TransitionTo(destination);
            await Until(() => output.DisplayedCue?.Id == destination.Id, "Animated scene enters native Move");
            var samples = new System.Collections.Generic.HashSet<double>();
            var frames = new System.Collections.Generic.HashSet<ImageSource>();
            var untilMove = DateTime.UtcNow.AddSeconds(35);
            while (output.IsTransitioning && errors.Count == 0 && DateTime.UtcNow < untilMove)
            {
                samples.Add(Math.Round(Title(output.ActiveSurface.SceneCanvas!).Opacity, 2));
                foreach (var image in Descendants(output).OfType<Image>().Where(image => image.Visibility == Visibility.Visible && Panel.GetZIndex(image) == 1000))
                    if (image.Source is not null) frames.Add(image.Source);
                await Task.Delay(20);
            }
            Check(errors.Count == 0 && !output.IsTransitioning, "Native Move finishes animated scene without fallback: " + string.Join(" | ", errors));
            Check(frames.Count > 3 && samples.Count(value => value is > 0 and < 1) > 2,
                "Native Move receives live composite frames while layer entrance progresses");
            Check(Title(output.ActiveSurface.SceneCanvas!).Opacity > .99 && output.PresentedTransitions.Kind == "Move",
                "Move reveals the completed entrance instead of a stale scene snapshot");
        }
        finally { host.Close(); }
    }
}
