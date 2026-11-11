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
    private void RefreshLibrary()
    {
        if (!_ready) return;
        var query = LibrarySearch.Text.Trim();
        var filter = LibraryFilter.SelectedIndex;
        var items = _workspace.Library.Where(cue => string.IsNullOrWhiteSpace(query)
            || cue.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || (int.TryParse(query, out var number) && cue.HymnNumber == number));
        items = filter switch
        {
            1 => items.Where(c => c.HymnNumber.HasValue), 2 => items.Where(c => c.Kind == CueKind.Video),
            3 => items.Where(c => c.Kind == CueKind.Image), 4 => items.Where(c => c.Kind == CueKind.Audio),
            5 => items.Where(c => c.Kind == CueKind.Text), _ => items
        };
        _libraryView.Clear();
        foreach (var cue in items.OrderBy(c => c.HymnNumber ?? int.MaxValue).ThenBy(c => c.Title)) _libraryView.Add(cue);
        LibraryCountText.Text = $"{_libraryView.Count:N0} de {_workspace.Library.Count:N0} contenidos";
        ClearSearchButton.IsEnabled = query.Length > 0;
        LibraryEmpty.Visibility = _libraryView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var category = (LibraryFilter.SelectedItem as ListBoxItem)?.Content?.ToString() ?? "Todo";
        LibraryEmpty.Text = _workspace.Library.Count == 0
            ? "Tu biblioteca empieza aquí.\n\nImporta videos MKV, imágenes o pistas de audio."
            : query.Length > 0
                ? $"Sin resultados para «{query}» en {category}.\n\nPrueba otro título, número o categoría."
                : $"Todavía no hay contenido en {category}.\n\nImporta archivos o elige otra categoría.";
    }
    private void LibrarySearchChanged(object sender, TextChangedEventArgs e) => RefreshLibrary();
    private void LibraryFilterChanged(object sender, SelectionChangedEventArgs e) => RefreshLibrary();
    private void ClearLibrarySearchClick(object sender, RoutedEventArgs e)
    {
        LibrarySearch.Clear();
        LibrarySearch.Focus();
    }
    private void LibrarySelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (_ready && LibraryList.SelectedItem is Cue cue) PrepareCue(cue); }

    private async void ImportFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Importar imágenes, videos o audio", Multiselect = true,
            Filter = "Contenido multimedia|*.mkv;*.mp4;*.mov;*.webm;*.avi;*.wmv;*.mp3;*.wav;*.flac;*.ogg;*.jpg;*.jpeg;*.png;*.bmp|Todos los archivos|*.*" };
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
        SetStatus($"{result.added.Count} archivos incorporados" + (result.failed > 0 ? $"; {result.failed} no se pudieron leer." : ". Los originales permanecen en su carpeta."), result.failed > 0);
        if (result.added.Count > 0) LibraryList.SelectedItem = result.added[0];
    }
    private static Cue CopyCue(Cue c, bool newId = false) => new()
    {
        Id = newId ? Guid.NewGuid() : c.Id, Title = c.Title, Kind = c.Kind, Body = c.Body,
        Reference = c.Reference, Version = c.Version, FilePath = c.FilePath, HymnNumber = c.HymnNumber, Notes = c.Notes
    };
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
        var dialog = new CueEditDialog(cue) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        cue.Title = dialog.CueTitle; cue.HymnNumber = dialog.HymnNumber;
        if (cue.Kind == CueKind.Text) cue.Body = dialog.Body;
        RefreshLibrary(); SaveWorkspace(); PrepareCue(cue);
    }
    private Cue CreateTextCue() => new() { Title = string.IsNullOrWhiteSpace(TextTitleBox.Text) ? "Texto" : TextTitleBox.Text.Trim(), Body = TextBodyBox.Text.Trim(), Kind = CueKind.Text };
    private void PrepareTextClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextBodyBox.Text)) { SetStatus("Escribe el texto que quieres proyectar."); return; }
        PrepareCue(CreateTextCue());
    }
    private void SaveTextClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextBodyBox.Text)) { SetStatus("Escribe el texto que quieres guardar."); return; }
        var cue = CreateTextCue(); _workspace.Library.Add(cue); RefreshLibrary(); AddToPlan(cue);
    }
    private void MoveUpClick(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDownClick(object sender, RoutedEventArgs e) => MoveSelected(1);
    private void MoveSelected(int delta)
    {
        var i = PlanList.SelectedIndex; var target = i + delta;
        if (i < 0 || target < 0 || target >= _plan.Count) return;
        _plan.Move(i, target); SaveWorkspace();
    }
    private void RemoveCueClick(object sender, RoutedEventArgs e)
    {
        if (PlanList.SelectedItem is not Cue cue) return;
        CommitNotes(); _plan.Remove(cue); UpdatePlanCount(); SaveWorkspace();
        SetStatus("Elemento retirado del orden. La proyección actual se conserva.");
    }
    private void PlanMouseDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(PlanList);
    private void PlanMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || PlanList.SelectedItem is not Cue cue) return;
        var delta = e.GetPosition(PlanList) - _dragStart;
        if (Math.Abs(delta.Y) >= SystemParameters.MinimumVerticalDragDistance)
            DragDrop.DoDragDrop(PlanList, new DataObject(typeof(Cue), cue), DragDropEffects.Move);
    }
    private void PlanDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(Cue)) is not Cue source) return;
        var element = e.OriginalSource as DependencyObject;
        while (element is not null && element is not ListBoxItem) element = VisualTreeHelper.GetParent(element);
        if (element is not ListBoxItem { DataContext: Cue target }) return;
        var from = _plan.IndexOf(source); var to = _plan.IndexOf(target);
        if (from >= 0 && to >= 0 && from != to) { _plan.Move(from, to); SaveWorkspace(); }
    }
    private void NewPlanClick(object sender, RoutedEventArgs e)
    {
        var name = PromptDialog.Ask(this, "Nuevo culto", "Nombre del culto", "Culto del sábado");
        if (name is null) return;
        CommitNotes(); SaveWorkspace();
        try { _store.Archive(_workspace.Plan); }
        catch (Exception ex) { SetStatus("No se pudo guardar el culto anterior: " + ex.Message, true); return; }
        _plan.Clear(); PlanNameBox.Text = name; UpdatePlanCount(); SaveWorkspace();
        SetStatus("Nuevo orden creado. El anterior se conserva en el historial local.");
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
            CommitNotes(); SaveWorkspace(); _store.Archive(_workspace.Plan);
            _plan.Clear(); foreach (var cue in plan.Items) _plan.Add(CopyCue(cue, true));
            PlanNameBox.Text = plan.Name; UpdatePlanCount(); SaveWorkspace();
            SetStatus("Culto abierto. La proyección actual no ha cambiado.");
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
        CommitNotes(); SaveWorkspace();
        JsonFileStore.Save(path, _workspace.Plan);
        SetStatus("Copia del orden guardada. Conserva también los archivos multimedia originales. La biblioteca, el mando y la configuración se guardan por separado en esta computadora.");
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
            SetStatus("Captura preparada. Pulsa Proyectar para mostrarla.");
        }
        catch (Exception ex) { SetStatus("No se pudo capturar la pantalla: " + ex.Message, true); }
        finally { WindowState = state; Activate(); }
    }
}
