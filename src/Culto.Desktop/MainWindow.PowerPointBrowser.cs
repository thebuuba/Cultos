using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private IReadOnlyList<PowerPointSlidePreview> _powerPointSlidePreviews = [];
    private CancellationTokenSource? _powerPointPreviewCancellation;
    private string? _powerPointPreviewKey;
    private bool _updatingPowerPointSlides;
    private int _powerPointCursor = 1, _powerPointRequestedStart = 1;
    private bool SelectedPowerPointIsLive => _currentPowerPointFile is { } cue
        && _session.Live?.Kind == CueKind.PowerPoint && SamePowerPoint(cue, _session.Live) && _powerPoint?.IsRunning == true;
    private static bool SamePowerPoint(Cue? first, Cue? second) => first?.FilePath is { } path
        && string.Equals(path, second?.FilePath, StringComparison.OrdinalIgnoreCase);

    private void PowerPointSearchChanged(object sender, TextChangedEventArgs e) => RefreshPowerPointFiles();

    private async void LoadPowerPointSlides(Cue cue, bool refresh = false)
    {
        try
        {
            if (cue.FilePath is not { } path || !File.Exists(path))
            {
                _powerPointPreviewCancellation?.Cancel(); _powerPointPreviewKey = null; _powerPointCursor = 1;
                SetPowerPointSlideItems([]); PowerPointSlideHint.Text = "No se encuentra el archivo de esta presentación."; return;
            }
            var file = new FileInfo(path);
            var key = file.FullName + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length;
            if (!refresh && key == _powerPointPreviewKey) return;
            _powerPointPreviewKey = key;
            _powerPointPreviewCancellation?.Cancel(); _powerPointPreviewCancellation?.Dispose();
            var cancellation = _powerPointPreviewCancellation = CancellationTokenSource.CreateLinkedTokenSource(_updateCancellation.Token);
            var metadata = PowerPointFileMetadata.Get(cue);
            _powerPointCursor = 1;
            SetPowerPointSlideItems(metadata.Slides.Select(s => new PowerPointSlidePreview(s.Number, s.Title, null, null)).ToArray());
            if (!PowerPointPresentation.IsAvailable)
            {
                PowerPointSlideHint.Text = "Instala Microsoft PowerPoint para ver las miniaturas."; return;
            }
            if (metadata.Slides.Count == 0 && Path.GetExtension(path).ToLowerInvariant() is ".pptx" or ".ppsx")
            {
                PowerPointSlideHint.Text = "No se pudieron leer las diapositivas de este archivo."; return;
            }
            PowerPointSlideHint.Text = "Cargando miniaturas…";
            var slides = await PowerPointSlidePreviews.LoadAsync(path, metadata.Slides, cancellation.Token);
            if (_closing || cancellation.IsCancellationRequested || !ReferenceEquals(cancellation, _powerPointPreviewCancellation)) return;
            SetPowerPointSlideItems(slides);
            PowerPointSlideHint.Text = SelectedPowerPointIsLive ? "Clic para mostrar en pantalla" : "Clic para elegir inicio";
            if (_session.Prepared?.Kind == CueKind.PowerPoint && SamePowerPoint(cue, _session.Prepared)) RenderPreparedPreview();
            UpdatePowerPointPanel();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!_closing && SamePowerPoint(cue, _currentPowerPointFile))
            { _powerPointPreviewKey = null; PowerPointSlideHint.Text = "No se pudieron cargar las miniaturas. Pulsa ↻ para reintentar."; SetStatus("Miniaturas de PowerPoint: " + error.GetBaseException().Message, true); }
        }
    }
    private void SetPowerPointSlideItems(IReadOnlyList<PowerPointSlidePreview> slides)
    {
        _powerPointSlidePreviews = slides;
        _updatingPowerPointSlides = true;
        try { PowerPointSlides.ItemsSource = slides; PowerPointSlides.SelectedIndex = slides.Count > 0 ? Math.Clamp(_powerPointCursor - 1, 0, slides.Count - 1) : -1; }
        finally { _updatingPowerPointSlides = false; }
        UpdatePowerPointPanel();
    }
    private void PowerPointSlideSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _updatingPowerPointSlides || PowerPointSlides.SelectedItem is not PowerPointSlidePreview slide) return;
        if (SelectedPowerPointIsLive)
        {
            if (_session.IsBlackout || _session.IsTextHidden || _pendingCuePlayback.IsPending) return;
            try { _powerPoint!.GoToSlide(slide.Number); UpdatePowerPointPosition(); }
            catch (Exception error) { SetStatus("PowerPoint: " + error.Message, true); }
        }
        else
        {
            _powerPointCursor = slide.Number;
            if (_currentPowerPointFile is { } cue && !SamePowerPoint(cue, _session.Prepared)) PrepareCue(cue);
            else RenderPreparedPreview();
            UpdatePowerPointPanel();
        }
    }
    private void RefreshPowerPointPreviewsClick(object sender, RoutedEventArgs e)
    { if (_currentPowerPointFile is { } cue) { PowerPointFileMetadata.ClearCache(); LoadPowerPointSlides(cue, refresh: true); } }
    private void PowerPointBlackoutClick(object sender, RoutedEventArgs e)
    { if (_session.Live?.Kind == CueKind.PowerPoint && _powerPoint?.IsRunning == true) { ToggleBlackout(); UpdatePowerPointPanel(); } }
    private void StopPowerPointFromPanel()
    {
        StopPowerPointOutput(); _session.RestoreOutput(null, false, false); ShowLiveCue(); UpdatePowerPointPanel();
    }
    private void StartSelectedPowerPoint()
    {
        if (_currentPowerPointFile is not { } cue) { SetStatus("Selecciona una presentación de PowerPoint."); return; }
        PrepareCue(cue); ProjectPrepared(); UpdatePowerPointPanel();
    }
    private void PowerPointFileMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button) { menu.PlacementTarget = button; menu.IsOpen = true; }
        e.Handled = true;
    }
    private void RenamePowerPointFileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: Cue cue }) return;
        var title = PromptDialog.Ask(this, "Nombre de la presentación", "Nombre para mostrar en la biblioteca", cue.Title, false);
        if (title is null) return;
        var previous = cue.Title; cue.Title = title;
        if (!SaveWorkspace()) { cue.Title = previous; return; }
        PowerPointFileMetadata.ClearCache(); RefreshLibrary();
    }
    private void RemovePowerPointFileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: Cue cue }) return;
        var previous = _workspace.Library.ToList();
        _workspace.Library.RemoveAll(item => item.Kind == CueKind.PowerPoint && SamePowerPoint(item, cue));
        if (!SaveWorkspace()) { _workspace.Library = previous; return; }
        if (SamePowerPoint(cue, _currentPowerPointFile))
        {
            _currentPowerPointFile = null; _powerPointPreviewKey = null; _powerPointPreviewCancellation?.Cancel(); SetPowerPointSlideItems([]);
            FollowLivePreview(); PowerPointSlideHint.Text = "Selecciona una presentación";
        }
        RefreshLibrary(); SetStatus("Presentación quitada de la lista. El archivo original se conserva.");
    }
    private void PowerPointFilesDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(IsPowerPointPath) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private async void PowerPointFilesDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        e.Handled = true; await AddPowerPointFilesAsync(paths.Where(IsPowerPointPath).ToArray());
    }
    private static bool IsPowerPointPath(string path) => Path.GetExtension(path).ToLowerInvariant() is ".pptx" or ".ppt" or ".ppsx" or ".pps";
    private async Task AddPowerPointFilesAsync(string[] paths)
    {
        if (paths.Length == 0) return;
        await ImportMediaAsync(paths);
        var first = _workspace.Library.FirstOrDefault(c => c.Kind == CueKind.PowerPoint && string.Equals(c.FilePath, Path.GetFullPath(paths[0]), StringComparison.OrdinalIgnoreCase));
        PowerPointSearch.Clear();
        if (first is not null) PrepareCue(first);
    }
}
