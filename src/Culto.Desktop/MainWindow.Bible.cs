using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Culto.Core;

namespace Culto.Desktop;

public sealed record BibleResult(BibleVerse Verse, Cue Cue)
{
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
            SetStatus("Biblia importada. Elige un libro o escribe una cita o frase para buscar.");
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
    private void BibleQueryKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { SearchBible(); e.Handled = true; } }
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
        SetBibleResults(_bible.FindChapter($"{book} {chapter}"), referenceSearch: true, selectedVerse);
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
    }
    private void SetBibleResults(IReadOnlyList<BibleVerse> verses, bool referenceSearch, int? selectedVerse = null)
    {
        var results = verses.Select(verse => new BibleResult(verse, _bibleCuesByReference[(verse.Book, verse.Chapter, verse.Verse)])).ToList();
        _referenceSearch = referenceSearch;
        _bibleResults = results;
        BibleResults.ItemsSource = results;
        BibleResultText.Text = results.Count switch
        {
            0 => "No se encontraron versículos.",
            1 => $"1 versículo · {_bible!.Document.Name}",
            _ => $"{results.Count:N0} versículos · {_bible!.Document.Name}"
        };
        if (results.Count > 0)
            BibleResults.SelectedItem = selectedVerse.HasValue
                ? results.FirstOrDefault(result => result.Verse.Verse == selectedVerse.Value) ?? results[0]
                : results[0];
        UpdateBibleAddActions();
    }
    private void UpdateBibleAddActions()
    {
        AddSelectedBibleButton.IsEnabled = BibleResults.SelectedItem is BibleResult;
        AddBiblePassageButton.Visibility = _referenceSearch && _bibleResults.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        AddBiblePassageButton.Content = $"Pasaje ({_bibleResults.Count:N0})";
        AddBiblePassageButton.ToolTip = $"Añadir al orden los {_bibleResults.Count:N0} versículos mostrados, en el mismo orden.";
    }
    private void SearchBible()
    {
        if (_bible is null) { SetStatus("Importa primero una Biblia desde un archivo JSON o Zefania XML.", true); return; }
        var query = BibleQuery.Text.Trim();
        if (query.Length == 0) { SetStatus("Elige un libro o escribe un capítulo (Juan 3), una cita o palabras del texto."); return; }
        try
        {
            var verseReference = Regex.IsMatch(query, @"\d\s*[:.,]\s*\d|\d\s*:");
            var chapterReference = !verseReference && Regex.IsMatch(query, @"^(?=.*\p{L})[\p{L}\p{N}.\sªº]+\d$");
            var verses = verseReference ? _bible.FindReference(query)
                : chapterReference ? _bible.FindChapter(query) : _bible.Search(query);
            SetBibleResults(verses, verseReference || chapterReference);
        }
        catch (Exception ex) { BibleResultText.Text = ex.Message; SetStatus(ex.Message, true); }
    }
    private void BibleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BibleResults.SelectedItem is BibleResult result)
        {
            PrepareCue(result.Cue, _bibleReading);
            SynchronizeBiblePickers(result.Verse);
            BibleResults.ScrollIntoView(result);
        }
        UpdateBibleAddActions();
    }
    private void AddSelectedBibleToPlanClick(object sender, RoutedEventArgs e)
    {
        if (BibleResults.SelectedItem is not BibleResult selected) { SetStatus("Busca y selecciona primero un versículo."); return; }
        _plan.Add(CopyCue(selected.Cue, true));
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
