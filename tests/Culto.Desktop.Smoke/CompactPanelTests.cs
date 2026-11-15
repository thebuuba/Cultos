using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunCompactPanelTests(MainWindow window)
    {
        RunPanelSizingTests(window);
        foreach (var template in new[] { "LibraryItemTemplate", "MediaRowTemplate", "OrderItemTemplate" })
        {
            Test(template + " stays compact and trims its title at narrow and wide widths", () =>
            {
                var cue = new Cue { Title = string.Join(" ", Enumerable.Repeat("Título largo de un video", 12)), Kind = CueKind.Video };
                var list = new ListBox
                {
                    Style = (Style)window.FindResource(typeof(ListBox)),
                    ItemContainerStyle = (Style)window.FindResource(template == "OrderItemTemplate" ? "OrderRow" : "CompactCueRow"),
                    ItemTemplate = (DataTemplate)window.FindResource(template),
                    ItemsSource = new[] { cue }
                };
                var host = new Window { Owner = window, Content = list, Width = 420, Height = 340, ShowInTaskbar = false, ShowActivated = false };
                host.Show();
                try
                {
                    foreach (var width in new[] { 400d, 180d, 400d })
                    {
                      host.Width = width + 20; host.UpdateLayout(); PumpFor(40);
                      var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                      var title = Descendants(row).OfType<TextBlock>().Single(text => text.Text == cue.Title);
                      Check(row.ActualHeight <= 40, $"Row grew to {row.ActualHeight} at width {width}");
                      Check(title.TextWrapping == TextWrapping.NoWrap && title.TextTrimming == TextTrimming.CharacterEllipsis,
                          "Title wraps or has no ellipsis");
                      Check(title.ActualWidth > 0 && title.ActualWidth < width && title.ActualHeight <= 22, "Title has no bounded single-line area");
                      Check(Descendants(row).OfType<MediaThumbnail>().Any(), "Compact row has no thumbnail");
                    }
                }
                finally { host.Close(); }
            });
        }
    }
}
