using System.Windows;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void TransitionSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TransitionDialog(_workspace.Transitions) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var previous = _workspace.Transitions;
        _workspace.Transitions = dialog.Options;
        if (!SaveWorkspace()) { _workspace.Transitions = previous; return; }
        if (_output is not null) _output.Transitions = _workspace.Transitions;
        SetStatus("Transición del sistema guardada.");
    }
}
