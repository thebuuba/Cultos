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
        UpdateBibleStatus();
    }
    private void UpdateBibleStatus()
    {
        if (_bible is null) return;
        BibleNameText.Text = _bible.Document.Name;
        BibleStatusText.Text = $"{_bible.Document.Verses.Count:N0} versículos disponibles sin internet.";
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
            SetStatus("Biblia importada. Escribe una cita o una frase para buscar.");
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
    private void SearchBible()
    {
        if (_bible is null) { SetStatus("Importa primero una Biblia desde un archivo JSON o Zefania XML.", true); return; }
        var query = BibleQuery.Text.Trim();
        if (query.Length == 0) { SetStatus("Escribe una cita, por ejemplo Juan 3:16, o palabras del texto."); return; }
        try
        {
            var referenceSearch = Regex.IsMatch(query, @"\d\s*[:.,]\s*\d");
            var verses = referenceSearch ? _bible.FindReference(query) : _bible.Search(query);
            var results = verses.Select(v => new BibleResult(v, _bibleCuesByReference[(v.Book, v.Chapter, v.Verse)])).ToList();
            _referenceSearch = referenceSearch;
            _bibleResults = results;
            BibleResults.ItemsSource = _bibleResults;
            BibleResultText.Text = _bibleResults.Count == 0 ? "No se encontraron versículos." : $"{_bibleResults.Count} versículos · {_bible.Document.Name}";
            if (_bibleResults.Count > 0) BibleResults.SelectedIndex = 0;
        }
        catch (Exception ex) { BibleResultText.Text = ex.Message; SetStatus(ex.Message, true); }
    }
    private void BibleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BibleResults.SelectedItem is BibleResult result)
            PrepareCue(result.Cue, _bibleReading);
    }
    private void AddBibleToPlanClick(object sender, RoutedEventArgs e)
    {
        if (BibleResults.SelectedItem is not BibleResult selected) { SetStatus("Busca y selecciona primero un pasaje."); return; }
        var results = _referenceSearch ? _bibleResults : [selected];
        foreach (var item in results) _plan.Add(CopyCue(item.Cue, true));
        UpdatePlanCount(); SaveWorkspace();
        SetStatus($"Lectura añadida al orden: {results.Count} versículos.");
    }
}
