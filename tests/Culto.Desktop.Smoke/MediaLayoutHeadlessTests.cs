using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    // Deliberately does not create Application, Window or HwndSource and never
    // pumps a dispatcher. It measures a disconnected visual tree in memory.
    private static int RunMediaLayoutHeadlessTests()
    {
        var initialSources = HwndSource.CurrentSources.Cast<PresentationSource>().Count();
        var items = new ObservableCollection<Cue>(Enumerable.Range(0, 10000)
            .Select(index => new Cue { Kind = CueKind.Video, Title = $"Synthetic {index:00000}" }));
        var tileFactory = new FrameworkElementFactory(typeof(MediaTilePanel));
        tileFactory.SetValue(MediaTilePanel.ItemWidthProperty, 184d);
        tileFactory.SetValue(MediaTilePanel.ItemHeightProperty, 148d);
        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(Cue.Title)));
        var list = new ListBox
        {
            ItemsSource = items,
            ItemsPanel = new ItemsPanelTemplate(tileFactory),
            ItemTemplate = new DataTemplate { VisualTree = textFactory }
        };
        ScrollViewer.SetCanContentScroll(list, true);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        var root = new Grid(); root.Children.Add(list);
        void Layout(double width = 800, double height = 360)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();
            }
        }
        MediaTilePanel Panel() => Descendants(list).OfType<MediaTilePanel>().Single();
        UIElement[] Realized() => Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(Panel()))
            .Select(index => (UIElement)VisualTreeHelper.GetChild(Panel(), index)).ToArray();
        void Bounded()
        {
            var panel = Panel();
            var budget = ((int)Math.Ceiling(panel.ViewportHeight / panel.ItemHeight) + 3) * panel.Columns + 1;
            Check(Realized().Length > 0 && Realized().Length <= budget,
                $"Realized {Realized().Length} elements for a viewport budget of {budget}");
        }
        void LayoutTest(string name, Action action)
        {
            try { action(); Console.WriteLine("PASS " + name); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error); }
        }

        LayoutTest("Offscreen gallery virtualizes 10000 items", () =>
        {
            Layout();
            Check(Panel().Columns == 4, "Wide viewport did not arrange four columns");
            Check(list.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem, "First item was not realized");
            Check(list.ItemContainerGenerator.ContainerFromIndex(9999) is null, "Distant item was realized eagerly");
            Bounded();
        });
        LayoutTest("Offscreen gallery recycles containers when scrolling to the end", () =>
        {
            var firstContainers = Realized();
            Panel().SetVerticalOffset(Panel().ExtentHeight);
            Layout();
            Check(list.ItemContainerGenerator.ContainerFromIndex(9999) is ListBoxItem, "Last item was not realized after scrolling");
            Check(list.ItemContainerGenerator.ContainerFromIndex(0) is null, "First item was not virtualized away");
            Check(Realized().Intersect(firstContainers).Any(), "Containers were recreated instead of recycled");
            Bounded();
        });
        LayoutTest("Offscreen gallery responds to narrow pane resizing", () =>
        {
            Layout(280, 360);
            Check(Panel().Columns == 1, "Narrow viewport did not use one column");
            Bounded();
            foreach (var child in Realized())
            {
                var bounds = child.TransformToAncestor(Panel()).TransformBounds(new Rect(child.RenderSize));
                Check(bounds.Left >= -.1 && bounds.Right <= Panel().ViewportWidth + .1, "Tile overflows the narrow viewport");
            }
            Panel().ItemHeight = 108;
            Layout(280, 260);
            Check(Math.Abs(Panel().ExtentHeight - 10000 * 108d) < .1, "Compact tile height did not update the scroll extent");
            Bounded();
            Panel().ItemHeight = 148;
        });
        LayoutTest("Offscreen ScrollIntoView realizes and exposes the requested item", () =>
        {
            Layout();
            list.ScrollIntoView(items[4567]); Layout();
            var row = list.ItemContainerGenerator.ContainerFromIndex(4567) as ListBoxItem;
            Check(row is not null, "ScrollIntoView did not realize the target");
            var bounds = row!.TransformToAncestor(Panel()).TransformBounds(new Rect(row.RenderSize));
            Check(bounds.Top >= -.1 && bounds.Bottom <= Panel().ViewportHeight + .1,
                "ScrollIntoView did not expose the entire target row");
            Bounded();
            list.ScrollIntoView(items[^1]); Layout();
            row = list.ItemContainerGenerator.ContainerFromIndex(items.Count - 1) as ListBoxItem;
            Check(row is not null, "ScrollIntoView did not realize the last item");
            bounds = row!.TransformToAncestor(Panel()).TransformBounds(new Rect(row.RenderSize));
            Check(bounds.Top >= -.1 && bounds.Bottom <= Panel().ViewportHeight + .1,
                "ScrollIntoView clipped the last row");
            Bounded();
        });
        LayoutTest("Offscreen gallery resets then repopulates without stale containers", () =>
        {
            items.Clear(); Layout();
            Check(Realized().Length == 0 && Panel().ExtentHeight == 0 && Panel().VerticalOffset == 0,
                "Clearing media retained containers, extent or scroll offset");
            for (var index = 0; index < 8; index++) items.Add(new Cue { Title = $"Replacement {index}" });
            Layout();
            Check(Realized().Length == 8, "Repopulated short folder did not realize its contents");
            Check(Realized().Cast<ListBoxItem>().All(row => items.Contains((Cue)row.DataContext)),
                "Recycled containers retained old media data");
            items.RemoveAt(2); Layout();
            items.Move(5, 1); Layout();
            items[3] = new Cue { Title = "Replaced item" }; Layout();
            Check(Realized().Length == items.Count, "Remove, move or replace broke container count");
            for (var index = 0; index < items.Count; index++)
                Check(ReferenceEquals(((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index)).DataContext, items[index]),
                    "Container/item order diverged after mutations");
        });
        LayoutTest("Thumbnail rows fit their width and keep scrolling geometry consistent", () =>
        {
            var property = typeof(MediaTilePanel).GetProperty("FitThumbnailsToWidth");
            Check(property is not null, "Gallery does not adapt thumbnail height to its width");
            property!.SetValue(Panel(), true);
            items.Clear();
            for (var index = 0; index < 10000; index++) items.Add(new Cue { Title = $"Adaptive {index}" });
            foreach (var width in new[] { 800d, 560d, 280d, 1000d })
            {
                Layout(width, 360);
                var panel = Panel();
                var expectedHeight = (panel.ViewportWidth / panel.Columns - 4) * 9 / 16 + 26;
                Check(Math.Abs(Realized().First().RenderSize.Height - expectedHeight) < 1, "Thumbnail cell has unused vertical space or wrong proportions");
                var rows = Math.Ceiling(items.Count / (double)panel.Columns);
                Check(Math.Abs(panel.ExtentHeight - rows * expectedHeight) < 1, "Scrolling still uses the old fixed row height");
                Check(Realized().Length <= (Math.Ceiling(panel.ViewportHeight / expectedHeight) + 3) * panel.Columns + 1,
                    "Adaptive mode stopped virtualizing a large folder");
                panel.SetVerticalOffset(panel.ExtentHeight); Layout(width, 360);
                Check(list.ItemContainerGenerator.ContainerFromIndex(items.Count - 1) is not null, "Adaptive gallery cannot reach its last item");
            }
        });
        LayoutTest("Headless media layout created no desktop window or application", () =>
        {
            Check(Application.Current is null, "A WPF application was created");
            Check(PresentationSource.FromVisual(root) is null && PresentationSource.FromVisual(list) is null,
                "Disconnected layout became attached to a presentation source");
            Check(HwndSource.CurrentSources.Cast<PresentationSource>().Count() == initialSources,
                "A new native presentation source was created");
        });
        Console.WriteLine($"Headless media layout: {failures} failure(s)");
        return failures == 0 ? 0 : 1;
    }
}
