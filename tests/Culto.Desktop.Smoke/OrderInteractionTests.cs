using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock;
using AvalonDock.Layout;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

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
            Test("Order left click offers actions without changing output or capturing a popup", () =>
            {
                var live = session.Live;
                list.SelectedItem = a;
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = Row(a) });
                var actions = (FrameworkElement)window.FindName("OrderActionsPanel");
                Check(actions.Visibility == Visibility.Visible, "Left click did not offer row actions");
                Check(ReferenceEquals(actions.Tag, a), "Actions target a different order row");
                Check(ReferenceEquals(live, session.Live), "Opening actions projected a cue");
                Check(Mouse.Captured is not System.Windows.Controls.Primitives.Popup,
                    "Actions captured a popup and would intercept double click");
                Call(window, "PrepareCue", b, null);
                ((Button)window.FindName("OrderPlayButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.Live?.Id == a.Id, "Play used the prepared cue instead of the clicked order row");
                list.SelectedItem = b;
                Check(actions.Visibility == Visibility.Hidden, "Actions retained a stale row after selection changed");
            });
            Test("Order delete removes only its clicked row while live output and library remain intact", () =>
            {
                var removable = new Cue { Kind = CueKind.Video, Title = "Remove order row", FilePath = video.FilePath };
                var workspace = Field<WorkspaceData>(window, "_workspace");
                var libraryBefore = workspace.Library.ToArray();
                plan.Add(removable); list.SelectedItem = removable;
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = Row(removable) });
                var live = session.Live;
                ((Button)window.FindName("OrderDeleteButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(!plan.Contains(removable) && !workspace.Plan.Items.Contains(removable), "Delete did not persist the order change");
                Check(workspace.Library.SequenceEqual(libraryBefore) && File.Exists(video.FilePath), "Delete removed library content or a media file");
                Check(ReferenceEquals(live, session.Live), "Deleting an order row changed the projected cue");
                if (list.SelectedItem is null) Check(session.PreviewFollowsLive, "Removing the selection left a stale prepared cue");
                Check(((FrameworkElement)window.FindName("OrderActionsPanel")).Visibility == Visibility.Hidden,
                    "Delete left stale row actions visible");
                var count = plan.Count;
                Call(window, "DeleteOrderActionClick", window, new RoutedEventArgs());
                Check(plan.Count == count, "A stale delete action removed another row");
            });
            Test("Delete key removes the focused order selection and preserves live output and library", () =>
            {
                Call(window, "ActivateWorkspacePanel", "order"); window.UpdateLayout();
                var removable = new Cue { Title = "Quitar con Supr", Body = "Solo del orden" };
                var workspace = Field<WorkspaceData>(window, "_workspace");
                var library = workspace.Library.ToArray();
                plan.Add(removable); list.SelectedItem = removable; Row(removable).Focus();
                var live = session.Live;
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(list), Environment.TickCount, Key.Delete)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                list.RaiseEvent(key);
                Check(key.Handled && !plan.Contains(removable) && !workspace.Plan.Items.Contains(removable), "Delete key did not remove and persist selection");
                Check(ReferenceEquals(live, session.Live) && library.SequenceEqual(workspace.Library), "Delete key changed live output or library");
                list.SelectedItem = a;
                Call(window, "SelectContentSource", 0); window.UpdateLayout();
                var search = (TextBox)window.FindName("LibrarySearch"); search.Focus();
                var count = plan.Count;
                search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(search), Environment.TickCount, Key.Delete)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(plan.Count == count && plan.Contains(a), "Delete in another field removed an order cue");
            });
            Test("Order drag release and empty space do not offer actions or project", () =>
            {
                Call(window, "HideOrderActions");
                var live = session.Live;
                var actions = (FrameworkElement)window.FindName("OrderActionsPanel");
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
                Check(actions.Visibility == Visibility.Hidden, "Blank space offered stale actions");
                SetField(window, "_orderDragPerformed", true);
                try
                {
                    list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                        { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = Row(a) });
                    Check(actions.Visibility == Visibility.Hidden, "Drag release offered actions");
                    Check(ReferenceEquals(live, session.Live), "Drag release changed output");
                }
                finally { SetField(window, "_orderDragPerformed", false); }
            });
            Test("Order add menu opens each content source without projecting", () =>
            {
                var add = window.FindName("AddOrderButton") as Button;
                Check(add?.ContextMenu is not null, "Order has no add menu");
                var live = session.Live;
                var manager = (DockingManager)window.FindName("WorkspaceDock");
                foreach (var entry in new[] { ("AddOrderLibrary", 0, "SourcesPanel"), ("AddOrderBible", 1, "BiblePanel"), ("AddOrderText", 2, "TextPanel") })
                {
                    var item = add!.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == entry.Item1);
                    if (entry.Item1 == "AddOrderText")
                    {
                        var opened = false;
                        window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
                        {
                            var editor = Application.Current.Windows.OfType<Culto.Desktop.Dialogs.CueEditDialog>().Single();
                            opened = true; editor.DialogResult = false;
                        }));
                        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        Check(opened && ReferenceEquals(session.Live, live), "Announcement editor did not open independently of notes and live output");
                        continue;
                    }
                    item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Check(Field<int>(window, "_activeContentSource") == entry.Item2 && ReferenceEquals(manager.ActiveContent, window.FindName(entry.Item3)),
                        "Add menu opened wrong source");
                }
                Check(ReferenceEquals(live, session.Live), "Add menu changed live output");
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
            Test("Video preview follows the one output player until another cue is prepared", () =>
            {
                list.SelectedItem = video;
                var preview = Field<ProjectionSurface>(window, "_preview");
                Until(() => preview.IsPlaying, "Preparing media did not start its silent preview");
                Check(preview.Volume == 0, "Prepared preview is audible");
                DoubleClick(video);
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => output.IsPlaying && output.Duration > 0, "Double click did not play output video");
                var mirror = (OutputMonitor)window.FindName("PreviewLiveMonitor");
                Check(mirror.Visibility == Visibility.Visible && session.PreviewFollowsLive, "Preview did not follow live output");
                Check(window.FindName("PreviewPauseButton") is null, "Duplicate transport remains in preview");
                var brush = Field<System.Windows.Media.VisualBrush>(mirror, "_localImage");
                Check(ReferenceEquals(brush.Visual, output), "Preview is not drawing the actual output compositor");
                Check(!preview.IsPlaying && preview.Duration == 0, "Following output started another decoder");
                list.SelectedItem = a;
                Check(mirror.Visibility == Visibility.Collapsed && session.Live?.Id == video.Id, "Preparing text replaced live playback");
                var oldPosition = output.Position;
                Call(window, "FollowLivePreviewClick", window, new RoutedEventArgs());
                Check(mirror.Visibility == Visibility.Visible && output.IsPlaying && session.Live?.Id == video.Id,
                    "Returning to output interrupted playback");
                Check(output.Position >= oldPosition - .2, "Returning to output restarted video");
                output.SetPaused(true);
                Check(ReferenceEquals(Field<System.Windows.Media.VisualBrush>(mirror, "_localImage").Visual, output),
                    "Paused preview stopped sharing the output surface");
                Call(window, "ToggleBlackout");
                Check(mirror.Visibility == Visibility.Visible && session.IsBlackout, "Blackout disconnected preview");
                Call(window, "CloseOutput");
                Check(Field<object?>(mirror, "_localImage") is null, "Closed output left a stale preview");
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
