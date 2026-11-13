using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunThemeTests(MainWindow window)
    {
        Test("Preview and actual output have separate side by side monitors", () =>
        {
            var preview = (FrameworkElement)window.FindName("PreviewViewport");
            var output = window.FindName("OutputViewport") as FrameworkElement;
            Check(output is not null, "Actual output monitor is missing");
            window.UpdateLayout();
            var left = BoundsWithin(preview, window); var right = BoundsWithin(output!, window);
            Check(left.Right <= right.Left && Math.Abs(left.Top - right.Top) < 80, "Monitors are not side by side");
            Check(output!.ActualHeight >= 80, "Output monitor is too small");
        });
        Test("Image thumbnail loading releases files and cannot overwrite a recycled text cue", () =>
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CultoThumb-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "image.png");
            var invalid = System.IO.Path.Combine(directory, "invalid.png");
            try
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
                    1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 10, 20, 30, 255 }, 4)));
                using (var stream = System.IO.File.Create(path)) encoder.Save(stream);
                var thumbnail = new CueThumbnail { Cue = new Cue { Kind = CueKind.Image, FilePath = path } };
                Until(() => Field<ImageSource?>(thumbnail, "_image") is not null, "Image thumbnail did not load");
                using (System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None)) { }
                thumbnail.Cue = new Cue { Kind = CueKind.Image, FilePath = path };
                thumbnail.Cue = new Cue { Body = "A text cue now owns this recycled tile" };
                PumpFor(150);
                Check(Field<ImageSource?>(thumbnail, "_image") is null, "Stale image replaced the text thumbnail");
                System.IO.File.WriteAllText(invalid, "Invalid image data");
                Wait((Task)Call(thumbnail, "LoadImage", invalid, Field<int>(thumbnail, "_generation"))!);
                Check(Field<ImageSource?>(thumbnail, "_image") is null, "Invalid image did not keep its fallback");
            }
            finally
            {
                System.IO.File.Delete(path); System.IO.File.Delete(invalid); System.IO.Directory.Delete(directory);
            }
        });
        Test("Central preview keeps a 16 by 9 viewport at desktop and compact sizes", () =>
        {
            var width = window.Width; var height = window.Height;
            var manager = (DockingManager)window.FindName("WorkspaceDock");
            var mediaTab = manager.Layout.Descendents().OfType<LayoutAnchorable>().Single(p => p.ContentId == "media");
            var previousTab = ((LayoutAnchorablePane)mediaTab.Parent).Children.FirstOrDefault(p => p.IsSelected)?.ContentId;
            try
            {
                Call(window, "ActivateWorkspacePanel", "media");
                foreach (var size in new[] { new Size(1480, 900), new Size(1280, 680) })
                {
                    window.Width = size.Width; window.Height = size.Height; window.UpdateLayout(); PumpFor(80);
                    Call(window, "ActivateWorkspacePanel", "media"); window.UpdateLayout();
                    var viewport = window.FindName("PreviewViewport") as FrameworkElement;
                    Check(viewport is not null, "Widescreen preview frame is missing");
                    Check(viewport!.ActualHeight >= 80 && Math.Abs(viewport.ActualWidth / viewport.ActualHeight - 16d / 9) < .04,
                        "Preview frame is distorted or unusably small");
                    var media = (FrameworkElement)window.FindName("MediaPanel");
                    var mediaList = (FrameworkElement)window.FindName("MediaList");
                    var stageBounds = BoundsWithin(viewport, window);
                    var mediaBounds = BoundsWithin(media, window);
                    Check(media.IsVisible && mediaList.IsVisible && mediaList.ActualHeight >= 48
                        && (mediaBounds.Top >= stageBounds.Bottom || mediaBounds.Right <= stageBounds.Left || mediaBounds.Left >= stageBounds.Right),
                        "Media browser is hidden, has no usable file area, or overlaps the preview");
                    CheckWithin(mediaList, media, new Rect(media.RenderSize), "Media file area");
                    foreach (var control in Descendants(media).OfType<FrameworkElement>().Where(e => e is Button or TextBox && e.IsVisible))
                        CheckWithin(control, media, new Rect(media.RenderSize), "Media browser control " + control.Name);
                }
            }
            finally
            {
                window.Width = width; window.Height = height;
                if (previousTab is not null) Call(window, "ActivateWorkspacePanel", previousTab);
                window.UpdateLayout();
            }
        });
        Test("Console font is bundled and resolves all four weights offline", () =>
        {
            foreach (var weight in new[] { FontWeights.Normal, FontWeights.Medium, FontWeights.SemiBold, FontWeights.Bold })
            {
                var face = new Typeface(window.FontFamily, FontStyles.Normal, weight, FontStretches.Normal);
                Check(face.TryGetGlyphTypeface(out var glyphs), "Font did not resolve");
                Check(glyphs!.FontUri.Scheme == "pack" && glyphs.FamilyNames.Values.Any(name => name.Contains("Lexend")),
                    "Console fell back to a system font instead of bundled Lexend");
                Check(glyphs.Weight == weight, "Font weight was synthesized");
            }
        });

        Test("Rapid tab changes finish visible without changing live content", () =>
        {

            var session = Field<PresentationSession>(window, "_session");
            var live = session.Live;
            var original = Field<int>(window, "_activeContentSource");
            for (var i = 0; i < 15; i++) Call(window, "SelectContentSource", i % 4);
            Call(window, "SelectContentSource", original);
            PumpFor(300);
            Check(ReferenceEquals(live, session.Live), "Navigation changed projected content");
            foreach (var name in new[] { "SourcesPanel", "BiblePanel", "TextPanel", "YouTubePanel" })
            {
                var content = (FrameworkElement)window.FindName(name);
                Check(content.Opacity == 1, "A tab was left faded after interrupted transitions");
                Check(content.RenderTransform is not TranslateTransform move || move.Y == 0, "A tab was left displaced");
            }
            Check(((FrameworkElement)window.FindName(new[] { "SourcesPanel", "BiblePanel", "TextPanel", "YouTubePanel" }[original])).IsVisible, "Selected source is hidden");
        });

        Test("Selected tab colors do not make Bible content unreadable", () =>
        {

            var original = Field<int>(window, "_activeContentSource");
            try
            {
                Call(window, "SelectContentSource", 1);
                var title = (TextBlock)window.FindName("BibleNameText");
                Check(((SolidColorBrush)title.Foreground).Color == ((SolidColorBrush)window.FindResource("TextBrush")).Color,
                    "Selected header foreground leaked into Bible content");
            }
            finally { Call(window, "SelectContentSource", original); window.UpdateLayout(); }
        });

        Test("Themed media slider retains native commands and value binding", () =>
        {
            var slider = (Slider)window.FindName("VolumeSlider");
            var original = slider.Value;
            slider.ApplyTemplate();
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            Check(track?.Thumb is not null, "Native slider track/thumb is missing");
            slider.Value = 30;
            Slider.IncreaseSmall.Execute(null, slider);
            Check(slider.Value == 30 + slider.SmallChange, "Keyboard command did not change slider value");
            Check(track!.Value == slider.Value, "Track did not follow slider value");
            track.Value = 45;
            Check(slider.Value == 45, "Dragging track would not update slider value");
            slider.Value = original;
        });
    }
}
