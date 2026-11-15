using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunDenseMediaTests(MainWindow window)
    {
        window.Width = 1900; window.Height = 1000;
        Call(window, "ActivateWorkspacePanel", "media");
        Call(window, "SetMediaView", "Thumbnails", false);
        var list = (ListBox)window.FindName("MediaList");
        var previous = list.ItemsSource;
        var cues = Enumerable.Range(1, 100).Select(index => new Cue { Kind = CueKind.Video,
            Title = $"{index:000} Video con un título largo que debe quedar en una línea",
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") }).ToArray();
        list.ItemsSource = cues; window.UpdateLayout(); PumpFor(200);
        try
        {
            Test("Media thumbnails have square corners and only a small gap between cards", () =>
            {
                var panel = Descendants(list).OfType<MediaTilePanel>().Single();
                Check(panel.FitThumbnailsToWidth && panel.Columns >= 2, $"Gallery is not using a multi-column adaptive layout: fit={panel.FitThumbnailsToWidth}, width={panel.ActualWidth}, columns={panel.Columns}");
                ListBoxItem Row(int index) => (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);
                var first = Row(0); var next = Row(1); var below = Row(panel.Columns);
                var a = first.TransformToAncestor(panel).TransformBounds(new Rect(first.RenderSize));
                var b = next.TransformToAncestor(panel).TransformBounds(new Rect(next.RenderSize));
                var c = below.TransformToAncestor(panel).TransformBounds(new Rect(below.RenderSize));
                Check(b.Left - a.Right is >= 0 and <= 4 && c.Top - a.Bottom is >= 0 and <= 4, "Gallery still has large spaces between cards");
                var thumbnail = Descendants(first).OfType<MediaThumbnail>().Single();
                Until(() => Field<System.Windows.Media.Imaging.BitmapSource?>(thumbnail, "_image") is not null,
                    "Gallery did not load an actual video frame");
                Check(thumbnail.CornerRadius == 0 && Math.Abs(thumbnail.ActualWidth / thumbnail.ActualHeight - 16d / 9) < .02,
                    "Thumbnail is rounded or distorted");
                Check(Descendants(first).OfType<Border>().All(border => border.CornerRadius == new CornerRadius(0)), "A card still has rounded corners");
                var title = Descendants(first).OfType<TextBlock>().Single(text => text.Text == cues[0].Title);
                Check(title.TextWrapping == TextWrapping.NoWrap && title.TextTrimming == TextTrimming.CharacterEllipsis, "Caption is not a compact single line");
                CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "dense-media.png"));
            });
            Test("Adaptive media gallery can scroll to the last file", () =>
            {
                list.ScrollIntoView(cues[^1]); window.UpdateLayout(); PumpFor(100);
                Check(list.ItemContainerGenerator.ContainerFromIndex(cues.Length - 1) is ListBoxItem { IsVisible: true }, "Last media file is unreachable");
            });
        }
        finally { list.ItemsSource = previous; }
    }
}
