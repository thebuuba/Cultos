using System.Windows;
using System.Windows.Controls;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool? _compactOutput;

    private void OutputPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (OutputTransportGrid is null) return;
        // Keep the picture visible even when only the controls need scrolling.
        var compact = LivePanel.ActualWidth < 320;
        LiveScroll.MaxHeight = Math.Max(48, LivePanel.ActualHeight - (compact ? 230 : 180));
        if (_compactOutput == compact) return;
        _compactOutput = compact;

        static void Place(UIElement element, int row, int column, int span = 1)
        { Grid.SetRow(element, row); Grid.SetColumn(element, column); Grid.SetColumnSpan(element, span); }

        Place(LiveTitleText, compact ? 1 : 0, compact ? 0 : 1, compact ? 3 : 1);
        LiveTitleText.Margin = compact ? new Thickness(0, 5, 0, 0) : new Thickness(0);
        Place(NextLiveText, 0, 0, compact ? 3 : 1);
        NextLiveText.Margin = compact ? new Thickness(0, 0, 0, 5) : new Thickness(0);
        Grid.SetRow(ChooseOutputButton, compact ? 1 : 0);
        Grid.SetRow(ToggleOutputButton, compact ? 1 : 0);
        ScreenNavigationPanel.Columns = compact ? 2 : 4;
        foreach (FrameworkElement button in ScreenNavigationPanel.Children)
            button.Margin = new Thickness(0, 0, 5, compact ? 4 : 0);

        for (var i = 0; i < OutputTransportGrid.ColumnDefinitions.Count; i++)
            OutputTransportGrid.ColumnDefinitions[i].Width = compact || i == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        Place(PauseButton, 0, 0, compact ? 2 : 1);
        Place(RestartOutputButton, 0, compact ? 2 : 1, compact ? 2 : 1);
        Place(TimeText, compact ? 1 : 0, compact ? 0 : 3, compact ? 4 : 1);
        TimeText.HorizontalAlignment = HorizontalAlignment.Right;
        TimeText.Margin = compact ? new Thickness(0, 4, 0, 0) : new Thickness(0);
        Place(PositionSlider, compact ? 2 : 0, compact ? 0 : 2, compact ? 4 : 1);
        PositionSlider.Margin = compact ? new Thickness(0, 2, 0, 4) : new Thickness(10, 0, 10, 0);
    }
}
