using System.IO;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunBibleVersionTests(MainWindow window)
    {
        Test("Bible version tabs use abbreviations and retain custom names", () =>
        {
            var converter = new BibleVersionLabelConverter();
            foreach (var (name, label) in new[] {
                ("Reina-Valera 1960 (RVR1960)", "RVR1960"),
                ("La Biblia de las Américas (LBLA)", "LBLA"),
                ("Biblica® Open Nueva Biblia Viva 2008", "NBV"),
                ("Valera 1602 Purificada", "RV1602P"),
                ("Mi versión personalizada", "Mi versión personalizada") })
                Check((string)converter.Convert(name, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture) == label,
                    "Incorrect version tab label: " + name);
        });
        Test("Bible versions coexist, keep the passage when switching and preserve live/order text", () =>
        {
            Call(window, "SelectContentSource", 1);
            var picker = window.FindName("BibleVersionPicker") as ListBox;
            Check(picker is not null, "Bible versions must be visible in a horizontal list, without opening a dropdown");
            var store = Field<WorkspaceStore>(window, "_store");
            string Source(string name, string prefix, bool extra)
            {
                var path = Path.Combine(store.DirectoryPath, name + ".json");
                var verses = new List<BibleVerse> { new("Génesis", 1, 1, prefix + " inicio"),
                    new("Juan", 3, 16, prefix + " dieciséis"), new("Juan", 3, 17, prefix + " diecisiete") };
                if (extra) verses.Add(new("Juan", 3, 18, prefix + " dieciocho"));
                JsonFileStore.Save(path, new BibleDocument { Name = name, Verses = verses });
                return path;
            }
            Wait((Task)Call(window, "ImportBibleFileAsync", Source("Primera sintética", "A", true))!);
            Wait((Task)Call(window, "ImportBibleFileAsync", Source("Segunda sintética", "B", false))!);
            Check(picker!.Items.Count == 2, "Importing a second Bible removed the first");
            window.UpdateLayout();
            var row = Descendants(picker).OfType<StackPanel>().Single(panel => panel.IsItemsHost);
            Check(row.Orientation == Orientation.Horizontal, "Versions must stay on one horizontal row");
            var results = (ListBox)window.FindName("BibleResults");
            Check(picker.TranslatePoint(new System.Windows.Point(), window).Y >=
                results.TranslatePoint(new System.Windows.Point(0, results.ActualHeight), window).Y,
                "Version tabs must appear below the verses");
            Check(ScrollViewer.GetHorizontalScrollBarVisibility(picker) == ScrollBarVisibility.Auto
                && ScrollViewer.GetVerticalScrollBarVisibility(picker) == ScrollBarVisibility.Disabled,
                "Narrow panels must scroll versions horizontally");
            BibleVersion Version(string name) => picker.Items.OfType<BibleVersion>().Single(item => item.Name == name);
            picker.SelectedItem = Version("Primera sintética");
            Until(() => Field<BibleCatalog>(window, "_bible").Document.Name == "Primera sintética", "Version selection did not load the Bible");
            var query = (TextBox)window.FindName("BibleQuery");
            query.Text = "Jn 3:16-17"; Call(window, "SearchBible");
            var session = Field<PresentationSession>(window, "_session");
            var original = session.Prepared!.Copy();
            session.RestoreOutput(original, false, false);
            var plan = Field<System.Collections.ObjectModel.ObservableCollection<Cue>>(window, "_plan");
            plan.Add(original.Copy(newId: true));
            var savedCue = plan.Last();
            picker.SelectedItem = Version("Segunda sintética");
            Until(() => session.Prepared?.Version == "Segunda sintética", "Switching did not prepare the same passage in the new version");
            Check(session.Prepared!.Reference == original.Reference && session.Prepared.Body.Contains("B dieciséis")
                && session.Prepared.Body.Contains("B diecisiete"), "Switching lost the selected range or text");
            Check(query.Text == "Jn 3:16-17", "Version selection erased the query");
            Check(session.Live?.Id == original.Id && session.Live.Body == original.Body && savedCue.Body == original.Body,
                "Changing version rewrote the projected passage or worship order");
            Check(JsonFileStore.Load<BibleDocument>(store.BiblePath)!.Name == "Segunda sintética", "Selected version did not persist");
            Call(window, "LoadSavedBible");
            Check(picker.Items.Count == 2 && ((BibleVersion)picker.SelectedItem).Name == "Segunda sintética", "Restart lost installed versions or selection");
            query.Text = "Consulta anterior";
            Call(window, "ShowBibleChapter", "Juan", 3, (int?)17);
            picker.SelectedItem = Version("Primera sintética");
            Until(() => Field<BibleCatalog>(window, "_bible").Document.Name == "Primera sintética", "Could not return to first version");
            Check(session.Prepared!.Reference == "Juan 3:17" && session.Prepared.Version == "Primera sintética",
                "A stale query replaced the verse chosen with the chapter/verse pickers");
            query.Clear(); Call(window, "ShowBibleChapter", "Juan", 3, (int?)18);
            picker.SelectedItem = Version("Segunda sintética");
            Until(() => Field<BibleCatalog>(window, "_bible").Document.Name == "Segunda sintética", "Missing reference prevented loading version");
            Check(((ListBox)window.FindName("BibleResults")).Items.Count == 0, "Missing verse silently selected a different verse");
            Check(session.Live!.Body == original.Body, "Missing verse changed live output");
            var second = Version("Segunda sintética");
            picker.SelectedItem = Version("Primera sintética");
            Until(() => Field<BibleCatalog>(window, "_bible").Document.Name == "Primera sintética", "First version did not restore");
            File.Delete(second.FilePath);
            if (File.Exists(second.FilePath + ".bak")) File.Delete(second.FilePath + ".bak");
            picker.SelectedItem = second;
            Until(() => picker.IsEnabled, "Failed selection left the version selector disabled");
            Check(Field<BibleCatalog>(window, "_bible").Document.Name == "Primera sintética"
                && JsonFileStore.Load<BibleDocument>(store.BiblePath)!.Name == "Primera sintética", "Missing file replaced the active Bible");
        });
        if (Environment.GetEnvironmentVariable("CULTO_BIBLE_LIBRARY_PROBE") is { } libraryDirectory)
        {
            Test("All installed Spanish translations load in the real selector and retain Juan 3:16", () =>
            {
                var store = Field<WorkspaceStore>(window, "_store");
                var library = new BibleVersionStore(store.DirectoryPath);
                foreach (var version in library.List().Where(v => v.Name.EndsWith("sintética")))
                {
                    File.Delete(version.FilePath);
                    if (File.Exists(version.FilePath + ".bak")) File.Delete(version.FilePath + ".bak");
                }
                var targetDirectory = Path.Combine(store.DirectoryPath, "Biblias");
                Directory.CreateDirectory(targetDirectory);
                foreach (var source in Directory.GetFiles(Path.Combine(libraryDirectory, "Biblias"), "*.json"))
                    File.Copy(source, Path.Combine(targetDirectory, Path.GetFileName(source)), overwrite: true);
                File.Copy(Path.Combine(libraryDirectory, "biblia.json"), store.BiblePath, overwrite: true);
                Call(window, "LoadSavedBible");
                var picker = (ListBox)window.FindName("BibleVersionPicker");
                Check(picker.Items.Count == 11, "Expected eleven installed versions");
                var query = (TextBox)window.FindName("BibleQuery");
                query.Text = "Juan 3:16"; Call(window, "SearchBible");
                var session = Field<PresentationSession>(window, "_session");
                var live = session.Live;
                var names = picker.Items.OfType<BibleVersion>().Select(v => v.Name).ToArray();
                var longestUiGap = 0d;
                var heartbeatClock = System.Diagnostics.Stopwatch.StartNew();
                var lastHeartbeat = heartbeatClock.Elapsed.TotalMilliseconds;
                var heartbeat = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
                    { Interval = TimeSpan.FromMilliseconds(16) };
                heartbeat.Tick += (_, _) =>
                {
                    var now = heartbeatClock.Elapsed.TotalMilliseconds;
                    longestUiGap = Math.Max(longestUiGap, now - lastHeartbeat);
                    lastHeartbeat = now;
                };
                heartbeat.Start();
                foreach (var name in names)
                {
                    var switchClock = System.Diagnostics.Stopwatch.StartNew();
                    picker.SelectedItem = picker.Items.OfType<BibleVersion>().Single(v => v.Name == name);
                    Until(() => picker.IsEnabled && session.Prepared?.Version == name, "Installed version did not load: " + name);
                    Check(session.Prepared!.Reference == "Juan 3:16" && !string.IsNullOrWhiteSpace(session.Prepared.Body), "Translation lost Juan 3:16: " + name);
                    Check(ReferenceEquals(live, session.Live), "Selecting a real translation changed live output");
                    Console.WriteLine($"VERSION {name}: {switchClock.Elapsed.TotalMilliseconds:F0} ms");
                }
                PumpFor(50);
                heartbeat.Stop();
                Console.WriteLine($"BIBLE UI longest dispatcher gap: {longestUiGap:F0} ms");
                Test("Bible switching keeps the dispatcher responsive", () =>
                    Check(longestUiGap < 250, $"Switching blocked the interface for {longestUiGap:F0} ms"));
                Test("Bible version strip retains its dark background while loading", () =>
                {
                    picker.IsEnabled = false;
                    try
                    {
                        window.UpdateLayout();
                        CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "bible-version-loading.png"));
                        var background = (System.Windows.Media.SolidColorBrush)Descendants(picker).OfType<Border>()
                            .First(border => ReferenceEquals(border.TemplatedParent, picker)).Background;
                        var color = background.Color;
                        Check(color.A < 240 || (color.R + color.G + color.B) / 3 < 200,
                            $"Loading painted a pale strip: {color}");
                    }
                    finally { picker.IsEnabled = true; }
                });
                window.UpdateLayout();
                Check(Descendants(picker).OfType<TextBlock>().Any(text => text.Text == "LBLA")
                    && Descendants(picker).OfType<TextBlock>().Any(text => text.Text == "RVR1960"),
                    "Installed versions must show their abbreviations on the tabs");
                var scroll = Descendants(picker).OfType<ScrollViewer>().First();
                Check(scroll.ScrollableWidth > 0, "The eleven versions must remain reachable through horizontal scrolling");
                PumpFor(100);
                window.UpdateLayout();
                var selectedRow = (ListBoxItem)picker.ItemContainerGenerator.ContainerFromItem(picker.SelectedItem);
                Test("Focused Bible tab retains the selected accent color", () =>
                {
                    selectedRow.Focus();
                    window.UpdateLayout();
                    var chip = (Border)selectedRow.Template.FindName("Chip", selectedRow);
                    Check(((System.Windows.Media.SolidColorBrush)chip.Background).Color ==
                        ((System.Windows.Media.SolidColorBrush)window.FindResource("ActionBrush")).Color,
                        "Click or keyboard focus replaced the selected tab with a pale background");
                });
                var selectedPosition = selectedRow.TranslatePoint(new System.Windows.Point(), scroll);
                Check(selectedPosition.X >= -1 && selectedPosition.X + selectedRow.ActualWidth <= scroll.ActualWidth + 1,
                    "The active version tab must be fully visible after switching");
                CaptureDesignWindow(window, Path.Combine(Environment.CurrentDirectory, ".tools", "bible-version-selector.png"));
            });
        }
    }
}
