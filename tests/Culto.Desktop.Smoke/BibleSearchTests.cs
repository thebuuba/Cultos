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
        var tabs = (TabControl)window.FindName("ContentTabs");
        var oldTab = tabs.SelectedIndex;
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
            tabs.SelectedIndex = 1;
            Call(window, "InstallBibleCatalog", synthetic);
            Test("Bible opens with a chapter without replacing prepared or live", () =>
            {
                Check(results.Items.Count == 1 && ((BibleResult)results.Items[0]).Reference == "Génesis 1:1", "Initial Bible page is empty");
                Check(ReferenceEquals(prepared, session.Prepared) && ReferenceEquals(live, session.Live), "Initial chapter changed prepared or live");
            });
            Test("Typing debounces Bible search and Enter immediately resolves flexible citations", () =>
            {
                query.Text = "Jn 3 16";
                Check(results.Items.Count == 0, "Typing retained actionable stale results");
                SendKey(Key.Enter);
                Check(results.Items.Count == 1 && session.Prepared?.Reference == "Juan 3:16", "Enter did not resolve the citation immediately");
                query.Text = "amor";
                PumpFor(100);
                Check(results.Items.Count == 0, "Search did not debounce input");
                PumpFor(250);
                Check(results.Items.Count == 2, "Debounced phrase search did not show matches");
                Check(ReferenceEquals(live, session.Live), "Typing projected a Bible result");
            });
            Test("Incomplete Bible citations clear stale results and Escape clears the query", () =>
            {
                var lastPrepared = session.Prepared;
                query.Text = "Juan 3:";
                SendKey(Key.Enter);
                Check(results.Items.Count == 0 && !((Button)window.FindName("AddSelectedBibleButton")).IsEnabled
                    && !((Button)window.FindName("AddBiblePassageButton")).IsEnabled, "Invalid citation retained an actionable result");
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
        }
        finally
        {
            if (catalog is not null) Call(window, "InstallBibleCatalog", catalog);
            query.Text = oldQuery;
            Field<DispatcherTimer?>(window, "_bibleSearchTimer")?.Stop();
            if (prepared is not null) Call(window, "PrepareCue", prepared, reading);
            tabs.SelectedIndex = oldTab;
        }
    }
}
