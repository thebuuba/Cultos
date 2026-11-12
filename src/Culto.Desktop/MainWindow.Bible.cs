using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Culto.Core;

namespace Culto.Desktop;

public sealed record BibleResult(BibleVerse Verse, Cue Cue)
{
    public bool ShowReference { get; init; }
    public string Reference => $"{Verse.Book} {Verse.Chapter}:{Verse.Verse}";
    public string Text => Verse.Text;
}

public partial class MainWindow
{
    private List<BibleResult> _bibleResults = [];
    private IReadOnlyList<Cue> _bibleReading = [];
    private Dictionary<(string Book, int Chapter, int Verse), Cue> _bibleCuesByReference = [];
    private bool _referenceSearch;
    private bool _updatingBiblePickers;
    private DispatcherTimer? _bibleSearchTimer;
    private void LoadSavedBible()
    {
        try
        {
            var document = JsonFileStore.Load<BibleDocument>(_store.BiblePath);
            if (document is not null) InstallBibleCatalog(new BibleCatalog(document));
        }
        catch (Exception ex) { SetStatus("La Biblia guardada no se pudo abrir. Importa de nuevo el archivo. " + ex.Message, true); }
    }
    private void InstallBibleCatalog(BibleCatalog catalog)
    {
        _bibleSearchTimer?.Stop();
        var reading = catalog.Document.Verses.Select(catalog.ToCue).ToArray();
        var cuesByReference = catalog.Document.Verses.Select((verse, index) => (verse, index))
            .ToDictionary(item => (item.verse.Book, item.verse.Chapter, item.verse.Verse), item => reading[item.index]);
        _bible = catalog;
        // A live reading keeps its original array when a different Bible is imported.
        _bibleReading = reading;
        _bibleCuesByReference = cuesByReference;
        _bibleResults = [];
        _referenceSearch = false;
        BibleResults.ItemsSource = null;
        BibleResultText.Text = "";
        UpdateBibleAddActions();
        _updatingBiblePickers = true;
        try
        {
            BibleBookPicker.ItemsSource = catalog.Document.Verses.Select(verse => verse.Book).Distinct().ToArray();
            BibleBookPicker.SelectedIndex = -1;
            BibleChapterPicker.ItemsSource = Array.Empty<int>();
            BibleVersePicker.ItemsSource = Array.Empty<int>();
            BibleBookPicker.IsEnabled = true;
            BibleChapterPicker.IsEnabled = false;
            BibleVersePicker.IsEnabled = false;
        }
        finally { _updatingBiblePickers = false; }
        var firstVerse = catalog.Document.Verses[0];
        SetBibleResults(catalog.FindChapter($"{firstVerse.Book} {firstVerse.Chapter}"), referenceSearch: true, selectFirst: false);
        SynchronizeBiblePickers(firstVerse);
        UpdateBibleStatus();
    }
    private void UpdateBibleStatus()
    {
        if (_bible is null) return;
        BibleNameText.Text = _bible.Document.Name;
        BibleStatusText.Text = _bible.Document.Verses.Count == 1
            ? "1 versículo disponible sin internet."
            : $"{_bible.Document.Verses.Count:N0} versículos disponibles sin internet.";
        BibleStatusText.ToolTip = _bible.Document.Attribution;
    }
    private async void ImportBibleClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Importar una Biblia autorizada", Filter = "Biblia JSON o Zefania XML|*.json;*.xml" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            SetStatus("Comprobando el texto bíblico…");
            await ImportBibleFileAsync(dialog.FileName);
            SetStatus("Biblia importada.");
        }
        catch (Exception ex) { SetStatus("No se pudo importar la Biblia: " + ex.Message, true); }
    }
    private async Task ImportBibleFileAsync(string path)
    {
        // Validate the external source before touching the persisted or working Bible.
        var catalog = await Task.Run(() => BibleCatalog.Load(path));
        try { JsonFileStore.Load<BibleDocument>(_store.BiblePath); }
        catch (InvalidDataException) { _store.ArchiveDamagedFile("biblia.json"); }
        JsonFileStore.Save(_store.BiblePath, catalog.Document);
        InstallBibleCatalog(catalog);
    }
    private void BibleQueryChanged(object sender, TextChangedEventArgs e)
    {
        if (_bible is null) return;
        _bibleSearchTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => SearchBible(), Dispatcher);
        _bibleSearchTimer.Stop();
        ClearBibleResults(BibleQuery.Text.Trim().Length == 0 ? "Escribe una cita o palabras del texto." : "Buscando…");
        if (!string.IsNullOrWhiteSpace(BibleQuery.Text)) _bibleSearchTimer.Start();
    }
    private void BibleQueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!_ready || e.IsRepeat) return;
            SearchBible();
            ProjectBibleResult(BibleResults.SelectedItem as BibleResult);
            SelectBibleQueryBook();
        }
        else if (e.Key == Key.Escape) { ClearBibleQueryClick(sender, e); e.Handled = true; }
        else if (e.Key == Key.Down)
        {
            if (_bibleSearchTimer?.IsEnabled == true) SearchBible();
            if (BibleResults.Items.Count > 0)
            {
                if (BibleResults.SelectedItem is { } selected) BibleResults.ScrollIntoView(selected);
                BibleResults.UpdateLayout();
                // Focusing the list before its row would make WPF treat this as
                // a new keyboard selection and collapse an already selected range.
                if (BibleResults.ItemContainerGenerator.ContainerFromItem(BibleResults.SelectedItem) is ListBoxItem row) row.Focus();
                else BibleResults.Focus();
            }
            e.Handled = true;
        }
    }
    private void ClearBibleQueryClick(object sender, RoutedEventArgs e)
    {
        _bibleSearchTimer?.Stop();
        BibleQuery.Clear();
        ClearBibleResults("Escribe una cita o palabras del texto.");
        BibleQuery.Focus();
    }
    private void SearchBibleClick(object sender, RoutedEventArgs e) => SearchBible();
    private void BibleBookChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingBiblePickers || _bible is null || BibleBookPicker.SelectedItem is not string book) return;
        var firstChapter = _bible.Document.Verses.First(verse => verse.Book == book).Chapter;
        ShowBibleChapter(book, firstChapter);
    }
    private void BibleChapterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingBiblePickers || BibleBookPicker.SelectedItem is not string book || BibleChapterPicker.SelectedItem is not int chapter) return;
        ShowBibleChapter(book, chapter);
    }
    private void BibleVerseChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingBiblePickers || BibleBookPicker.SelectedItem is not string book
            || BibleChapterPicker.SelectedItem is not int chapter || BibleVersePicker.SelectedItem is not int verse) return;
        ShowBibleChapter(book, chapter, verse);
    }
    private void ShowBibleChapter(string book, int chapter, int? selectedVerse = null)
    {
        if (_bible is null) return;
        _bibleSearchTimer?.Stop();
        SetBibleResults(_bible.FindChapter($"{book} {chapter}"), referenceSearch: true, selectedVerse);
    }
    private void PreviousBibleChapterClick(object sender, RoutedEventArgs e) => MoveBibleChapter(-1);
    private void NextBibleChapterClick(object sender, RoutedEventArgs e) => MoveBibleChapter(1);
    private void MoveBibleChapter(int direction)
    {
        var index = BibleChapterPicker.SelectedIndex + direction;
        if (index >= 0 && index < BibleChapterPicker.Items.Count) BibleChapterPicker.SelectedIndex = index;
    }
    private void SynchronizeBiblePickers(BibleVerse verse)
    {
        if (_bible is null) return;
        _updatingBiblePickers = true;
        try
        {
            BibleBookPicker.SelectedItem = verse.Book;
            var chapters = _bible.Document.Verses.Where(item => item.Book == verse.Book).Select(item => item.Chapter).Distinct().ToArray();
            if (!BibleChapterPicker.Items.Cast<int>().SequenceEqual(chapters)) BibleChapterPicker.ItemsSource = chapters;
            BibleChapterPicker.SelectedItem = verse.Chapter;
            BibleChapterPicker.IsEnabled = true;
            var verses = _bible.FindChapter($"{verse.Book} {verse.Chapter}").Select(item => item.Verse).ToArray();
            if (!BibleVersePicker.Items.Cast<int>().SequenceEqual(verses)) BibleVersePicker.ItemsSource = verses;
            BibleVersePicker.SelectedItem = verse.Verse;
            BibleVersePicker.IsEnabled = true;
        }
        finally { _updatingBiblePickers = false; }
        PreviousBibleChapterButton.IsEnabled = BibleChapterPicker.SelectedIndex > 0;
        NextBibleChapterButton.IsEnabled = BibleChapterPicker.SelectedIndex >= 0
            && BibleChapterPicker.SelectedIndex < BibleChapterPicker.Items.Count - 1;
    }
    private void SetBibleResults(IReadOnlyList<BibleVerse> verses, bool referenceSearch, int? selectedVerse = null, bool selectFirst = true, bool selectRange = false)
    {
        var results = verses.Select(verse => new BibleResult(verse, _bibleCuesByReference[(verse.Book, verse.Chapter, verse.Verse)])
            { ShowReference = !string.IsNullOrWhiteSpace(BibleQuery.Text) }).ToList();
        _referenceSearch = referenceSearch;
        _bibleResults = results;
        _updatingBibleSelection = true;
        try
        {
            BibleResults.ItemsSource = results;
            if (results.Count > 0 && selectFirst)
            {
                if (selectRange)
                    foreach (var result in results) BibleResults.SelectedItems.Add(result);
                else BibleResults.SelectedItem = selectedVerse.HasValue
                    ? results.FirstOrDefault(result => result.Verse.Verse == selectedVerse.Value) ?? results[0]
                    : results[0];
            }
        }
        finally { _updatingBibleSelection = false; }
        BibleResultText.Text = results.Count switch
        {
            0 => "No se encontraron versículos.",
            1 => $"1 versículo · {_bible!.Document.Name}",
            _ => $"{results.Count:N0} versículos · {_bible!.Document.Name}"
        };
        if (BibleResults.SelectedItem is BibleResult first)
        {
            PrepareBibleSelection();
            SynchronizeBiblePickers(first.Verse);
            BibleResults.ScrollIntoView(first);
        }
        UpdateBibleAddActions();
    }
    private void UpdateBibleAddActions()
    {
        AddSelectedBibleButton.IsEnabled = BibleResults.SelectedItem is BibleResult;
        AddBiblePassageButton.IsEnabled = BibleResults.SelectedItem is BibleResult && _referenceSearch && _bibleResults.Count > 1;
        AddBiblePassageButton.Visibility = _referenceSearch && _bibleResults.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        AddBiblePassageButton.Content = $"Separados ({_bibleResults.Count:N0})";
        AddBiblePassageButton.ToolTip = $"Añadir los {_bibleResults.Count:N0} versículos mostrados como elementos separados del orden.";
        UpdateBibleSelectionSummary();
    }
    private void SearchBible()
    {
        _bibleSearchTimer?.Stop();
        if (_bible is null) { SetStatus("Importa primero una Biblia desde un archivo JSON o Zefania XML.", true); return; }
        var query = BibleQuery.Text.Trim();
        if (query.Length == 0) { ClearBibleResults("Escribe una cita o palabras del texto."); return; }
        try
        {
            var verses = _bible.Lookup(query, out var referenceSearch);
            var selectRange = referenceSearch && BibleReferenceInput.TryParse(query, out var reference) && reference.HasVerseRange;
            SetBibleResults(verses, referenceSearch, selectRange: selectRange);
        }
        catch (ArgumentException ex) { ClearBibleResults(ex.Message.Split('\n')[0]); }
    }
    private void ClearBibleResults(string message)
    {
        _referenceSearch = false;
        _bibleResults = [];
        BibleResults.ItemsSource = _bibleResults;
        BibleResultText.Text = message;
        UpdateBibleAddActions();
    }
    private void BibleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingBibleSelection) return;
        if (BibleResults.SelectedItem is BibleResult result)
        {
            PrepareBibleSelection();
            SynchronizeBiblePickers(result.Verse);
            // WPF keeps the keyboard's moving end visible during Shift selection.
            if (BibleResults.SelectedItems.Count == 1) BibleResults.ScrollIntoView(result);
        }
        UpdateBibleAddActions();
    }
    private void BibleResultsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || e.Timestamp == _bibleProjectionClickTimestamp || e.ChangedButton != MouseButton.Left
            || ContentRowAt(BibleResults, e.OriginalSource)?.DataContext is not BibleResult result) return;
        e.Handled = true;
        ProjectBibleResult(result);
    }
    private void BibleResultsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (!_ready || e.IsRepeat) return;
        ProjectBibleResult(BibleResults.SelectedItem as BibleResult);
    }
    private void ProjectBibleResult(BibleResult? result)
    {
        // A cleared/invalid search must never send the previously prepared cue.
        if (!_ready || _bible is null || result is null || !_bibleResults.Contains(result)
            || !BibleResults.Items.Contains(result)) return;
        if (!BibleResults.SelectedItems.Contains(result)) BibleResults.SelectedItem = result;
        PrepareBibleSelection();
        ProjectPrepared();
    }
    private void AddSelectedBibleToPlanClick(object sender, RoutedEventArgs e)
    {
        if (SelectedBibleCue() is not Cue selected) { SetStatus("Busca y selecciona primero un versículo."); return; }
        _plan.Add(CopyCue(selected, true));
        UpdatePlanCount(); SaveWorkspace();
        SetStatus($"Añadido al orden: {selected.Reference}.");
    }
    private void AddBibleToPlanClick(object sender, RoutedEventArgs e)
    {
        if (BibleResults.SelectedItem is not BibleResult selected) { SetStatus("Busca y selecciona primero un pasaje."); return; }
        var results = _referenceSearch ? _bibleResults : [selected];
        foreach (var item in results) _plan.Add(CopyCue(item.Cue, true));
        UpdatePlanCount(); SaveWorkspace();
        SetStatus(results.Count == 1 ? "Lectura añadida al orden: 1 versículo." : $"Lectura añadida al orden: {results.Count} versículos.");
    }
}
