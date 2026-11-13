using System.Windows;
using System.Windows.Controls;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool? _compactOutput;

    private void OutputPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (OutputTransportGrid is null) return;
        // The transport is inside the picture; only the short output actions scroll.
        var compact = LivePanel.ActualWidth < 320;
        var available = Math.Max(0, LivePanel.ActualHeight - LivePanel.Padding.Top - LivePanel.Padding.Bottom
            - Math.Max(24, LiveHeaderGrid.ActualHeight));
        var picture = Math.Max(102, Math.Min((LivePanel.ActualWidth - LivePanel.Padding.Left - LivePanel.Padding.Right) * 9 / 16 + 22, available - 44));
        LiveScroll.MaxHeight = Math.Max(44, available - picture);
        if (_compactOutput == compact) return;
        _compactOutput = compact;

        static void Place(UIElement element, int row, int column, int span = 1)
        { Grid.SetRow(element, row); Grid.SetColumn(element, column); Grid.SetColumnSpan(element, span); }

        // Trim the title on one line instead of taking height from the picture
        // when the panel is both narrow and short. Its tooltip keeps the full title.
        Place(LiveTitleText, 0, 1);
        LiveTitleText.Margin = new Thickness(0);
        Place(NextLiveText, 0, 0, compact ? 3 : 1);
        NextLiveText.Margin = compact ? new Thickness(0, 0, 0, 5) : new Thickness(0);
        OutputActionsGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        OutputActionsGrid.ColumnDefinitions[1].Width = compact ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        OutputActionsGrid.ColumnDefinitions[2].Width = compact ? new GridLength(0) : GridLength.Auto;
        Place(ChooseOutputButton, compact ? 1 : 0, compact ? 0 : 1);
        Place(ToggleOutputButton, compact ? 1 : 0, compact ? 1 : 2);
        ChooseOutputButton.Margin = compact ? new Thickness(0, 0, 4, 0) : new Thickness(5, 0, 5, 0);
        UpdateOutputSwitchLabel();

        // The player keeps a full-width timeline and centered round controls at
        // every width. Only the surrounding output actions need to wrap.
    }

    private void UpdateOutputSwitchLabel()
    {
        var action = _outputWindow is null ? "Activar" : "Desactivar";
        ToggleOutputButton.Content = _compactOutput == true ? action : action + " HDMI";
        ToggleOutputButton.ToolTip = action + " HDMI";
        System.Windows.Automation.AutomationProperties.SetName(ToggleOutputButton, action + " HDMI");
    }
}
