using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    // These checks exercise WPF selection state and the app's routed handlers.
    // Physical Shift+click / Shift+Up/Down and mouse dragging still require a
    // manual native input pass; assigning SelectedItems does not prove gestures.
    private static void RunBibleSelectionTests(MainWindow window)
    {
        var session = Field<PresentationSession>(window, "_session");
        var oldCatalog = Field<BibleCatalog?>(window, "_bible");
        var oldPrepared = session.Prepared;
        var oldPreparedReading = Field<IReadOnlyList<Cue>?>(window, "_preparedReading");
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var oldPlan = plan.ToArray();
        var oldSource = Field<int>(window, "_activeContentSource");
        var query = (TextBox)window.FindName("BibleQuery");
        var oldQuery = query.Text;
        var results = (ListBox)window.FindName("BibleResults");
        var catalog = new BibleCatalog(new BibleDocument
        {
            Name = "Biblia sintética para selección", Attribution = "Datos de prueba",
            Verses = Enumerable.Range(1, 6).Select(number =>
                new BibleVerse("Génesis", 1, number, $"Texto sintético {number}")).ToList()
        });
        void Search(string text)
        {
            query.Text = text;
            Call(window, "SearchBible");
            window.UpdateLayout();
        }
        void Enter(UIElement source)
        {
            source.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Enter)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        }
        void SelectVerses(params int[] numbers)
        {
            results.UnselectAll();
            foreach (var number in numbers)
                results.SelectedItems.Add(results.Items.Cast<BibleResult>().Single(result => result.Verse.Verse == number));
        }
        void CheckPassage(Cue? cue, string expectedReference, params int[] verseNumbers)
        {
            Check(cue?.Kind == CueKind.Bible && cue.Reference == expectedReference,
                $"Expected grouped {expectedReference}, got {cue?.Reference}");
            Check(cue!.Body.Split('\n').Length == verseNumbers.Length, "Grouped body lost or duplicated a verse");
            foreach (var number in verseNumbers) Check(cue.Body.Contains($"Texto sintético {number}"), "Grouped body lost a selected verse");
        }
        try
        {
            Call(window, "SelectContentSource", 1);
            Call(window, "CloseOutput");
            Call(window, "InstallBibleCatalog", catalog);

            Test("Bible explicit range selects every verse and prepares one passage without projecting", () =>
            {
                var live = session.Live;
                Search("Gn 1:2-4");
                Check(results.SelectionMode == SelectionMode.Extended, "Bible results do not support extended selection");
                Check(results.SelectedItems.Cast<BibleResult>().Select(result => result.Verse.Verse).Order().SequenceEqual(new[] { 2, 3, 4 }),
                    "Typed range selected only its first verse");
                CheckPassage(session.Prepared, "Génesis 1:2-4", 2, 3, 4);
                Check(ReferenceEquals(live, session.Live) && Field<object?>(window, "_output") is null,
                    "Searching a range projected before an explicit send");
                var id = session.Prepared!.Id;
                Call(window, "PrepareBibleSelection");
                Check(session.Prepared?.Id == id, "Unchanged selection recreated its combined cue");
                query.Focus();
                query.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Down)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Check(results.SelectedItems.Count == 3, "Moving focus from search to results collapsed the range");
            });

            Test("Bible query Enter sends the complete typed range with a usable reading context", () =>
            {
                Enter(query);
                CheckPassage(session.Live, "Génesis 1:2-4", 2, 3, 4);
                Check(Field<object?>(window, "_output") is not null, "Range Enter did not create local output");
                var reading = Field<IReadOnlyList<Cue>>(window, "_liveReading");
                Check(reading.Any(cue => cue.Id == session.Live!.Id), "Combined cue is absent from its reading context");
                Check(reading.Select(cue => cue.Reference).SequenceEqual(new[] { "Génesis 1:1", "Génesis 1:2-4", "Génesis 1:5", "Génesis 1:6" }),
                    "Reading context did not replace the selected span with one passage");
                Call(window, "MoveLive", 1);
                Check(session.Live?.Reference == "Génesis 1:5", "Next repeated part of the grouped passage");
                Call(window, "MoveLive", -1);
                CheckPassage(session.Live, "Génesis 1:2-4", 2, 3, 4);
                Call(window, "MoveLive", -1);
                Check(session.Live?.Reference == "Génesis 1:1", "Previous did not reach the verse before the grouped passage");
            });

            Test("Bible multi-selection prepares one ordered cue and Enter sends all selected verses", () =>
            {
                var live = session.Live;
                Search("Génesis 1");
                Check(results.SelectedItems.Count == 1, "Chapter lookup selected an entire chapter implicitly");
                SelectVerses(4, 2, 3);
                CheckPassage(session.Prepared, "Génesis 1:2-4", 2, 3, 4);
                Check(ReferenceEquals(live, session.Live), "Extending a selection changed output");
                Enter(results);
                CheckPassage(session.Live, "Génesis 1:2-4", 2, 3, 4);
                Check(results.SelectedItems.Count == 3, "Enter collapsed the range selection");
            });

            Test("Bible add and drag insert one independent grouped cue without changing original verses or live", () =>
            {
                Search("Génesis 1");
                SelectVerses(2, 3, 4);
                var originalCues = results.Items.Cast<BibleResult>().Select(result => result.Cue).ToArray();
                var originalIds = originalCues.Select(cue => cue.Id).ToArray();
                var originalBodies = originalCues.Select(cue => cue.Body).ToArray();
                var combinedId = session.Prepared!.Id;
                var live = session.Live;
                var count = plan.Count;
                Call(window, "AddSelectedBibleToPlanClick", window, new RoutedEventArgs());
                Check(plan.Count == count + 1, "Adding selection inserted separate verse rows");
                var added = plan[^1];
                CheckPassage(added, "Génesis 1:2-4", 2, 3, 4);
                Check(added.Id != combinedId && !originalIds.Contains(added.Id), "Added passage reused source identity");
                var member = results.Items.Cast<BibleResult>().Single(result => result.Verse.Verse == 3);
                var data = Call(window, "CreateContentDragData", results, member.Cue) as IDataObject;
                Check(data is not null, "Selected passage could not create a drag payload");
                var effect = (DragDropEffects)Call(window, "ApplyContentDrop", data!, plan.Count)!;
                Check(effect == DragDropEffects.Copy && plan.Count == count + 2, "Dragging selection did not insert exactly one cue");
                var dragged = plan[^1];
                CheckPassage(dragged, "Génesis 1:2-4", 2, 3, 4);
                Check(dragged.Id != added.Id && dragged.Id != combinedId && !originalIds.Contains(dragged.Id), "Dragged passage reused an existing identity");
                Check(originalCues.Select(cue => cue.Id).SequenceEqual(originalIds)
                    && originalCues.Select(cue => cue.Body).SequenceEqual(originalBodies), "Adding or dragging mutated source verses");
                Check(ReferenceEquals(live, session.Live), "Adding or dragging a passage changed live output");
                var persisted = Field<WorkspaceStore>(window, "_store").Load().Plan.Items;
                Check(persisted.Count == plan.Count && persisted[^1].Reference == "Génesis 1:2-4", "Grouped drag was not persisted");
                Call(window, "ClearContentDrag");
            });
        }
        finally
        {
            Call(window, "ClearContentDrag");
            Call(window, "CloseOutput");
            plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue);
            Call(window, "UpdatePlanCount"); Call(window, "SaveWorkspace");
            if (oldCatalog is not null) Call(window, "InstallBibleCatalog", oldCatalog);
            query.Text = oldQuery;
            Field<DispatcherTimer?>(window, "_bibleSearchTimer")?.Stop();
            if (oldPrepared is not null) Call(window, "PrepareCue", oldPrepared, oldPreparedReading);
            Call(window, "SelectContentSource", oldSource);
        }
    }
}
