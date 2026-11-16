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
    public string Reference => $"{Verse.Book} {Verse.Chapter}:{Verse.VerseNumber}";
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
    private BibleVersionStore? _bibleVersions;
    private BibleVersionStore BibleVersions => _bibleVersions ??= new(_store.DirectoryPath);
    private bool _changingBibleVersion;
    private void LoadSavedBible()
    {
        try
        {
            var document = JsonFileStore.Load<BibleDocument>(_store.BiblePath);
            if (document is not null)
            {
                var catalog = new BibleCatalog(document);
                BibleVersions.Add(catalog);
                InstallBibleCatalog(catalog);
            }
        }
        catch (Exception ex) { SetStatus("La Biblia guardada no se pudo abrir. Importa de nuevo el archivo. " + ex.Message, true); }
        RefreshBibleVersions();
    }
    private void RefreshBibleVersions()
    {
        _changingBibleVersion = true;
        try
        {
            var versions = BibleVersions.List();
            BibleVersionPicker.ItemsSource = versions;
            BibleVersionPicker.SelectedItem = versions.FirstOrDefault(version => version.Name == _bible?.Document.Name);
            BibleVersionPicker.IsEnabled = versions.Count > 0;
            BibleVersionPicker.ToolTip = BibleVersions.Warnings.Count == 0 ? "Cambiar versión de la Biblia"
                : $"{BibleVersions.Warnings.Count} archivos de versiones no se pudieron abrir.";
        }
        finally { _changingBibleVersion = false; }
    }
    private async void BibleVersionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _changingBibleVersion || BibleVersionPicker.SelectedItem is not BibleVersion version
            || version.Name == _bible?.Document.Name) return;
        var previous = SelectedBibleResults().Select(result => result.Verse).ToArray();
        _changingBibleVersion = true;
        BibleVersionPicker.IsEnabled = false;
        try
        {
            var catalog = await Task.Run(() => BibleVersions.Load(version));
            JsonFileStore.Save(_store.BiblePath, catalog.Document);
            InstallBibleCatalog(catalog);
            if (previous.Length > 0)
            {
                try
                {
                    var matching = previous.SelectMany(verse => catalog.FindReference($"{verse.Book} {verse.Chapter}:{verse.VerseNumber}"))
                        .Distinct().ToArray();
                    if (matching.Length == 1) ShowBibleChapter(matching[0].Book, matching[0].Chapter, matching[0].Verse);
                    else SetBibleResults(matching, referenceSearch: true, selectRange: true);
                }
                catch (ArgumentException) { ClearBibleResults("Esta cita no está disponible en la versión elegida."); }
            }
            else if (!string.IsNullOrWhiteSpace(BibleQuery.Text)) SearchBible();
            SetStatus("Versión: " + catalog.Document.Name);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { SetStatus("No se pudo cambiar de Biblia: " + error.Message, true); }
        finally { _changingBibleVersion = false; RefreshBibleVersions(); }
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
        UpdateBibleSelectionSummary();
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
        var dialog = new OpenFileDialog { Title = "Agregar versiones de la Biblia", Filter = "Biblia JSON o Zefania XML|*.json;*.xml", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            SetStatus("Comprobando el texto bíblico…");
            foreach (var path in dialog.FileNames) await ImportBibleFileAsync(path);
            SetStatus(dialog.FileNames.Length == 1 ? "Biblia agregada." : $"{dialog.FileNames.Length} versiones agregadas.");
        }
        catch (Exception ex) { SetStatus("No se pudo importar la Biblia: " + ex.Message, true); }
    }
    private async Task ImportBibleFileAsync(string path)
    {
        // Validate the external source before touching the persisted or working Bible.
        var catalog = await Task.Run(() => BibleCatalog.Load(path));
        try
        {
            if (JsonFileStore.Load<BibleDocument>(_store.BiblePath) is { } previous)
                BibleVersions.Add(new BibleCatalog(previous));
        }
        catch (InvalidDataException) { _store.ArchiveDamagedFile("biblia.json"); }
        BibleVersions.Add(catalog);
        JsonFileStore.Save(_store.BiblePath, catalog.Document);
        InstallBibleCatalog(catalog);
        RefreshBibleVersions();
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
        UpdateBibleSelectionSummary();
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
        UpdateBibleSelectionSummary();
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
        ProjectPreparedFromInput();
    }
}
