using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunUsabilityTests(MainWindow window, string directory)
    {
        var session = Field<PresentationSession>(window, "_session");
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var tabs = (TabControl)window.FindName("ContentTabs");
        var search = (TextBox)window.FindName("LibrarySearch");
        var filter = (ListBox)window.FindName("LibraryFilter");
        var library = (ListBox)window.FindName("LibraryList");
        var planList = (ListBox)window.FindName("PlanList");
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var oldLibrary = workspace.Library.ToList();
        var oldPlan = plan.ToList();
        var oldQuery = search.Text;
        var oldFilter = filter.SelectedIndex;
        var oldTab = tabs.SelectedIndex;
        var oldLive = session.Live;
        var oldPrepared = session.Prepared;
        var oldPreparedReading = Field<IReadOnlyList<Cue>?>(window, "_preparedReading");
        var oldLiveReading = Field<IReadOnlyList<Cue>?>(window, "_liveReading");
        var originalOutput = Field<object?>(window, "_outputWindow");
        var hymn13 = new Cue { Title = "013 Himno sintético trece", HymnNumber = 13 };
        var hymn113 = new Cue { Title = "113 Himno sintético ciento trece", HymnNumber = 113 };
        var title13 = new Cue { Title = "Aviso sintético 13", Body = "Texto sintético de prueba" };
        try
        {
            workspace.Library = [hymn13, hymn113, title13];
            tabs.SelectedIndex = 0; filter.SelectedIndex = 0; search.Clear();
            Call(window, "RefreshLibrary");
            Test("Numeric hymn search matches 13 exactly instead of 113 or title substrings", () =>
            {
                var live = session.Live;
                search.Text = "13";
                Check(library.Items.Count == 1 && ((Cue)library.Items[0]).Id == hymn13.Id, "Numeric query 13 included hymn 113 or a title substring");
                search.Text = "0013";
                Check(library.Items.Count == 1 && ((Cue)library.Items[0]).Id == hymn13.Id, "Leading zeros did not resolve the exact hymn number");
                search.Text = "113";
                Check(library.Items.Count == 1 && ((Cue)library.Items[0]).Id == hymn113.Id, "Exact hymn 113 was not found");
                filter.SelectedIndex = 5; search.Text = "13";
                Check(library.Items.Cast<Cue>().Any(cue => cue.Id == title13.Id), "Numeric title search in Texts incorrectly required a hymn number");
                filter.SelectedIndex = 0;
                Check(ReferenceEquals(live, session.Live), "Numeric search changed the live snapshot");
            });
            Test("Library Enter prepares a visible result and focuses the list without projecting", () =>
            {
                search.Text = "13";
                library.SelectedIndex = -1;
                var live = session.Live;
                var keyboardEvent = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(search)!, Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent };
                search.Focus(); search.RaiseEvent(keyboardEvent);
                Check(keyboardEvent.Handled && session.Prepared?.Id == hymn13.Id, "Enter did not prepare the exact visible hymn result");
                Until(() => library.IsKeyboardFocusWithin, "Enter did not move keyboard focus to the result list");
                Check(ReferenceEquals(live, session.Live), "Enter projected content instead of only preparing it");
                Call(window, "PrepareCue", title13, null);
                search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(search)!, Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
                Check(session.Prepared?.Id == hymn13.Id, "Enter on an already selected result did not prepare it again");
                search.Text = "sin-resultados-sintéticos";
                var prepared = session.Prepared;
                search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(search)!, Environment.TickCount, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
                Check(ReferenceEquals(prepared, session.Prepared) && ReferenceEquals(live, session.Live), "Enter with no results changed prepared/live content");
            });
            KeyBinding BindingFor(Key key) => window.InputBindings.OfType<KeyBinding>()
                .Single(binding => binding.Key == key && binding.Modifiers == ModifierKeys.Control);
            Test("Ctrl 1 2 3 changes source and Ctrl F selects the active source query", () =>
            {
                var live = session.Live;
                var bible = (TextBox)window.FindName("BibleQuery");
                var text = (TextBox)window.FindName("TextBodyBox");
                foreach (var entry in new[] { (Key.D1, 0, search), (Key.D2, 1, bible), (Key.D3, 2, text) })
                {
                    entry.Item3.Text = "Consulta sintética";
                    BindingFor(entry.Item1).Command.Execute(null);
                    Until(() => tabs.SelectedIndex == entry.Item2 && entry.Item3.IsKeyboardFocusWithin, "Source shortcut did not select and focus its source");
                    entry.Item3.Select(0, 0);
                    BindingFor(Key.F).Command.Execute(null);
                    Until(() => entry.Item3.SelectionLength == entry.Item3.Text.Length && entry.Item3.IsKeyboardFocusWithin, "Ctrl F did not select the active query/text");
                }
                Check(ReferenceEquals(live, session.Live), "Source shortcuts changed the live snapshot");
            });
            Test("Search and source shortcuts ignore input before initialization is ready", () =>
            {
                var selected = tabs.SelectedIndex; var prepared = session.Prepared;
                SetField(window, "_ready", false);
                try
                {
                    BindingFor(Key.D1).Command.Execute(null);
                    Call(window, "LibrarySearchKeyDown", search, new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, Key.Enter));
                    Check(tabs.SelectedIndex == selected && ReferenceEquals(prepared, session.Prepared), "An initialization shortcut changed source or prepared state");
                }
                finally { SetField(window, "_ready", true); }
            });
            Test("Playback controls follow live media while preview controls follow prepared media", () =>
            {
                Call(window, "CloseOutput");
                var mediaPanel = (FrameworkElement)window.FindName("MediaPlaybackPanel");
                var previewPause = (Button)window.FindName("PreviewPauseButton");
                var navigation = (FrameworkElement)window.FindName("ScreenNavigationPanel");
                var outputMode = (TextBlock)window.FindName("OutputModeText");
                Check(outputMode.Text == "SIN SALIDA", "Closed output retained a live mode indicator");
                Call(window, "PrepareCue", new Cue { Title = "Texto sintético", Kind = CueKind.Text }, null);
                Check(mediaPanel.Visibility == Visibility.Collapsed && previewPause.Visibility == Visibility.Collapsed, "Text/closed output shows irrelevant media controls");
                var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv");
                Call(window, "PrepareCue", new Cue { Title = "Video sintético", Kind = CueKind.Video, FilePath = fixture }, null);
                Check(previewPause.Visibility == Visibility.Visible && mediaPanel.Visibility == Visibility.Collapsed, "Preparing video exposed live controls or hid preview playback");
                Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
                Call(window, "ProjectPrepared");
                Check(mediaPanel.Visibility == Visibility.Visible, "Live video has no playback controls");
                Call(window, "ShowBackground");
                Call(window, "PrepareCue", title13, null);
                Check(previewPause.Visibility == Visibility.Collapsed && mediaPanel.Visibility == Visibility.Visible, "Prepared text hid the controls for the still-live video");
                Check(outputMode.Text == "FONDO", "Preparing another cue hid the persistent background-only indicator");
                Call(window, "ToggleBlackout");
                Check(outputMode.Text == "NEGRO", "Blackout did not take priority over background-only mode");
                Call(window, "ToggleBlackout");
                Check(outputMode.Text == "FONDO", "Restoring blackout lost the retained background-only indicator");
                Check(navigation.Visibility == Visibility.Visible, "Screen navigation disappeared with contextual controls");
                Call(window, "ProjectPrepared");
                Check(mediaPanel.Visibility == Visibility.Collapsed, "Live text retained video playback controls");
                Check(outputMode.Text == "ENSAYO" && !session.IsBlackout && !session.IsTextHidden, "Explicit Project did not restore the normal output mode");
            });
            Test("Next guidance and live marker follow live order without changing selected prepared cue", () =>
            {
                var a = new Cue { Title = "Orden sintético A", Body = "A" };
                var b = new Cue { Title = "Orden sintético B", Body = "B" };
                var c = new Cue { Title = "Orden sintético C", Body = "C" };
                plan.Clear(); plan.Add(a); plan.Add(b); plan.Add(c); Call(window, "UpdatePlanCount");
                planList.SelectedItem = a; Call(window, "ProjectPrepared");
                planList.SelectedItem = c;
                var next = (TextBlock)window.FindName("NextLiveText");
                Check(next.Text.Contains(b.Title), "Next guidance followed selected/prepared instead of live order");
                planList.UpdateLayout();
                bool IsLive(Cue cue)
                {
                    planList.ScrollIntoView(cue); planList.UpdateLayout();
                    var row = (ListBoxItem)planList.ItemContainerGenerator.ContainerFromItem(cue);
                    var getter = planList.GetType().GetMethod("GetIsLive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    return getter?.Invoke(null, [row]) is true;
                }
                Check(IsLive(a) && !IsLive(c), "Live row marker tracks selection instead of live ID");
                Call(window, "MoveLive", 1);
                Check(session.Live?.Id == b.Id && ((Cue)planList.SelectedItem).Id == c.Id, "Next changed the operator's order selection");
                Check(IsLive(b) && !IsLive(a) && next.Text.Contains(c.Title), "Moving live did not update marker/next guidance");
                Call(window, "MoveUpClick", window, new RoutedEventArgs());
                Check(next.Text.Contains("Fin", StringComparison.OrdinalIgnoreCase), "Reorder left stale next guidance");
                Call(window, "RemoveCueClick", window, new RoutedEventArgs());
                Check(session.Live?.Id == b.Id, "Removing another selected cue changed live output");
                Call(window, "CloseOutput");
                Check(!IsLive(b), "Closed output retained a misleading live row marker");
            });
        }
        finally
        {
            workspace.Library = oldLibrary;
            plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue); Call(window, "UpdatePlanCount");
            search.Text = oldQuery; filter.SelectedIndex = oldFilter; Call(window, "RefreshLibrary"); tabs.SelectedIndex = oldTab;
            if (oldLive is not null) { Call(window, "PrepareCue", oldLive, oldLiveReading); Call(window, "ProjectPrepared"); }
            if (oldPrepared is not null) Call(window, "PrepareCue", oldPrepared, oldPreparedReading);
            if (originalOutput is null) Call(window, "CloseOutput");
        }
    }
}
