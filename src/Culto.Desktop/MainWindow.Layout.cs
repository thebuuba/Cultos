using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void OpenActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ToggleNotesClick(object sender, RoutedEventArgs e)
    {
        if (FindName("NotesPanel") is not FrameworkElement panel || FindName("NotesToggleButton") is not Button toggle) return;
        var opening = panel.Visibility != Visibility.Visible;
        panel.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        toggle.Content = opening ? "Ocultar notas" : "Notas del elemento";
        if (opening) NotesBox.Focus();
        else CommitNotes();
    }
}
