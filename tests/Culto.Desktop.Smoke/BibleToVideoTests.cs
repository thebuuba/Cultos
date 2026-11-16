using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunBibleToVideoTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        workspace.Transitions = new TransitionOptions { Kind = "Dissolve", DurationMilliseconds = 1250,
            AudioFadeOutMilliseconds = 1600, AudioFadeInMilliseconds = 1500, AudioFadeEffect = "Cathedral" };
        workspace.BibleStyle = new BibleVisualStyle { Transition = "TextPreset:Blur:Words:5", TransitionMilliseconds = 300 };
        ((Slider)window.FindName("VolumeSlider")).Value = 0;
        var media = (ListBox)window.FindName("MediaList");
        var originalItems = media.ItemsSource;
        var originalFolderItems = Field<IReadOnlyList<Cue>>(window, "_folderItems");
        var session = Field<PresentationSession>(window, "_session");
        var video = new Cue { Title = "Video sintético después de Biblia", Kind = CueKind.Video,
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        var bible = new Cue { Kind = CueKind.Bible, Title = "Prueba 1:1", Reference = "Prueba 1:1", Version = "Prueba",
            Body = "Texto sintético de un pasaje para comprobar el cambio al video." };
        var doubleClicks = 0;
        var refreshDuringPress = false;
        MouseButtonEventHandler countDoubleClick = (_, _) => doubleClicks++;
        MouseButtonEventHandler countPress = (_, args) =>
        {
            var position = args.GetPosition(window);
            Console.WriteLine($"MEDIA press count={args.ClickCount} time={args.Timestamp} point=({position.X:F1},{position.Y:F1}) source={args.OriginalSource.GetType().Name}");
            if (refreshDuringPress && args.ClickCount == 1)
            {
                refreshDuringPress = false;
                window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    var refreshed = Field<IReadOnlyList<Cue>>(window, "_folderItems").Select(item => item.Copy()).ToArray();
                    SetField(window, "_folderItems", refreshed);
                    Call(window, "RefreshMedia"); window.UpdateLayout();
                    Console.WriteLine("MEDIA folder refreshed during first press");
                }));
            }
        };
        media.AddHandler(Control.MouseDoubleClickEvent, countDoubleClick, true);
        media.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, countPress, true);
        try
        {
            Test("A folder rescan during a media press still prepares the same file on release", () =>
            {
                Call(window, "PrepareCue", bible, null); Call(window, "ProjectPrepared");
                Call(window, "ActivateWorkspacePanel", "media");
                SetField(window, "_folderItems", new[] { video });
                media.ItemsSource = Field<ObservableCollection<Cue>>(window, "_mediaView");
                Call(window, "RefreshMedia"); window.UpdateLayout();
                SetField(window, "_contentDragSource", media);
                SetField(window, "_contentDragCue", video);
                var rescanned = video.Copy();
                SetField(window, "_folderItems", new[] { rescanned });
                Call(window, "RefreshMedia"); window.UpdateLayout();
                var row = (ListBoxItem)media.ItemContainerGenerator.ContainerFromItem(rescanned);
                media.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Source = row });
                Check(session.Prepared?.Id == video.Id && session.Prepared.Kind == CueKind.Video,
                    "Rescanning the same file consumed the first click and left Bible prepared");
                Check(session.Live?.Id == bible.Id, "A single media click changed live output");
            });
            foreach (var (duringAnimation, clipped, refresh) in new[] { (false, false, true), (false, true, false), (false, false, false), (true, false, false) })
            Test($"A single video double click replaces Bible (animating={duringAnimation}, clipped={clipped}, refresh={refresh})", () =>
            {
                workspace.BibleStyle.TransitionMilliseconds = duringAnimation ? 1800 : 300;
                Call(window, "PrepareCue", bible, null); Call(window, "ProjectPrepared");
                var output = Field<ProjectionSurface>(window, "_output");
                Until(() => output.DisplayedCue?.Id == bible.Id, "Bible did not reach output");
                if (!duringAnimation) Until(() => !output.IsTransitioning, "Bible did not finish its entry");
                Call(window, "ActivateWorkspacePanel", "media");
                var candidates = Enumerable.Range(0, clipped ? 30 : 1).Select(index =>
                    new Cue { Title = $"Video de prueba {index}", Kind = CueKind.Video, FilePath = video.FilePath }).ToArray();
                SetField(window, "_folderItems", candidates);
                media.ItemsSource = Field<ObservableCollection<Cue>>(window, "_mediaView");
                Call(window, "RefreshMedia");
                refreshDuringPress = refresh;
                media.SelectedIndex = -1;
                window.Activate(); window.UpdateLayout();
                video = candidates[0];
                media.ScrollIntoView(video); window.UpdateLayout();
                if (clipped)
                {
                    var scroll = Descendants(media).OfType<ScrollViewer>().First();
                    scroll.ScrollToVerticalOffset(25); window.UpdateLayout();
                    video = candidates.First(item => media.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem candidate
                        && candidate.TranslatePoint(new Point(), scroll).Y > 0
                        && candidate.TranslatePoint(new Point(0, candidate.ActualHeight), scroll).Y > scroll.ActualHeight);
                }
                var row = (ListBoxItem)media.ItemContainerGenerator.ContainerFromItem(video);
                Check(row is not null && row.IsVisible, "Video row was not available for a real click");
                var before = doubleClicks;
                media.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = row });
                PumpFor(50);
                // A scan can recycle the row between press and projection.
                var current = media.Items.OfType<Cue>().Single(item => item.Id == video.Id);
                var currentRow = (ListBoxItem)media.ItemContainerGenerator.ContainerFromItem(current);
                media.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = currentRow });
                PumpFor(750);
                Console.WriteLine($"MEDIA first-double={doubleClicks - before} prepared={session.Prepared?.Kind} live={session.Live?.Kind} displayed={output.DisplayedCue?.Kind} loading={output.IsPreparingPlaybackEngine} transition={output.IsTransitioning} paused={output.IsPaused} status={((TextBlock)window.FindName("StatusText")).Text}");
                Check(doubleClicks == before + 1, "The first gesture did not deliver a double-click event");
                Check(session.Live?.Id == video.Id, "The first double click did not request video projection");
                Until(() => output.DisplayedCue?.Id == video.Id && output.IsPlaying && output.Position > .1,
                    "The first double click needed a second request to show video");
                output.SetPaused(true);
            });
        }
        finally
        {
            media.RemoveHandler(Control.MouseDoubleClickEvent, countDoubleClick);
            media.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, countPress);
            media.ItemsSource = originalItems;
            SetField(window, "_folderItems", originalFolderItems);
            Call(window, "CloseOutput");
        }
    }

}
