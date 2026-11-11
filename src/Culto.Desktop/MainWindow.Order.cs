using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;
using Microsoft.Win32;

namespace Culto.Desktop;

public partial class MainWindow
{
    private Cue? _orderDragCue;
    private bool _orderDragPerformed;

    private Cue? OrderCueAt(object source)
    {
        var element = source as DependencyObject;
        while (element is FrameworkContentElement content) element = content.Parent;
        if (element is null) return null;
        return ItemsControl.ContainerFromElement(PlanList, element) is ListBoxItem { DataContext: Cue cue }
            && _plan.Contains(cue) ? cue : null;
    }

    private void PlanMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || _orderDragPerformed || OrderCueAt(e.OriginalSource) is not Cue cue) return;
        // Revisit a selected row after browsing elsewhere without restarting its own video.
        if (_session.Prepared?.Id != cue.Id) PrepareCue(cue);
    }

    private void PlanDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || e.ChangedButton != MouseButton.Left || _orderDragPerformed
            || OrderCueAt(e.OriginalSource) is not Cue cue) return;
        e.Handled = true;
        PlanList.SelectedItem = cue;
        if (_session.Prepared?.Id != cue.Id) PrepareCue(cue);
        ProjectPrepared();
    }

    private void AddOrderSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || !int.TryParse(item.Tag?.ToString(), out var source)) return;
        SelectContentSource(source);
        SetStatus(source switch
        {
            1 => "Busca un pasaje y pulsa + Versículo al orden o Pasaje para agregarlo.",
            2 => "Escribe el anuncio y pulsa + Guardar en biblioteca y orden.",
            _ => "Busca y selecciona un contenido; pulsa + Añadir al orden para agregarlo."
        });
    }

    private async void AddOrderFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Agregar archivos al orden del culto", Multiselect = true,
            Filter = "Contenido multimedia|*.mkv;*.mp4;*.mov;*.webm;*.avi;*.wmv;*.mp3;*.wav;*.flac;*.ogg;*.jpg;*.jpeg;*.png;*.bmp|Todos los archivos|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        AddOrderButton.IsEnabled = false;
        try
        {
            await ImportMediaAsync(dialog.FileNames);
            var added = 0;
            foreach (var path in dialog.FileNames)
            {
                var cue = _workspace.Library.FirstOrDefault(c => string.Equals(c.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
                if (cue is null) continue;
                AddToPlan(cue); added++;
            }
            SetStatus($"{added} de {dialog.FileNames.Length} archivos añadidos al orden.", added != dialog.FileNames.Length);
        }
        catch (Exception ex) { SetStatus("No se pudieron agregar los archivos: " + ex.Message, true); }
        finally { AddOrderButton.IsEnabled = true; }
    }

    private void PreviewRestartClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || _session.Prepared?.Kind is not (CueKind.Video or CueKind.Audio)) return;
        _preview.Restart(); _previewPaused = false;
        PreviewPauseButton.Content = "Pausar previa";
    }

    private void PreviewPositionSeek(object sender, MouseButtonEventArgs e) => SeekPreview();
    private void PreviewPositionKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown) SeekPreview();
    }
    private void SeekPreview()
    {
        if (_ready && _session.Prepared?.Kind is CueKind.Video or CueKind.Audio)
            _preview.Seek(PreviewPositionSlider.Value);
    }
}
