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
        if (!_ready || e.ChangedButton != MouseButton.Left || _orderDragPerformed
            || OrderCueAt(e.OriginalSource) is not Cue cue) return;
        PlanList.SelectedItem = cue;
        // Revisit a selected row after browsing elsewhere, without projecting it.
        if (_session.Prepared?.Id != cue.Id) PrepareCue(cue);
        ShowOrderActions(cue);
    }

    private void PlanDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || e.ChangedButton != MouseButton.Left || _orderDragPerformed
            || OrderCueAt(e.OriginalSource) is not Cue cue) return;
        e.Handled = true;
        PlanList.SelectedItem = cue;
        if (_session.Prepared?.Id != cue.Id) PrepareCue(cue);
        ProjectPreparedFromInput();
    }

    private void AddOrderSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || !int.TryParse(item.Tag?.ToString(), out var source)) return;
        SelectContentSource(source);
        SetStatus(source switch { 1 => "Biblia", 2 => "Notas", _ => "Biblioteca" });
    }

    private async void AddOrderFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Agregar archivos al orden del culto", Multiselect = true,
            Filter = MediaLibrary.MediaFileFilter + "|Todos los archivos|*.*" };
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

}
