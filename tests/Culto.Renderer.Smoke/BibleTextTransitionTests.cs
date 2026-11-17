using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static async Task RunBibleTextTransitionChecks()
    {
        foreach (var mixed in new[] { false, true })
        {
            using var surface = new ProjectionSurface(smoothTransitions: mixed);
            var host = new Window { Content = surface, Width = 960, Height = 600, Topmost = true };
            host.Show(); host.Activate();
            var verse = new Cue { Kind = CueKind.Bible, Reference = "Prueba 1:1", Version = "Prueba",
                Body = string.Join(" ", Enumerable.Repeat("Texto sintético para comprobar la entrada de cada línea del pasaje.", 5)) };
            var style = new BibleVisualStyle { BackgroundColor = "#204080", BackgroundDim = 0, TextEffect = "Shadow", Transition = "Cut" };
            TextBlock Body() => Descendants(surface.ActiveSurface).OfType<TextBlock>().Single(block => block.Text == verse.Body);
            byte[] Corner()
            {
                surface.UpdateLayout();
                var image = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(surface);
                var pixel = new byte[4]; image.CopyPixels(new Int32Rect(8, 8, 1, 1), pixel, 4, 0); return pixel;
            }
            try
            {
                surface.BibleStyle = style; surface.ShowCue(verse); surface.UpdateLayout();
                foreach (var kind in new[] { "TextRise", "TextLines", "TextFocus" })
                {
                    surface.BibleStyle = style with { Transition = kind, TransitionMilliseconds = 600 };
                    surface.TransitionTo(verse);
                    await Task.Delay(90);
                    Check(surface.IsTransitioning, $"{kind} runs on {(mixed ? "mixed" : "direct")} output");
                    var body = Body();
                    var words = (StackPanel)body.Parent;
                    Check(kind switch
                    {
                        "TextRise" => body.RenderTransform is TranslateTransform { Y: > 0 } && body.Opacity < 1,
                        "TextFocus" => body.Effect is BlurEffect { Radius: > 0 },
                        _ => body.OpacityMask is DrawingBrush { Drawing: DrawingGroup lines } && lines.Children.Count > 1
                            && lines.Children.OfType<GeometryDrawing>().First().Brush.Opacity > lines.Children.OfType<GeometryDrawing>().Last().Brush.Opacity
                    }, $"{kind} animates actual text rather than the whole slide");
                    var pixel = Corner();
                    Check(pixel[0] == 128 && pixel[1] == 64 && pixel[2] == 32 && surface.ActiveSurface.Opacity == 1,
                        $"{kind} keeps the backdrop stable");
                    var frame = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    frame.Render(surface);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
                    var snapshots = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "bible-text-transitions");
                    System.IO.Directory.CreateDirectory(snapshots);
                    using (var file = System.IO.File.Create(System.IO.Path.Combine(snapshots, kind + (mixed ? "-mixed" : "-direct") + ".png"))) encoder.Save(file);
                    await Task.Delay(700);
                    Check(!surface.IsTransitioning && body.Opacity == 1 && body.OpacityMask is null
                        && body.RenderTransform.Value.IsIdentity && words.Effect is null && body.Effect is DropShadowEffect,
                        $"{kind} completes cleanly and preserves the text style");

                    surface.TransitionTo(verse);
                    await Task.Delay(50);
                    surface.ShowCue(verse, blackout: true);
                    await Task.Delay(650);
                    Check(!surface.IsTransitioning && Corner().Take(3).All(channel => channel == 0),
                        $"Blackout cancels {kind} without revealing stale text");
                    surface.ShowCue(verse);
                    Check(!surface.IsTransitioning && Body().Opacity == 1, "Static previews do not animate automatically");
                }
                surface.TransitionTo(verse);
                await Task.Delay(40);
                var newest = verse.Copy(); newest.Body = "Última selección sintética";
                surface.TransitionTo(newest);
                await Task.Delay(800);
                Check(!surface.IsTransitioning && surface.DisplayedCue?.Body == newest.Body,
                    "Rapid text transitions retain the latest passage");
            }
            finally { host.Close(); }
        }
        await RunIndependentBibleTransitionChecks();
        await RunBiblePresetCatalogChecks();
    }

    private static async Task RunIndependentBibleTransitionChecks()
    {
        foreach (var mixed in new[] { false, true })
        {
            using var surface = new ProjectionSurface(smoothTransitions: mixed);
            var host = new Window { Content = surface, Width = 960, Height = 600 };
            host.Show();
            var verse = new Cue { Kind = CueKind.Bible, Body = "Texto independiente", Reference = "Salmos 23:1", Version = "Prueba" };
            TextBlock Block(string text) => Descendants(surface.ActiveSurface).OfType<TextBlock>().Single(block => block.Text == text);
            var preset = BibleTextTransitionCatalog.All.First(item => item.Id.StartsWith("TextPreset:") && item.Motion == BibleTextMotion.Rotate);
            try
            {
                foreach (var (text, reference) in new[] { ("Cut", preset.Id), (preset.Id, "Cut"), ("TextFocus", "TextRise"), ("System", preset.Id) })
                {
                    surface.BibleStyle = new BibleVisualStyle { Transition = text, ReferenceTransition = reference, TransitionMilliseconds = 600, ShowVersion = false };
                    surface.TransitionTo(verse); surface.UpdateLayout();
                    var body = Block(verse.Body); var citation = Block(verse.Reference);
                    Check(surface.IsTransitioning, $"Independent Bible effects start ({mixed}/{text}/{reference})");
                    if (text == "System") Check(body.HasAnimatedProperties, "Independent reference retains the system dissolve on the passage");
                    if (text == "Cut") Check(body.Opacity == 1 && !body.HasAnimatedProperties && body.RenderTransform.Value.IsIdentity,
                        "Reference animation leaves a cut passage static");
                    if (reference == "Cut") Check(citation.Opacity == 1 && !citation.HasAnimatedProperties && citation.RenderTransform.Value.IsIdentity,
                        "Passage animation leaves a cut reference static");
                    if (text == "TextFocus") Check(body.Effect is BlurEffect && citation.Effect is not BlurEffect && citation.RenderTransform is TranslateTransform,
                        "Focus and rise effects target their own blocks");
                    await Task.Delay(800);
                    Check(!surface.IsTransitioning && body.Opacity == 1 && citation.Opacity == 1
                        && body.RenderTransform.Value.IsIdentity && citation.RenderTransform.Value.IsIdentity && body.Effect is null
                        && Block(verse.Version).Visibility == Visibility.Collapsed,
                        "Independent Bible animations restore text and preserve the hidden version");
                }
            }
            finally { host.Close(); }
        }
    }

    private static async Task RunBiblePresetCatalogChecks()
    {
        var presets = BibleTextTransitionCatalog.All.Where(preset => preset.Id.StartsWith("TextPreset:")).ToArray();
        foreach (var mixed in new[] { false, true })
        {
            using var surface = new ProjectionSurface(smoothTransitions: mixed);
            var host = new Window { Content = surface, Width = 960, Height = 600, Topmost = true };
            host.Show(); host.Activate();
            var verse = new Cue { Kind = CueKind.Bible, Reference = "Prueba 1:2", Version = "Prueba",
                Body = "Texto sintético con tildes y claridad.\nLas palabras conservan su posición, su fuente y su alineación durante la animación. Esta frase permite comprobar varias líneas del pasaje." };
            var style = new BibleVisualStyle { FontFamily = "Georgia", ReferenceFontFamily = "Arial", ReferenceColor = "#FFE08A", TextEffect = "Shadow",
                BackgroundColor = "#204080", BackgroundDim = 0, TransitionMilliseconds = 250 };
            Canvas Overlay() => (Canvas)typeof(ProjectionSurface).GetField("_bibleTextOverlay",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(surface.ActiveSurface)!;
            TextBlock Body() => Descendants(surface.ActiveSurface).OfType<TextBlock>().Single(block => block.Text == verse.Body);
            void Select(BibleTextTransitionPreset preset)
            {
                surface.BibleStyle = style with { Transition = preset.Id };
                surface.TransitionTo(verse); surface.UpdateLayout();
            }
            try
            {
                surface.BibleStyle = style with { Transition = "Cut" }; surface.ShowCue(verse); surface.UpdateLayout();
                if (!mixed)
                {
                    foreach (var scope in Enum.GetValues<BibleTextScope>())
                    foreach (var effect in new[] { "Shadow", "Glow" })
                    foreach (var alignment in new[] { "Left", "Center", "Right" })
                    {
                        var preset = presets.Single(item => item.Motion == BibleTextMotion.Slide && item.Scope == scope && item.Variant == 0);
                        surface.BibleStyle = style with { Transition = preset.Id, Italic = true, TextEffect = effect, Alignment = alignment };
                        surface.ShowCue(verse); surface.UpdateLayout();
                        var text = (Viewbox)typeof(ProjectionSurface).GetField("_text",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(surface.ActiveSurface)!;
                        var canvas = (FrameworkElement)text.Child;
                        var expected = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32); expected.Render(canvas);
                        var position = Body().TranslatePoint(new Point(), canvas);
                        var bodyHeight = Body().ActualHeight;
                        surface.TransitionTo(verse); surface.UpdateLayout();
                        var reconstructed = new DrawingVisual();
                        using (var drawing = reconstructed.RenderOpen())
                            foreach (var image in Overlay().Children.OfType<Image>())
                                drawing.DrawImage(image.Source, new Rect(Canvas.GetLeft(image), Canvas.GetTop(image), image.Width, image.Height));
                        var actual = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32); actual.Render(reconstructed);
                        var expectedBytes = new byte[1600 * 900 * 4]; var actualBytes = new byte[expectedBytes.Length];
                        expected.CopyPixels(expectedBytes, 1600 * 4, 0); actual.CopyPixels(actualBytes, 1600 * 4, 0);
                        var largestError = 0;
                        for (var y = 0; y < 900; y++)
                        for (var x = 0; x < 1600 * 4; x++)
                            largestError = Math.Max(largestError, Math.Abs(expectedBytes[y * 1600 * 4 + x] - actualBytes[y * 1600 * 4 + x]));
                        Check(largestError <= 2, $"Fragment reconstruction preserves italic {effect} ink for {scope}/{alignment} (error {largestError})");
                        surface.ShowCue(verse);
                    }
                    foreach (var preset in presets)
                    {
                        Select(preset);
                        var reference = Descendants(surface.ActiveSurface).OfType<TextBlock>().Single(block => block.Text == verse.Reference);
                        Check(reference.Opacity == 0 && !reference.HasAnimatedProperties,
                            "Reference uses preset fragments instead of a separate fade: " + preset.Id);
                        Check(surface.IsTransitioning && Overlay().Children.Count is > 0 and <= 240,
                            "Preset renders bounded animated fragments: " + preset.Id);
                        var images = Overlay().Children.OfType<Image>().ToArray();
                        Check(images.All(image => image.HasAnimatedProperties && image.Source is CroppedBitmap),
                            "Preset uses real WPF animations: " + preset.Id);
                        surface.ShowCue(verse);
                        Check(!surface.IsTransitioning && Overlay().Children.Count == 0 && Body().Opacity == 1,
                            "Preset cancellation releases fragments: " + preset.Id);
                    }
                }
                foreach (var motion in Enum.GetValues<BibleTextMotion>())
                foreach (var scope in Enum.GetValues<BibleTextScope>())
                {
                    var preset = presets.Single(item => item.Motion == motion && item.Scope == scope && item.Variant == 0);
                    Select(preset);
                    var count = Overlay().Children.OfType<Image>().Count(image => ReferenceEquals(image.Tag, Body()));
                    Check(scope == BibleTextScope.Passage ? count == 1 : count > 1, "Preset respects its fragment scope: " + preset.Id);
                    await Task.Delay(60);
                    Check(surface.IsTransitioning && surface.ActiveSurface.Opacity == 1, "Preset keeps the backdrop stable: " + preset.Id);
                    if (!mixed && scope == BibleTextScope.Words)
                    {
                        var frame = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        frame.Render(surface);
                        var folder = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "bible-preset-coverage");
                        System.IO.Directory.CreateDirectory(folder);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
                        using var file = System.IO.File.Create(System.IO.Path.Combine(folder, motion + ".png")); encoder.Save(file);
                    }
                    var deadline = DateTime.UtcNow.AddSeconds(2);
                    while (surface.IsTransitioning && DateTime.UtcNow < deadline) await Task.Delay(20);
                    Check(!surface.IsTransitioning && Overlay().Children.Count == 0 && Body().Opacity == 1
                        && Body().FontFamily.Source == "Georgia" && Body().Effect is DropShadowEffect,
                        "Preset restores native body text: " + preset.Id);
                    Check(Descendants(surface.ActiveSurface).OfType<TextBlock>().Single(block => block.Text == verse.Reference)
                            is { Opacity: 1, FontFamily.Source: "Arial", Foreground: SolidColorBrush { Color: var referenceColor } }
                        && referenceColor == (Color)ColorConverter.ConvertFromString("#FFE08A"),
                        "Preset restores native styled text: " + preset.Id);
                }
                var wordPreset = presets.Single(item => item.Motion == BibleTextMotion.Blur && item.Scope == BibleTextScope.Words && item.Variant == 7);
                surface.BibleStyle = style with { Transition = wordPreset.Id, ShowVersion = false };
                surface.TransitionTo(verse); surface.UpdateLayout();
                var hiddenVersion = Descendants(surface.ActiveSurface).OfType<TextBlock>().Single(block => block.Text == verse.Version);
                Check(hiddenVersion.Visibility == Visibility.Collapsed
                    && Overlay().Children.OfType<Image>().All(image => !ReferenceEquals(image.Tag, hiddenVersion)),
                    "Preset never captures a hidden Bible version");
                surface.ShowCue(verse);
                verse.Body = string.Join(" ", Enumerable.Repeat("Lectura sintética extensa", 160));
                Select(wordPreset);
                Check(Overlay().Children.Count is > 0 and <= 240 && surface.DisplayedCue?.Body == verse.Body,
                    "Long passages bound animation work without losing words");
                surface.ShowCue(verse, blackout: true);
                await Task.Delay(300);
                Check(!surface.IsTransitioning && Overlay().Children.Count == 0, "Blackout cancels every preset fragment");
                surface.ShowCue(verse);
                Select(wordPreset);
                var latest = new Cue { Title = "Último", Body = "Selección posterior" };
                surface.ShowCue(latest);
                await Task.Delay(300);
                Check(!surface.IsTransitioning && Overlay().Children.Count == 0 && surface.DisplayedCue?.Id == latest.Id,
                    "Replacing a preset keeps the newest cue and releases animations");
            }
            finally { host.Close(); }
        }
    }
}
