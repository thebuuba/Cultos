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
        LiveScroll.MaxHeight = Math.Max(44, LivePanel.ActualHeight - 240);
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
        Grid.SetRow(ChooseOutputButton, compact ? 1 : 0);
        Grid.SetRow(ToggleOutputButton, compact ? 1 : 0);
        ScreenNavigationPanel.Columns = 2;
        foreach (FrameworkElement button in ScreenNavigationPanel.Children)
            button.Margin = new Thickness(0, 0, 5, compact ? 4 : 0);

        // The player keeps a full-width timeline and centered round controls at
        // every width. Only the surrounding output actions need to wrap.
    }
}
