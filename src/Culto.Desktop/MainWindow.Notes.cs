using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly ObservableCollection<WorkspaceNote> _notesView = [];
    private readonly DispatcherTimer _notesSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private WorkspaceNote? _editingNote;
    private bool _updatingNotes;

    private void InitializeNotes()
    {
        NotesList.ItemsSource = _notesView;
        _notesSaveTimer.Tick += (_, _) => FlushNotesSave();
        Closed += (_, _) => _notesSaveTimer.Stop();
        ShowNotesList();
    }

    private void RefreshNotes()
    {
        if (!_ready) return;
        var query = NotesSearchBox.Text.Trim();
        _updatingNotes = true;
        try
        {
            _notesView.Clear();
            foreach (var note in _workspace.Notes.Where(note => query.Length == 0
                || note.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || note.Body.Contains(query, StringComparison.CurrentCultureIgnoreCase)).OrderByDescending(note => note.ModifiedAt))
                _notesView.Add(note);
        }
        finally { _updatingNotes = false; }
        NotesCountText.Text = $"{_workspace.Notes.Count} notas · En esta PC";
        NotesEmptyText.Text = query.Length == 0 ? "Todavía no hay notas. Pulsa + Nueva para empezar." : "No hay notas que coincidan con la búsqueda.";
        NotesEmptyText.Visibility = _notesView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowNotesList()
    {
        _editingNote = null;
        NotesEditor.Visibility = Visibility.Collapsed;
        NotesOverview.Visibility = Visibility.Visible;
        RefreshNotes();
    }

    private void OpenNote(WorkspaceNote note)
    {
        _updatingNotes = true;
        try
        {
            _editingNote = note;
            NotesTitleBox.Text = note.Title; NotesBodyBox.Text = note.Body;
            NotesModifiedText.Text = note.ModifiedLabel;
        }
        finally { _updatingNotes = false; }
        NotesOverview.Visibility = Visibility.Collapsed;
        NotesEditor.Visibility = Visibility.Visible;
        NotesSaveState.Text = "Guardado en esta PC";
    }

    private void NewNoteClick(object sender, RoutedEventArgs e)
    {
        FlushNotesSave();
        var note = new WorkspaceNote();
        _workspace.Notes.Add(note);
        NotesSearchBox.Clear();
        OpenNote(note);
        FlushNotesSave();
        NotesTitleBox.Focus();
    }

    private void NotesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingNotes && NotesList.SelectedItem is WorkspaceNote note) OpenNote(note);
    }
    private void NotesSearchChanged(object sender, TextChangedEventArgs e) => RefreshNotes();
    private void NotesChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _updatingNotes || _editingNote is null) return;
        _editingNote.Title = NotesTitleBox.Text;
        _editingNote.Body = NotesBodyBox.Text;
        _editingNote.ModifiedAt = DateTimeOffset.Now;
        NotesSaveState.Text = "Guardando…";
        _notesSaveTimer.Stop(); _notesSaveTimer.Start();
    }
    private void FlushNotesSave()
    {
        _notesSaveTimer.Stop();
        if (!_ready) return;
        NotesSaveState.Text = SaveWorkspace() ? "Guardado en esta PC" : "No se pudo guardar. Revisa el mensaje de la consola.";
        if (_editingNote is not null) NotesModifiedText.Text = _editingNote.ModifiedLabel;
    }
    private void NotesBackClick(object sender, RoutedEventArgs e) { FlushNotesSave(); ShowNotesList(); }
    private void DeleteNoteClick(object sender, RoutedEventArgs e)
    {
        if (_editingNote is null || MessageBox.Show(this, "¿Eliminar esta nota guardada en esta PC?", "Eliminar nota",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        DeleteCurrentNote();
    }
    private void DeleteCurrentNote()
    {
        if (_editingNote is null) return;
        _workspace.Notes.Remove(_editingNote);
        FlushNotesSave(); ShowNotesList();
    }
}
