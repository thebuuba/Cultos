using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;

internal static partial class Program
{
    private static void RunOrderInteractionTests(MainWindow window)
    {
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var list = (ListBox)window.FindName("PlanList");
        var session = Field<PresentationSession>(window, "_session");
        var oldPlan = plan.ToArray();
        var a = new Cue { Title = "Orden A", Body = "Primero" };
        var b = new Cue { Title = "Orden B", Body = "Segundo" };
        var video = new Cue { Title = "Video del orden", Kind = CueKind.Video,
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        ListBoxItem Row(Cue cue)
        {
            list.ScrollIntoView(cue); list.UpdateLayout();
            return (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(cue);
        }
        // These WPF mouse events are direct; preserve the hit row as their source.
        void DoubleClick(Cue cue) => list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Control.MouseDoubleClickEvent, Source = Row(cue) });
        try
        {
            plan.Clear(); plan.Add(a); plan.Add(b); plan.Add(video); Call(window, "UpdatePlanCount");
            Test("Order single click previews including an already selected row without changing output", () =>
            {
                list.SelectedItem = a; Call(window, "ProjectPrepared");
                var live = session.Live;
                list.SelectedItem = b;
                Check(session.Prepared?.Id == b.Id && ReferenceEquals(live, session.Live), "Selection changed live output");
                Call(window, "PrepareCue", a, null);
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = Row(b) });
                Check(session.Prepared?.Id == b.Id && ReferenceEquals(live, session.Live), "Click on selected row did not restore its preview");
            });
            Test("Order double click projects its row instead of another prepared cue and ignores blank space", () =>
            {
                Call(window, "PrepareCue", a, null);
                DoubleClick(b);
                Check(session.Live?.Id == b.Id, "Double click did not project the clicked row");
                var live = session.Live;
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent });
                Check(ReferenceEquals(live, session.Live), "Blank-space double click reprojected a cue");
            });
            Test("Order add menu opens each content source without projecting", () =>
            {
                var add = window.FindName("AddOrderButton") as Button;
                Check(add?.ContextMenu is not null, "Order has no add menu");
                var live = session.Live;
                foreach (var entry in new[] { ("AddOrderLibrary", 0), ("AddOrderBible", 1), ("AddOrderText", 2) })
                {
                    var item = add!.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == entry.Item1);
                    item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Check(((TabControl)window.FindName("ContentTabs")).SelectedIndex == entry.Item2, "Add menu opened wrong source");
                }
                Check(ReferenceEquals(live, session.Live), "Add menu changed live output");
            });
            Test("Sequence thumbnails follow order and preserve single and double click behavior", () =>
            {
                var strip = window.FindName("SequenceList") as ListBox;
                Check(strip is not null, "Sequence strip is missing");
                Check(strip!.Items.Cast<Cue>().SequenceEqual(plan), "Sequence is not bound to the actual order");
                var live = session.Live;
                strip.SelectedItem = a;
                window.UpdateLayout();
                Check(ReferenceEquals(list.SelectedItem, a) && session.Prepared?.Id == a.Id, "Thumbnail selection did not prepare its cue");
                Check(ReferenceEquals(live, session.Live), "Single thumbnail selection projected");
                Call(window, "PrepareCue", b, null);
                strip.ScrollIntoView(a); strip.UpdateLayout();
                var row = (ListBoxItem)strip.ItemContainerGenerator.ContainerFromItem(a);
                strip.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = row });
                Check(session.Prepared?.Id == a.Id, "Revisiting an already selected thumbnail failed");
                strip.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = row });
                Check(session.Live?.Id == a.Id, "Thumbnail double click did not project its cue");
                live = session.Live;
                strip.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent });
                Check(ReferenceEquals(live, session.Live), "Empty sequence space projected");
                plan.Move(0, 1); window.UpdateLayout();
                Check(strip.Items.Cast<Cue>().SequenceEqual(plan), "Sequence order became stale after reordering");
            });
            Test("Library double click projects its row and ignores empty space", () =>
            {
                var library = (ListBox)window.FindName("LibraryList");
                var originalSource = library.ItemsSource;
                try
                {
                    Call(window, "SelectContentSource", 0);
                    library.ItemsSource = new[] { video };
                    library.SelectedItem = video; library.ScrollIntoView(video); window.UpdateLayout();
                    var row = (ListBoxItem)library.ItemContainerGenerator.ContainerFromIndex(0);
                    Check(row is not null && row.IsVisible, "Library test row is not visible");
                    library.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                        { RoutedEvent = Control.MouseDoubleClickEvent, Source = row });
                    Check(session.Live?.Id == video.Id && Field<ProjectionSurface>(window, "_output").IsTransitioning,
                        "Library double click did not send its video with a dissolve");
                    var live = session.Live;
                    Call(window, "PrepareCue", a, null);
                    library.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                        { RoutedEvent = Control.MouseDoubleClickEvent });
                    Check(ReferenceEquals(session.Live, live), "Empty library space projected the prepared cue");
                    Call(window, "ProjectPrepared");
                }
                finally { library.ItemsSource = originalSource; }
            });
            Test("Video double click starts output while mini player pause seek and restart stay independent", () =>
            {
                list.SelectedItem = video;
                var preview = Field<ProjectionSurface>(window, "_preview");
                Until(() => preview.IsPlaying && preview.Duration > 0, "Video did not start in mini player");
                DoubleClick(video);
                var output = Field<ProjectionSurface>(window, "_output");
                Check(output.GetType().GetProperty("IsTransitioning")?.GetValue(output) is true,
                    "Double click cut directly to video without a dissolve");
                Until(() => output.IsPlaying && output.Duration > 0, "Double click did not play output video");
                var panel = window.FindName("PreviewPlaybackPanel") as FrameworkElement;
                Check(panel?.Visibility == Visibility.Visible, "Mini player controls are missing");
                Call(window, "PreviewPauseClick", window.FindName("PreviewPauseButton"), new RoutedEventArgs());
                Until(() => !preview.IsPlaying, "Mini player did not pause");
                Check(output.IsPlaying && preview.Volume == 0, "Preview pause/mute changed live playback");
                var slider = (Slider)window.FindName("PreviewPositionSlider");
                slider.Value = .5;
                Call(window, "PreviewPositionSeek", slider, new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left));
                Until(() => preview.Position >= preview.Duration * .35, "Mini player did not seek");
                Call(window, "PreviewRestartClick", window, new RoutedEventArgs());
                Until(() => preview.IsPlaying && preview.Position < preview.Duration * .3, "Mini player did not restart");
                Check(output.IsPlaying, "Restarting preview stopped live playback");
                list.SelectedItem = a;
                Check(panel!.Visibility == Visibility.Collapsed && session.Live?.Id == video.Id, "Text preview retained video controls or replaced output");
            });
        }
        finally
        {
            Call(window, "CloseOutput");
            plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue);
            Call(window, "UpdatePlanCount");
            if (plan.Count > 0) list.SelectedIndex = 0;
        }
    }
}
