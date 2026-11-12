using System;
using System.IO;
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
    static async Task RunBibleStyleChecks(ProjectionSurface output, ProjectionSurface preview)
    {
        var directory = Path.Combine(Path.GetTempPath(), "CultoBibleStyle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var imagePath = Path.Combine(directory, "red.png");
        var corruptPath = Path.Combine(directory, "corrupt.png");
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr32, null, new byte[] { 0, 0, 255, 255 }, 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(imagePath)) encoder.Save(stream);
        File.WriteAllText(corruptPath, "not an image");
        var verse = new Cue { Kind = CueKind.Bible, Body = "Texto de prueba", Reference = "Prueba 1:1", Version = "Versión de prueba" };
        var style = new BibleVisualStyle
        {
            FontFamily = "Georgia", FontSize = 96, Bold = true, Italic = true,
            Alignment = "Left", TextColor = "#FFFF00", TextEffect = "Shadow",
            BackgroundImagePath = imagePath, BackgroundDim = .5, Transition = "Cut"
        };
        TextBlock Body() => Descendants(output).OfType<TextBlock>().Single(block => block.Text == verse.Body);
        byte[] Pixel()
        {
            output.UpdateLayout();
            var render = new RenderTargetBitmap((int)Math.Ceiling(output.ActualWidth), (int)Math.Ceiling(output.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            render.Render(output);
            var pixels = new byte[4]; render.CopyPixels(new Int32Rect(8, 8, 1, 1), pixels, 4, 0);
            return pixels;
        }
        try
        {
            output.SetPaused(true); preview.SetPaused(true);
            output.ShowCue(null); output.BibleStyle = style; preview.BibleStyle = style;
            style.FontSize = 30;
            output.TransitionTo(verse); preview.ShowCue(verse);
            Check(!output.IsTransitioning, "Bible Cut applies without an outgoing dissolve");
            Check(Body().FontFamily.Source == "Georgia" && Math.Abs(Body().FontSize - 96) < .1
                && Body().FontWeight == FontWeights.Bold && Body().FontStyle == FontStyles.Italic
                && Body().TextAlignment == TextAlignment.Left && ((SolidColorBrush)Body().Foreground).Color == Colors.Yellow,
                "Bible typography applies from a copied style rather than caller mutations");
            Check(Body().Effect is DropShadowEffect { ShadowDepth: > 0 }, "Bible shadow is visible on text");
            var red = Pixel();
            Check(red[2] is >= 120 and <= 135 && red[0] < 5 && red[1] < 5, "Bible image is composited with the selected darkness");
            Check(Descendants(preview).OfType<TextBlock>().Single(block => block.Text == verse.Body).FontSize == Body().FontSize,
                "Preview and output render identical Bible typography");
            foreach (var effect in new[] { "Glow", "None" })
            {
                output.BibleStyle = output.BibleStyle with { TextEffect = effect };
                output.ShowCue(verse);
                Check(effect == "None" ? Body().Effect is null : Body().Effect is DropShadowEffect { ShadowDepth: 0 },
                    "Bible effect resets correctly to " + effect);
            }
            foreach (var path in new[] { corruptPath, Path.Combine(directory, "missing.png") })
            {
                output.BibleStyle = output.BibleStyle with { BackgroundImagePath = path, BackgroundColor = "#204080", BackgroundDim = 0 };
                output.ShowCue(verse);
                var fallback = Pixel();
                Check(fallback[0] == 128 && fallback[1] == 64 && fallback[2] == 32 && Body().IsVisible,
                    "Unavailable Bible background keeps solid color and readable verse: " + Path.GetFileName(path));
            }
            verse.Body = string.Join(" ", Enumerable.Repeat("Un pasaje extenso mantiene el texto y la cita dentro del lienzo.", 40));
            output.ShowCue(verse); output.UpdateLayout();
            Check(Body().FontSize < 96, "Long Bible passages shrink from the selected maximum font size");
            foreach (var block in Descendants(output).OfType<TextBlock>().Where(block => !string.IsNullOrEmpty(block.Text)))
            {
                var bounds = block.TransformToAncestor(output).TransformBounds(new Rect(0, 0, block.ActualWidth, block.ActualHeight));
                Check(block.IsVisible && bounds.Top >= 0 && bounds.Bottom <= output.ActualHeight,
                    "Styled Bible body and attribution remain inside the output");
            }
            output.ShowCue(new Cue { Kind = CueKind.Text, Body = "Texto normal" });
            var plain = Descendants(output).OfType<TextBlock>().Single(block => block.Text == "Texto normal");
            Check(plain.FontFamily.Source == "Segoe UI" && plain.FontWeight == FontWeights.Normal
                && plain.FontStyle == FontStyles.Normal && plain.TextAlignment == TextAlignment.Center
                && plain.Effect is null && ((SolidColorBrush)plain.Foreground).Color == Colors.White,
                "Bible formatting cannot leak into ordinary text");
            var neutral = Pixel();
            Check(neutral[0] == 34 && neutral[1] == 23 && neutral[2] == 16, "Bible background cannot leak into ordinary text");
            output.ShowCue(new Cue { Kind = CueKind.Image, FilePath = imagePath });
            Check(Descendants(output).OfType<Image>().Any(image => image.Source is not null && image.IsVisible && image.Stretch == Stretch.Uniform),
                "Media images retain their normal aspect ratio after Bible styling");

            output.BibleStyle = new BibleVisualStyle { Transition = "Cut", BackgroundColor = "#FF0000", BackgroundDim = 0 };
            output.ShowCue(verse);
            output.BibleStyle = new BibleVisualStyle { Transition = "Dissolve", TransitionMilliseconds = 1400, BackgroundColor = "#0000FF", BackgroundDim = 0 };
            output.TransitionTo(verse);
            await Task.Delay(450);
            var mixed = Pixel();
            Check(output.IsTransitioning && mixed[0] > 20 && mixed[2] > 20, "Bible dissolve uses the configured duration and blends actual pixels");
            output.BibleStyle = output.BibleStyle with { Transition = "Cut", BackgroundColor = "#00FF00" };
            output.TransitionTo(verse);
            Check(!output.IsTransitioning && Pixel()[1] > 240, "A new Bible cut immediately cancels the previous dissolve");
            await Task.Delay(1100);
            Check(!output.IsTransitioning && Pixel()[1] > 240, "Stale Bible transition completion cannot replace the newest passage");
            output.BibleStyle = output.BibleStyle with { Transition = "Dissolve", TransitionMilliseconds = 250 };
            output.TransitionTo(verse); output.ShowCue(verse, blackout: true);
            await Task.Delay(350);
            Check(!output.IsTransitioning && Pixel().Take(3).All(value => value == 0), "Blackout cancels styled Bible transitions without revealing old content");
        }
        finally
        {
            output.BibleStyle = new BibleVisualStyle(); preview.BibleStyle = new BibleVisualStyle();
            output.ShowCue(null); preview.ShowCue(null);
            File.Delete(imagePath); File.Delete(corruptPath); Directory.Delete(directory);
        }
    }
}
