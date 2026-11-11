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
            Test("Video double click starts output while mini player pause seek and restart stay independent", () =>
            {
                list.SelectedItem = video;
                var preview = Field<ProjectionSurface>(window, "_preview");
                Until(() => preview.IsPlaying && preview.Duration > 0, "Video did not start in mini player");
                DoubleClick(video);
                var output = Field<ProjectionSurface>(window, "_output");
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
