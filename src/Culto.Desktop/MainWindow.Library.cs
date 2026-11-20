using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool _refreshingLibrary;
    private void LibraryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || e.ChangedButton != MouseButton.Left) return;
        var source = e.OriginalSource as DependencyObject;
        while (source is FrameworkContentElement content) source = content.Parent;
        if (source is null || ItemsControl.ContainerFromElement(LibraryList, source) is not ListBoxItem { DataContext: Cue cue }) return;
        e.Handled = true;
        if (_session.Prepared?.Id != cue.Id) PrepareCue(cue);
        PlayFileFromInput();
    }
    private void RefreshLibrary()
    {
        if (!_ready) return;
        var query = LibrarySearch.Text.Trim();
        var filter = (LibraryFilter.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? "All";
        var sourceItems = _workspace.Library.Where(cue => cue.HymnNumber.HasValue
            && cue.Kind is CueKind.Text or CueKind.Video or CueKind.Audio).ToList();
        var numericQuery = query.Length > 0 && query.All(char.IsAsciiDigit);
        var exactHymnSearch = numericQuery && filter is "All" or "Hymns";
        var validNumber = int.TryParse(query, out var number);
        var items = sourceItems.Where(cue => query.Length == 0
            || (numericQuery && validNumber && cue.HymnNumber == number)
            || (!exactHymnSearch && cue.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        items = filter switch
        {
            "Hymns" => items.Where(c => c.HymnNumber.HasValue),
            "Audio" => items.Where(c => c.Kind == CueKind.Audio), _ => items
        };
        var selectedId = (LibraryList.SelectedItem as Cue)?.Id;
        _refreshingLibrary = true;
        try
        {
            _libraryView.Clear();
            foreach (var cue in items.OrderBy(c => c.HymnNumber ?? int.MaxValue).ThenBy(c => c.Title)) _libraryView.Add(cue);
            LibraryList.SelectedItem = _libraryView.FirstOrDefault(c => c.Id == selectedId);
        }
        finally { _refreshingLibrary = false; }
        LibraryCountText.Text = $"{_libraryView.Count:N0} de {sourceItems.Count:N0} himnos";
        ClearSearchButton.IsEnabled = query.Length > 0;
        RefreshPowerPointFiles();
        LibraryEmpty.Visibility = _libraryView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var category = (LibraryFilter.SelectedItem as ListBoxItem)?.Content?.ToString() ?? "Todo";
        LibraryEmpty.Text = sourceItems.Count == 0
            ? "Himnario vacío"
            : query.Length > 0
                ? $"Sin resultados para «{query}» en {category}."
                : $"Sin contenido en {category}.";
    }
    private void LibrarySearchChanged(object sender, TextChangedEventArgs e)
    {
        CancelSearchReset(LibrarySearch);
        RefreshLibrary();
    }
    private void LibrarySearchKeyDown(object sender, KeyEventArgs e)
    {
        if (!_ready || e.Key != Key.Enter) return;
        e.Handled = true;
        var query = LibrarySearch.Text.Trim();
        var cue = _libraryView.FirstOrDefault(item => item.Title.Equals(query, StringComparison.CurrentCultureIgnoreCase))
            ?? _libraryView.FirstOrDefault();
        if (cue is null) { SetStatus("Sin resultados."); return; }
        var alreadySelected = ReferenceEquals(LibraryList.SelectedItem, cue);
        LibraryList.SelectedItem = cue;
        if (alreadySelected) PrepareCue(cue);
        LibraryList.ScrollIntoView(cue);
        LibraryList.UpdateLayout();
        LibraryList.Focus();
        if (LibraryList.ItemContainerGenerator.ContainerFromItem(cue) is ListBoxItem row) row.Focus();
    }
    private void LibraryFilterChanged(object sender, SelectionChangedEventArgs e) => RefreshLibrary();
    private void ClearLibrarySearchClick(object sender, RoutedEventArgs e)
    {
        LibrarySearch.Clear();
        LibrarySearch.Focus();
    }
    private void LibrarySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _refreshingLibrary || LibraryList.SelectedItem is not Cue cue) return;
        // Loading native media during mouse-down can swallow a quick drag gesture.
        // The release handler prepares a click; a drop prepares its new order copy.
        if (_contentDragSource == LibraryList && _contentDragCue is not null && Mouse.LeftButton == MouseButtonState.Pressed) return;
        PrepareCue(cue);
    }

    private async void ImportFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Importar contenido", Multiselect = true,
            Filter = MediaLibrary.FileFilter };
        if (dialog.ShowDialog(this) == true) await ImportMediaAsync(dialog.FileNames);
    }
    private async void ImportFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Elegir carpeta del himnario", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            SetStatus("Buscando archivos del himnario…");
            var files = await Task.Run(() => Directory.EnumerateFiles(dialog.FolderName, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(MediaLibrary.IsSupported).ToArray());
            await ImportMediaAsync(files);
        }
        catch (Exception ex) { SetStatus("No se pudo leer la carpeta: " + ex.Message, true); }
    }
    private async Task ImportMediaAsync(IEnumerable<string> paths)
    {
        var existing = _workspace.Library.Where(c => c.FilePath is not null).Select(c => c.FilePath!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = await Task.Run(() =>
        {
            var added = new List<Cue>(); var failed = 0;
            foreach (var path in paths)
            {
                if (existing.Contains(Path.GetFullPath(path))) continue;
                try { var cue = MediaLibrary.FromFile(path); added.Add(cue); existing.Add(Path.GetFullPath(path)); }
                catch (Exception) { failed++; }
            }
            return (added, failed);
        });
        _workspace.Library.AddRange(result.added);
        RefreshLibrary(); SaveWorkspace();
        SetStatus($"{result.added.Count} archivos incorporados" + (result.failed > 0 ? $"; {result.failed} no se pudieron leer." : "."), result.failed > 0);
        if (result.added.Count > 0) LibraryList.SelectedItem = result.added[0];
    }
    private static Cue CopyCue(Cue cue, bool newId = false) => cue.Copy(newId);
    private void AddToPlan(Cue cue)
    {
        var item = CopyCue(cue, true);
        _plan.Add(item); UpdatePlanCount(); SaveWorkspace();
        PlanList.SelectedItem = item; PlanList.ScrollIntoView(item);
        SetStatus($"Añadido al orden: {item.Title}.");
    }
    private void AddLibraryToPlanClick(object sender, RoutedEventArgs e)
    { if (LibraryList.SelectedItem is Cue cue) AddToPlan(cue); else SetStatus("Selecciona primero un contenido de la biblioteca."); }
    private void EditLibraryClick(object sender, RoutedEventArgs e)
    {
        if (LibraryList.SelectedItem is not Cue cue) { SetStatus("Selecciona un elemento para editarlo."); return; }
        if (cue.Kind == CueKind.BrowserSource)
        {
            var browser = new BrowserSourceDialog(cue) { Owner = this };
            if (browser.ShowDialog() != true || browser.SelectedCue is not { } edited) return;
            cue.Title = edited.Title; cue.Browser = edited.Browser;
            RefreshLibrary(); SaveWorkspace(); PrepareCue(cue); return;
        }
        if (cue.Kind == CueKind.WindowCapture)
        {
            var capture = new WindowCaptureDialog(cue, _workspace.YouTubeLayout) { Owner = this };
            if (capture.ShowDialog() != true || capture.SelectedCue is not { } edited) return;
            var previous = CopyCue(cue);
            var previousLayout = _workspace.YouTubeLayout;
            cue.Title = edited.Title; cue.Capture = edited.Capture;
            RememberYouTubeLayout(cue);
            RefreshLibrary();
            if (!SaveWorkspace())
            {
                cue.Title = previous.Title; cue.Capture = previous.Capture;
                _workspace.YouTubeLayout = previousLayout; RefreshLibrary(); return;
            }
            PrepareCue(cue); return;
        }
        var dialog = new CueEditDialog(cue) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        cue.Title = dialog.CueTitle; cue.HymnNumber = dialog.HymnNumber;
        if (cue.Kind == CueKind.Text) cue.Body = dialog.Body;
        RefreshLibrary(); SaveWorkspace(); PrepareCue(cue);
    }
    private void AddOrderTextClick(object sender, RoutedEventArgs e)
    {
        var dialog = new CueEditDialog(new Cue { Kind = CueKind.Text, Title = "Texto" }) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Body)) return;
        AddToPlan(new Cue { Kind = CueKind.Text, Title = dialog.CueTitle, Body = dialog.Body });
    }
    private void MoveUpClick(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDownClick(object sender, RoutedEventArgs e) => MoveSelected(1);
    private void MoveSelected(int delta)
    {
        var i = PlanList.SelectedIndex; var target = i + delta;
        if (i < 0 || target < 0 || target >= _plan.Count) return;
        MovePlanItem(i, target); SaveWorkspace();
    }
    private void RemoveCueClick(object sender, RoutedEventArgs e)
    {
        if (PlanList.SelectedItem is not Cue cue) return;
        _plan.Remove(cue); UpdatePlanCount(); SaveWorkspace();
        SetStatus("Elemento retirado del orden.");
    }
    private void NewPlanClick(object sender, RoutedEventArgs e)
    {
        var name = PromptDialog.Ask(this, "Nuevo culto", "Nombre del culto", "Culto del sábado");
        if (name is null) return;
        SaveWorkspace();
        try { _store.Archive(_workspace.Plan); }
        catch (Exception ex) { SetStatus("No se pudo guardar el culto anterior: " + ex.Message, true); return; }
        _plan.Clear(); PlanNameBox.Text = name; UpdatePlanCount(); SaveWorkspace();
        SetStatus("Nuevo orden creado.");
    }
    private void OpenPlanClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Abrir orden de culto", Filter = "Culto Studio|*.culto.json;*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var plan = JsonFileStore.Load<ServicePlan>(dialog.FileName);
            if (plan?.Items is null || plan.Items.Count > 10000 || plan.Items.Any(c => c is null || !Enum.IsDefined(c.Kind))) throw new InvalidDataException("El archivo no contiene un orden válido.");
            // IDs are refreshed so a loaded plan cannot accidentally match the current live item.
            SaveWorkspace(); _store.Archive(_workspace.Plan);
            _plan.Clear(); foreach (var cue in plan.Items) _plan.Add(CopyCue(cue, true));
            PlanNameBox.Text = plan.Name; UpdatePlanCount(); SaveWorkspace();
            SetStatus("Culto abierto.");
        }
        catch (Exception ex) { SetStatus("No se pudo abrir el culto: " + ex.Message, true); }
    }
    private void SavePlanClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Guardar una copia del culto", Filter = "Culto Studio|*.culto.json", FileName = "Mi culto.culto.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { ExportPlan(dialog.FileName); }
        catch (Exception ex) { SetStatus("No se pudo guardar la copia: " + ex.Message, true); }
    }
    private void ExportPlan(string path)
    {
        SaveWorkspace();
        JsonFileStore.Save(path, _workspace.Plan);
        SetStatus("Copia del orden guardada.");
    }

    private async void CaptureClick(object sender, RoutedEventArgs e)
    {
        var dialog = new DisplayDialog(DisplayService.GetDisplays(), null, capture: true) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedDisplay is null) return;
        var state = WindowState;
        try
        {
            WindowState = WindowState.Minimized;
            await Task.Delay(1500);
            var path = Path.Combine(_store.DirectoryPath, "Capturas", $"captura-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            var saved = CaptureService.CaptureDisplay(dialog.SelectedDisplay, path);
            WindowState = state; Activate();
            await ImportMediaAsync([saved]);
            SetStatus("Captura preparada.");
        }
        catch (Exception ex) { SetStatus("No se pudo capturar la pantalla: " + ex.Message, true); }
        finally { WindowState = state; Activate(); }
    }
}
