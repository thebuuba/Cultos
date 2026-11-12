using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunBibleNavigationTests(MainWindow window, string directory)
    {
        var session = Field<PresentationSession>(window, "_session");
        var previousCatalog = Field<BibleCatalog?>(window, "_bible");
        var previousPrepared = session.Prepared;
        var previousPreparedReading = Field<IReadOnlyList<Cue>?>(window, "_preparedReading");
        var previousSource = Field<int>(window, "_activeContentSource");
        var query = (TextBox)window.FindName("BibleQuery");
        var previousQuery = query.Text;
        var book = (ComboBox)window.FindName("BibleBookPicker");
        var chapter = (ComboBox)window.FindName("BibleChapterPicker");
        var verse = (ComboBox)window.FindName("BibleVersePicker");
        var results = (ListBox)window.FindName("BibleResults");
        var resultText = (TextBlock)window.FindName("BibleResultText");
        var live = session.Live;
        var liveBody = live?.Body;
        var liveReference = live?.Reference;
        var synthetic = new BibleCatalog(new BibleDocument
        {
            Name = "Navegación de prueba sintética", Attribution = "Contenido sintético, no es una traducción bíblica",
            Verses = new List<BibleVerse>
            {
                new("Apocalipsis", 22, 21, "Final sintético"),
                new("Juan", 3, 3, "Tercero sintético"),
                new("Juan", 3, 1, "Primero sintético"),
                new("Juan", 3, 2, "Marcador común sintético de Juan"),
                new("Juan", 4, 1, "Otro capítulo sintético"),
                new("Génesis", 2, 5, "Marcador común sintético de Génesis"),
                new("Génesis", 2, 7, "Otro versículo existente sintético"),
                new("2 Tesalonicenses", 1, 1, "Primera carta sintética"),
                new("2 Tesalonicenses", 2, 1, "Primero del segundo capítulo sintético"),
                new("2 Tesalonicenses", 2, 2, "Marcador común sintético de la carta"),
                new("2 Tesalonicenses", 3, 1, "Tercer capítulo sintético")
            }.Concat(Enumerable.Range(1, 176).Select(number => new BibleVerse("Salmos", 119, number, $"Versículo largo sintético {number}"))).ToList()
        });
        void UnchangedLive() => Check(ReferenceEquals(live, session.Live) && session.Live?.Body == liveBody && session.Live?.Reference == liveReference,
            "Bible navigation changed the live snapshot");
        void Search(string text) { query.Text = text; Call(window, "SearchBible"); window.UpdateLayout(); }
        try
        {
            Call(window, "SelectContentSource", 1);
            Test("Installing a Bible populates existing canonical books without replacing prepared or live", () =>
            {
                var prepared = session.Prepared;
                Call(window, "InstallBibleCatalog", synthetic);
                Check(book.Items.Cast<string>().SequenceEqual(new[] { "Génesis", "Salmos", "Juan", "2 Tesalonicenses", "Apocalipsis" }),
                    "Book picker includes missing books or uses noncanonical order");
                Check(ReferenceEquals(prepared, session.Prepared), "Installing the catalog automatically replaced prepared content");
                UnchangedLive();
            });

            Test("Book and chapter navigation use existing data and prepare without projection", () =>
            {
                query.Text = "Consulta conservada";
                book.SelectedItem = "Juan";
                book.SelectedItem = "Génesis";
                Check(chapter.Items.Cast<int>().SequenceEqual(new[] { 2 }), "Chapter picker assumed a chapter absent from imported data");
                Check(verse.Items.Cast<int>().SequenceEqual(new[] { 5, 7 }), "Verse picker fabricated missing verses");
                Check(results.Items.Count == 2 && session.Prepared!.Reference == "Génesis 2:5", "Choosing a book did not open its first available chapter");
                book.SelectedItem = "2 Tesalonicenses";
                Check(chapter.Items.Cast<int>().SequenceEqual(new[] { 1, 2, 3 }), "Numbered book lost its available chapters");
                Check(results.Items.Count == 1 && resultText.Text.StartsWith("1 versículo ·"), "Single-verse result count uses an incorrect plural");
                chapter.SelectedItem = 2;
                Check(results.Items.Count == 2 && session.Prepared!.Reference == "2 Tesalonicenses 2:1", "Choosing a chapter did not prepare its first verse");
                verse.SelectedItem = 2;
                Check(session.Prepared!.Reference == "2 Tesalonicenses 2:2" && ((BibleResult)results.SelectedItem!).Reference == "2 Tesalonicenses 2:2",
                    "Choosing a verse did not select and prepare the requested content");
                Check(query.Text == "Consulta conservada", "Picker navigation overwrote the query");
                UnchangedLive();
            });

            Test("Chapter queries with aliases synchronize pickers and preserve the written query", () =>
            {
                Search("Jn 3");
                Check(results.Items.Count == 3 && session.Prepared!.Reference == "Juan 3:1", "Chapter query did not return the complete chapter");
                Check((string)book.SelectedItem == "Juan" && (int)chapter.SelectedItem == 3 && (int)verse.SelectedItem == 1,
                    "Chapter search did not synchronize the navigation pickers");
                Check(query.Text == "Jn 3", "Synchronizing pickers rewrote the chapter query");
                Search("II Tes 2");
                Check(results.Items.Count == 2 && (string)book.SelectedItem == "2 Tesalonicenses", "Roman numbered-book chapter query failed");
                Search("2. Tes 2");
                Check(results.Items.Count == 2 && session.Prepared!.Reference == "2 Tesalonicenses 2:1", "Dotted ordinal was confused with a chapter/verse separator");
                Search("GÉN 2");
                Check(results.Items.Count == 2 && session.Prepared!.Reference == "Génesis 2:5", "Accented chapter alias failed");
                UnchangedLive();
            });

            Test("Phrase results retain their query and matches when selection synchronizes pickers", () =>
            {
                Search("marcador comun");
                Check(results.Items.Count == 3 && session.Prepared!.Reference == "Génesis 2:5", "Phrase search did not show all canonical matches");
                results.SelectedIndex = 2;
                Check((string)book.SelectedItem == "2 Tesalonicenses" && (int)chapter.SelectedItem == 2 && (int)verse.SelectedItem == 2,
                    "Selecting a phrase result did not synchronize its location");
                Check(query.Text == "marcador comun" && results.Items.Count == 3, "Picker synchronization reran or rewrote phrase search");
                verse.SelectedItem = 1;
                Check(results.Items.Count == 2 && session.Prepared!.Reference == "2 Tesalonicenses 2:1", "Explicit verse navigation did not show the selected chapter");
                Check(query.Text == "marcador comun", "Explicit navigation discarded the phrase query");
                UnchangedLive();
            });

            Test("Long chapter verse picker prepares and scrolls the last existing verse", () =>
            {
                book.SelectedItem = "Salmos";
                Check(results.Items.Count == 176 && verse.Items.Count == 176, "Chapter navigation truncated at phrase search limit");
                verse.SelectedItem = 176;
                window.UpdateLayout();
                var selected = (BibleResult)results.SelectedItem!;
                Check(selected.Reference == "Salmos 119:176" && session.Prepared!.Reference == selected.Reference, "Last verse picker selection prepared the wrong verse");
                var row = results.ItemContainerGenerator.ContainerFromItem(selected) as ListBoxItem;
                Check(row is not null && row.IsVisible && BoundsWithin(row, results).IntersectsWith(new Rect(results.RenderSize)),
                    "Selected verse was not scrolled into the visible results viewport");
                UnchangedLive();
            });

            Test("Adding a selected verse and adding its whole passage are separate explicit actions", () =>
            {
                var plan = Field<ObservableCollection<Cue>>(window, "_plan");
                var oldPlan = plan.ToArray();
                try
                {
                    book.SelectedItem = "Salmos";
                    verse.SelectedItem = 176;
                    var addSelected = (Button)window.FindName("AddSelectedBibleButton");
                    var addPassage = (Button)window.FindName("AddBiblePassageButton");
                    Check(addPassage.Visibility == Visibility.Visible && addPassage.Content?.ToString() == "Separados (176)",
                        "Whole-passage action does not identify the 176 verses that it will add");
                    addSelected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(plan.Count == oldPlan.Length + 1 && plan[^1].Reference == "Salmos 119:176",
                        "Adding the selected verse unexpectedly added the chapter or a different verse");
                    UnchangedLive();
                    addPassage.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var passage = plan.Skip(oldPlan.Length + 1).ToArray();
                    Check(plan.Count == oldPlan.Length + 177 && passage.Length == 176,
                        "Whole-passage action did not add exactly the 176 displayed verses");
                    Check(passage[0].Reference == "Salmos 119:1" && passage[^1].Reference == "Salmos 119:176",
                        "Whole passage was not appended in verse order");
                    UnchangedLive();
                    Search("marcador comun");
                    Check(addPassage.Visibility == Visibility.Collapsed, "Phrase results offer an ambiguous add-all action");
                }
                finally
                {
                    plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue);
                    Call(window, "UpdatePlanCount"); Call(window, "SaveWorkspace");
                }
            });

            Test("Invalid chapter lookup preserves prepared content and clears stale actions", () =>
            {
                var prepared = session.Prepared;
                var addPassage = (Button)window.FindName("AddBiblePassageButton");
                Search("Juan 999");
                Check(resultText.Text.Contains("capítulo", StringComparison.OrdinalIgnoreCase), "Missing chapter did not show a Spanish chapter error");
                Check(ReferenceEquals(prepared, session.Prepared), "Invalid query discarded valid prepared content");
                Check(results.Items.Count == 0 && results.SelectedItem is null, "Invalid query retained stale search results");
                Check(!addPassage.IsEnabled && !((Button)window.FindName("AddSelectedBibleButton")).IsEnabled, "Invalid query retained an enabled add action");
                UnchangedLive();
            });
        }
        finally
        {
            if (previousCatalog is not null) Call(window, "InstallBibleCatalog", previousCatalog);
            if (previousPrepared is not null) Call(window, "PrepareCue", previousPrepared, previousPreparedReading);
            query.Text = previousQuery;
            Field<System.Windows.Threading.DispatcherTimer?>(window, "_bibleSearchTimer")?.Stop();
            Call(window, "SelectContentSource", previousSource);
        }
    }
}
