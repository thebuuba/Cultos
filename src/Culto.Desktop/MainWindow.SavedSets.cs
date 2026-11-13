using System.Windows;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void VideoCollectionsClick(object sender, RoutedEventArgs e) { RefreshVideoLists(); ActivateWorkspacePanel("playlists"); }
    private void SavedServicesClick(object sender, RoutedEventArgs e) => ShowSavedSets(videosOnly: false);
    private void AddVideoToCollectionClick(object sender, RoutedEventArgs e)
    {
        if (LibraryList.SelectedItem is not Cue { Kind: CueKind.Video } cue)
        { SetStatus("Selecciona un video de la biblioteca."); return; }
        if (AppendToVideoQueue(cue)) ActivateWorkspacePanel("playlists");
    }

    private void ShowSavedSets(bool videosOnly, Cue? selectedVideo = null)
    {
        CommitNotes();
        var original = videosOnly ? _workspace.VideoCollections : _workspace.SavedServices;
        var dialog = new SavedSetsDialog(original, videosOnly, selectedVideo) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (videosOnly) _workspace.VideoCollections = dialog.Sets;
        else _workspace.SavedServices = dialog.Sets;
        if (!SaveWorkspace())
        {
            if (videosOnly) _workspace.VideoCollections = original;
            else _workspace.SavedServices = original;
            return;
        }
        if (dialog.RequestedSet is { } set) ApplySavedSet(set, dialog.ReplaceOrder);
        else SetStatus(videosOnly ? "Colecciones guardadas." : "Cultos guardados.");
    }

    private void SaveServiceClick(object sender, RoutedEventArgs e)
    {
        if (_plan.Count == 0) { SetStatus("El orden está vacío."); return; }
        var name = PromptDialog.Ask(this, "Guardar culto", "Nombre", PlanNameBox.Text);
        if (name is null) return;
        CommitNotes();
        var set = SavedSet.Capture(name, _plan);
        _workspace.SavedServices.Add(set);
        if (!SaveWorkspace()) { _workspace.SavedServices.Remove(set); return; }
        SetStatus($"Culto guardado: {set.Name}.");
    }

    private bool ApplySavedSet(SavedSet set, bool replace)
    {
        if (set.Items.Count == 0) { SetStatus("No hay contenido guardado."); return false; }
        CommitNotes();
        if (!SaveWorkspace()) return false;
        if (replace)
        {
            try { _store.Archive(_workspace.Plan); }
            catch (Exception ex) { SetStatus("No se pudo conservar el orden actual: " + ex.Message, true); return false; }
        }
        var oldPlan = _plan.ToList();
        var oldName = PlanNameBox.Text;
        var oldSelection = PlanList.SelectedItem;
        var items = set.CreateItems();
        // Prepare and live retain their own cues; loading a set never projects or stops media.
        _ready = false;
        try
        {
            if (replace) { _plan.Clear(); PlanNameBox.Text = set.Name; }
            foreach (var item in items) _plan.Add(item);
        }
        finally { _ready = true; }
        if (!SaveWorkspace())
        {
            _ready = false;
            try
            {
                _plan.Clear(); foreach (var cue in oldPlan) _plan.Add(cue);
                PlanNameBox.Text = oldName; PlanList.SelectedItem = oldSelection;
                _workspace.Plan.Name = oldName; _workspace.Plan.Items = oldPlan;
            }
            finally { _ready = true; }
            UpdatePlanCount(); return false;
        }
        if (replace) { _notesCue = null; NotesBox.Text = ""; }
        UpdatePlanCount();
        PlanList.SelectedItem = items[0]; PlanList.ScrollIntoView(items[0]);
        SetStatus(replace ? $"Orden reemplazado: {set.Name}." : $"{items.Count} contenidos añadidos: {set.Name}.");
        return true;
    }
}
