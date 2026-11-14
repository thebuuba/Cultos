using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool? _workspaceSaveOkay;

    private void InitializeWorkspaceHeader()
    {
        Loaded += (_, _) => UpdateWorkspaceHeader();
        SizeChanged += (_, _) => UpdateWorkspaceHeader();
        _timer.Tick += (_, _) => UpdateWorkspaceHeader();
    }

    private void UpdateWorkspaceHeader()
    {
        if (_closing || Dispatcher.HasShutdownStarted) return;
        var compact = ActualHeight < 760;
        ConsoleDateText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ConsoleHeaderDetails.Margin = compact ? new Thickness(0) : new Thickness(0, 6, 0, 8);
        ConsoleDateText.Text = DateTime.Now.ToString("dddd, d 'de' MMMM", CultureInfo.GetCultureInfo("es-DO"));
        HeaderSaveState.Text = _workspaceSaveOkay == false || _workspaceRecoveryBlocked ? "Revisar"
            : PlanNameBox.Text.Trim() != _workspace.Plan.Name ? "Sin guardar" : _workspaceSaveOkay is null ? "Local" : "Guardado";
        var connected = _remote?.ConnectedClientCount ?? 0;
        RemoteConnectionText.Text = _remote is null ? "Desactivado" : connected == 0 ? "Sin dispositivos"
            : connected == 1 ? "1 conectado" : $"{connected} conectados";
        RemoteConnectionDot.Fill = (Brush)FindResource(_remote is null ? "PanelBorderBrush" : "AccentBrush");
    }

    private void MonitorFrameSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border frame && frame.ActualWidth > 0 && frame.ActualHeight > 0)
            frame.Clip = new RectangleGeometry(new Rect(frame.RenderSize), frame.CornerRadius.TopLeft, frame.CornerRadius.TopLeft);
    }
}
