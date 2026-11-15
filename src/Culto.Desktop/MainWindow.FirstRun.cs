using System.Windows;
using System.Windows.Threading;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool _firstRunSetupScheduled;

    private void ScheduleFirstRunSetup(bool hadWorkspace)
    {
        UpdateChurchWindowTitle();
        if (hadWorkspace || _workspace.FirstRunCompleted || _workspaceRecoveryBlocked || _firstRunSetupScheduled
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CULTO_DATA_DIR"))) return;
        _firstRunSetupScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (_closing || !IsVisible || _workspaceRecoveryBlocked || _workspace.FirstRunCompleted) return;
            ShowChurchSetup(firstRun: true);
        }));
    }

    private void ChurchSettingsClick(object sender, RoutedEventArgs e) => ShowChurchSetup(firstRun: false);

    private void ShowChurchSetup(bool firstRun)
    {
        var dialog = new FirstRunDialog(_workspace.ChurchName, firstRun) { Owner = this };
        if (dialog.ShowDialog() == true) ApplyChurchSetup(dialog.ChurchName);
    }

    private bool ApplyChurchSetup(string churchName)
    {
        if (!_ready || _closing) return false;
        var previousName = _workspace.ChurchName;
        var previousCompleted = _workspace.FirstRunCompleted;
        _workspace.ChurchName = churchName;
        _workspace.FirstRunCompleted = true;
        if (!SaveWorkspace())
        {
            _workspace.ChurchName = previousName; _workspace.FirstRunCompleted = previousCompleted;
            UpdateChurchWindowTitle(); return false;
        }
        UpdateChurchWindowTitle();
        SetStatus("Datos de la iglesia guardados en este equipo.");
        return true;
    }

    private void UpdateChurchWindowTitle() => Title = string.IsNullOrEmpty(_workspace.ChurchName)
        ? "Culto Studio — Consola de iglesia" : "Culto Studio — " + _workspace.ChurchName;
}
