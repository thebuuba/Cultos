using System.Windows;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void OutputPanelSizeChanged(object sender, SizeChangedEventArgs e) => UpdateOutputPanelLayout();

    private void UpdateOutputPanelLayout()
    {
        if (OutputDetails is null || ProductionPanel.ActualHeight <= 0) return;
        var available = Math.Max(0, ProductionPanel.ActualHeight - ProductionPanel.Padding.Top - ProductionPanel.Padding.Bottom
            - LivePanel.Padding.Top - LivePanel.Padding.Bottom
            - Math.Max(24, LiveHeaderGrid.ActualHeight));
        // A fixed controls area keeps both monitors stable when background
        // playback actions appear. Compact layouts scroll within this area.
        var actions = Math.Max(44, Math.Min(160, available - 102));
        var picture = Math.Max(80, available - actions - LivePicture.Margin.Top - LivePicture.Margin.Bottom);
        LivePicture.MaxHeight = picture;
        PreviewPicture.MaxHeight = picture;
        LiveScroll.MaxHeight = actions;
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
