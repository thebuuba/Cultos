using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunBibleSearchTests(MainWindow window, string directory)
    {
        var session = Field<PresentationSession>(window, "_session");
        var catalog = Field<BibleCatalog?>(window, "_bible");
        var prepared = session.Prepared;
        var reading = Field<IReadOnlyList<Cue>?>(window, "_preparedReading");
        var live = session.Live;
        var oldSource = Field<int>(window, "_activeContentSource");
        var query = (TextBox)window.FindName("BibleQuery");
        var oldQuery = query.Text;
        var results = (ListBox)window.FindName("BibleResults");
        var synthetic = new BibleCatalog(new BibleDocument
        {
            Name = "Búsqueda sintética", Verses =
            [
                new("Génesis", 1, 1, "Inicio sintético"),
                new("Juan", 3, 16, "Amor sintético"),
                new("Juan", 3, 17, "Amor compartido sintético"),
                new("Juan", 4, 1, "Otro capítulo sintético")
            ]
        });
        void SendKey(Key key)
        {
            window.Activate();
            window.UpdateLayout();
            query.Focus();
            Until(() => query.IsKeyboardFocusWithin, "Search field did not receive keyboard focus");
            query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, key)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        }
        try
        {
            Call(window, "SelectContentSource", 1);
            Call(window, "InstallBibleCatalog", synthetic);
            Test("Bible opens with a chapter without replacing prepared or live", () =>
            {
                Check(results.Items.Count == 1 && ((BibleResult)results.Items[0]).Reference == "Génesis 1:1", "Initial Bible page is empty");
                Check(ReferenceEquals(prepared, session.Prepared) && ReferenceEquals(live, session.Live), "Initial chapter changed prepared or live");
            });
            Test("Typing debounces Bible search and Enter resolves and projects the citation", () =>
            {
                query.Text = "Jn 3 16";
                Check(results.Items.Count == 0, "Typing retained actionable stale results");
                SendKey(Key.Enter);
                Check(results.Items.Count == 1 && session.Prepared?.Reference == "Juan 3:16", "Enter did not resolve the citation immediately");
                Check(session.Live?.Reference == "Juan 3:16" && Field<object?>(window, "_output") is not null,
                    "Enter only prepared the verse instead of projecting it locally");
                live = session.Live;
                query.Text = "amor";
                PumpFor(100);
                Check(results.Items.Count == 0, "Search did not debounce input");
                PumpFor(250);
                Check(results.Items.Count == 2, "Debounced phrase search did not show matches");
                results.ScrollIntoView(results.Items[0]); results.UpdateLayout();
                var row = (ListBoxItem)results.ItemContainerGenerator.ContainerFromIndex(0);
                Check(Descendants(row).OfType<TextBlock>().Any(text => text.IsVisible && text.Text == "Juan 3:16"),
                    "Phrase search lost the full visible Bible reference");
                Check(ReferenceEquals(live, session.Live), "Typing projected a Bible result");
            });
            Test("Editable book picker resolves a complete citation without projecting", () =>
            {
                var book = (ComboBox)window.FindName("BibleBookPicker");
                var previousLive = session.Live;
                book.Text = "Jn 3 17";
                book.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(session.Prepared?.Reference == "Juan 3:17", "Book picker ignored chapter and verse");
                Check(((ComboBox)window.FindName("BibleChapterPicker")).SelectedItem is 3
                    && ((ComboBox)window.FindName("BibleVersePicker")).SelectedItem is 17, "Book picker did not synchronize citation");
                Check(ReferenceEquals(previousLive, session.Live), "Book entry projected without an explicit send");
                book.Text = "Juan 3 16-17";
                Call(window, "ResolveBibleBookText");
                Check(results.SelectedItems.Count == 2 && session.Prepared?.Reference == "Juan 3:16-17", "Book entry lost a verse range");
                book.Text = "Juan 99 99";
                Call(window, "ResolveBibleBookText");
                Check(results.Items.Count == 0 && ReferenceEquals(previousLive, session.Live), "Invalid book entry retained stale results or projected");
            });
            Test("Incomplete Bible citations clear stale results and Escape clears the query", () =>
            {
                var lastPrepared = session.Prepared;
                query.Text = "Juan 3:";
                SendKey(Key.Enter);
                Check(results.Items.Count == 0 && window.FindName("AddSelectedBibleButton") is null
                    && window.FindName("AddBiblePassageButton") is null, "Invalid citation retained an actionable result");
                Check(ReferenceEquals(lastPrepared, session.Prepared), "Invalid citation replaced prepared content");
                SendKey(Key.Escape);
                Check(query.Text.Length == 0 && results.Items.Count == 0, "Escape did not clear the query and results");
                PumpFor(300);
                Check(results.Items.Count == 0, "Pending search repopulated cleared results");
                Check(ReferenceEquals(live, session.Live), "Clearing search changed live");
            });
            Test("Bible keyboard navigation focuses results and chapter buttons respect available boundaries", () =>
            {
                query.Text = "Juan 3";
                SendKey(Key.Down);
                window.UpdateLayout();
                Until(() => results.IsKeyboardFocusWithin && results.Items.Count == 2, $"Down did not search and move into results; count={results.Items.Count}, focused={Keyboard.FocusedElement}");
                var previous = (Button)window.FindName("PreviousBibleChapterButton");
                var next = (Button)window.FindName("NextBibleChapterButton");
                Check(!previous.IsEnabled && next.IsEnabled, "Chapter boundaries include unavailable chapters");
                next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.Prepared?.Reference == "Juan 4:1" && previous.IsEnabled && !next.IsEnabled, "Next chapter did not reach the last available chapter");
                previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(session.Prepared?.Reference == "Juan 3:16", "Previous chapter did not return to the first available verse");
                Check(ReferenceEquals(live, session.Live), "Chapter navigation changed live");
            });
            Test("Bible result double click projects its row even when another source is prepared", () =>
            {
                query.Text = "Juan 3";
                Call(window, "SearchBible");
                var result = (BibleResult)results.Items[1];
                var oldLive = session.Live;
                results.SelectedItem = result;
                Check(ReferenceEquals(oldLive, session.Live), "Single selection projected instead of preparing");
                Call(window, "PrepareCue", new Cue { Title = "Otro contenido", Body = "No proyectar" }, null);
                results.ScrollIntoView(result); window.UpdateLayout();
                var row = (ListBoxItem)results.ItemContainerGenerator.ContainerFromItem(result);
                results.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = row });
                Check(session.Live?.Reference == "Juan 3:17", "Double click projected a different prepared source");
                Check(ReferenceEquals(Field<IReadOnlyList<Cue>>(window, "_liveReading"), Field<IReadOnlyList<Cue>>(window, "_bibleReading")),
                    "Bible projection lost the reading used by Next/Previous");
                live = session.Live;
                results.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = Control.MouseDoubleClickEvent });
                results.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
                    { RoutedEvent = Control.MouseDoubleClickEvent, Source = row });
                Check(ReferenceEquals(live, session.Live), "Blank space or right double click projected content");
            });
            Test("Enter in Bible results projects the selected verse and ignores an empty selection", () =>
            {
                results.SelectedIndex = 0;
                Call(window, "PrepareCue", new Cue { Title = "Otra fuente" }, null);
                results.Focus(); window.UpdateLayout();
                results.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(session.Live?.Reference == "Juan 3:16", "Enter in results projected the wrong cue");
                live = session.Live;
                query.Text = "Juan 3:";
                SendKey(Key.Enter);
                results.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(ReferenceEquals(live, session.Live), "An invalid or empty Bible result projected stale content");
            });
        }
        finally
        {
            if (catalog is not null) Call(window, "InstallBibleCatalog", catalog);
            query.Text = oldQuery;
            Field<DispatcherTimer?>(window, "_bibleSearchTimer")?.Stop();
            if (prepared is not null) Call(window, "PrepareCue", prepared, reading);
            Call(window, "SelectContentSource", oldSource);
        }
    }
}
