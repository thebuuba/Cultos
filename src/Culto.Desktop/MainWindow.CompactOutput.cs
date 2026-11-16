using System.Windows;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void OutputPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (OutputTransportGrid is null) return;
        // The transport is inside the picture; only the short output actions scroll.
        var available = Math.Max(0, ProductionPanel.ActualHeight - ProductionPanel.Padding.Top - ProductionPanel.Padding.Bottom
            - LivePanel.Padding.Top - LivePanel.Padding.Bottom
            - Math.Max(24, LiveHeaderGrid.ActualHeight));
        var picture = Math.Max(102, Math.Min((LivePanel.ActualWidth - LivePanel.Padding.Left - LivePanel.Padding.Right) * 9 / 16 + 22, available - 44));
        LiveScroll.MaxHeight = Math.Max(44, available - picture);
    }

    private void UpdateOutputSwitchLabel()
    {
        var active = _outputWindow is not null;
        var action = active ? "Desactivar salida" : "Activar salida";
        ToggleOutputButton.IsChecked = active;
        ToggleOutputButton.ToolTip = action;
        System.Windows.Automation.AutomationProperties.SetName(ToggleOutputButton, action);
        var display = _outputDisplay ?? DisplayService.SelectOutput(DisplayService.GetDisplays(), _workspace.DisplayDevice, DisplayService.GetWindowDevice(this));
        OutputDisplayName.Text = display?.Label.Split('·')[0].Trim() ?? "Pantalla";
        ChooseOutputButton.ToolTip = display?.Label ?? "Conecta una pantalla externa para activar la salida";
    }
}
