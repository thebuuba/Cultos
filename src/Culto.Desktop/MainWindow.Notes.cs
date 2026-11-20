using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly ObservableCollection<WorkspaceNote> _notesView = [];
    private readonly DispatcherTimer _notesSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private WorkspaceNote? _editingNote;
    private bool _updatingNotes;
    private string _notesFilter = "all";
    private Window? _notesModerator;
    private TextBlock? _notesModeratorBody;

    private void InitializeNotes()
    {
        NotesList.ItemsSource = _notesView;
        CollectionViewSource.GetDefaultView(_notesView).GroupDescriptions.Add(new PropertyGroupDescription(nameof(WorkspaceNote.GroupLabel)));
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
                || note.Body.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .Where(note => _notesFilter == "all" || note.Category == _notesFilter)
                .OrderByDescending(note => note.IsPinned).ThenByDescending(note => note.ModifiedAt))
                _notesView.Add(note);
        }
        finally { _updatingNotes = false; }
        NotesCountText.Text = $"{_workspace.Notes.Count} notas · En esta PC";
        NotesEmptyText.Text = query.Length == 0 ? "Todavía no hay notas. Pulsa + Nueva para empezar." : "No hay notas que coincidan con la búsqueda.";
        NotesEmptyText.Visibility = _notesView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshNotesFilters(); UpdatePrivateNotes();
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
            NotesCategoryBox.SelectedValue = note.Category;
            NotesPinnedCheck.IsChecked = note.IsPinned;
            NotesModeratorCheck.IsChecked = note.ShowInModerator;
        }
        finally { _updatingNotes = false; }
        NotesOverview.Visibility = Visibility.Collapsed;
        NotesEditor.Visibility = Visibility.Visible;
        NotesSaveState.Text = "Guardado en esta PC";
        UpdateNoteProjectionButton();
    }

    private void NewNoteClick(object sender, RoutedEventArgs e)
    {
        FlushNotesSave();
        var note = new WorkspaceNote { Category = _notesFilter == "all" ? "sermon" : _notesFilter };
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
        UpdatePrivateNotes();
    }
    private void NotesBackClick(object sender, RoutedEventArgs e) { FlushNotesSave(); ShowNotesList(); }
    private void DeleteNoteClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var note = (sender as FrameworkElement)?.DataContext as WorkspaceNote ?? _editingNote;
        if (note is null || MessageBox.Show(this, $"¿Eliminar la nota «{note.DisplayTitle}» guardada en esta PC?", "Eliminar nota",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        DeleteNote(note);
    }
    private void DeleteCurrentNote()
    {
        if (_editingNote is { } note) DeleteNote(note);
    }
    private void DeleteNote(WorkspaceNote note)
    {
        if (!_workspace.Notes.Remove(note)) return;
        if (_session.Live?.Id == note.Id) { _session.RestoreOutput(null, false, false); ShowLiveCue(); }
        FlushNotesSave();
        if (ReferenceEquals(_editingNote, note)) ShowNotesList();
        else RefreshNotes();
    }

    private void SetNotesFilter(string category)
    {
        _notesFilter = category is "sermon" or "anuncio" or "oracion" or "recordatorio" ? category : "all";
        RefreshNotes();
    }
    private void NotesFilterClick(object sender, RoutedEventArgs e)
    { if (sender is FrameworkElement { Tag: string category }) SetNotesFilter(category); }
    private void RefreshNotesFilters()
    {
        foreach (var button in NotesFilters.Children.OfType<ToggleButton>())
        {
            var category = (string)button.Tag;
            var count = _workspace.Notes.Count(n => category == "all" || n.Category == category);
            var label = category == "all" ? "Todas" : new WorkspaceNote { Category = category }.CategoryLabel;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            if (category != "all") content.Children.Add(new System.Windows.Shapes.Ellipse { Width = 6, Height = 6,
                Fill = (Brush)new BrushConverter().ConvertFromString(new WorkspaceNote { Category = category }.CategoryColor)!, Margin = new Thickness(0, 0, 5, 0) });
            content.Children.Add(new TextBlock { Text = label + "  " + count });
            button.Content = content; button.IsChecked = category == _notesFilter;
        }
    }
    private void NoteCategoryChanged(object sender, SelectionChangedEventArgs e) => NoteOptionsChanged(sender, e);
    private void NoteOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || _updatingNotes || _editingNote is null) return;
        _editingNote.Category = NotesCategoryBox.SelectedValue as string ?? "sermon";
        _editingNote.IsPinned = NotesPinnedCheck.IsChecked == true;
        _editingNote.ShowInModerator = NotesModeratorCheck.IsChecked == true;
        _editingNote.ModifiedAt = DateTimeOffset.Now;
        FlushNotesSave();
    }
    private void DuplicateNoteClick(object sender, RoutedEventArgs e)
    {
        if (_editingNote is not { } note) return;
        FlushNotesSave();
        var copy = new WorkspaceNote { Title = note.DisplayTitle + " (copia)", Body = note.Body, Category = note.Category, ShowInModerator = note.ShowInModerator };
        _workspace.Notes.Add(copy); OpenNote(copy); FlushNotesSave();
    }
    private void NotesMoreClick(object sender, RoutedEventArgs e)
    { if (sender is Button { ContextMenu: { } menu } button) { menu.PlacementTarget = button; menu.IsOpen = true; } }
    private void ProjectNoteClick(object sender, RoutedEventArgs e)
    {
        if (_editingNote is not { } note) return;
        FlushNotesSave();
        if (_session.Live?.Id == note.Id && _session.Live.Kind == CueKind.Text)
        { _session.RestoreOutput(null, false, false); ShowLiveCue(); }
        else { PrepareCue(new Cue { Id = note.Id, Kind = CueKind.Text, Title = note.DisplayTitle, Body = note.Body }); ProjectPrepared(); }
        UpdateNoteProjectionButton();
    }
    private void UpdateNoteProjectionButton()
    {
        if (NotesProjectButton is null) return;
        NotesProjectButton.Content = _editingNote is { } note && _session.Live?.Id == note.Id ? "Detener nota" : "Proyectar nota";
    }
    private string PrivateNotesText => string.Join("\n\n", _workspace.Notes.Where(n => n.ShowInModerator)
        .OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.ModifiedAt).Select(n => n.DisplayTitle + "\n" + n.Body));
    private void UpdatePrivateNotes()
    {
        var text = PrivateNotesText;
        if (_notesModeratorBody is not null) _notesModeratorBody.Text = text.Length == 0 ? "Marca una nota para mostrarla aquí." : text;
        _powerPointModerator?.SetServiceNotes(text);
    }
    private void OpenNotesModeratorClick(object sender, RoutedEventArgs e)
    {
        FlushNotesSave();
        if (_notesModerator is { } existing) { existing.Activate(); return; }
        _notesModeratorBody = new TextBlock { Foreground = (Brush)FindResource("TextBrush"), FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
        var view = _notesModerator = new Window { Owner = this, Title = "Notas del culto · Moderador", Width = 560, Height = 650,
            MinWidth = 340, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("PanelBrush"), Content = new ScrollViewer { Content = _notesModeratorBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        view.Closed += (_, _) => { _notesModerator = null; _notesModeratorBody = null; };
        UpdatePrivateNotes(); view.Show();
    }
}
