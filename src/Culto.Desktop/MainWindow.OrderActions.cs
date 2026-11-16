using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private Cue? _orderActionCue;

    private void ClearOrderClick(object sender, RoutedEventArgs e)
    {
        if (_plan.Count == 0 || MessageBox.Show(this, $"¿Eliminar los {_plan.Count} elementos del orden del culto? La salida actual continuará.",
            "Eliminar todo el orden", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        ClearOrder();
    }

    private void ClearOrder()
    {
        _plan.Clear(); HideOrderActions(); UpdatePlanCount();
        if (SaveWorkspace()) SetStatus("Se eliminaron todos los elementos del orden. La salida actual continúa.");
    }

    private void InitializeOrderActions()
    {
        var pasteMenu = new ContextMenu();
        var paste = new MenuItem { Header = "Pegar enlace de YouTube", InputGestureText = "Ctrl+V" };
        paste.Click += PasteYouTubeLinkClick; pasteMenu.Items.Add(paste); PlanList.ContextMenu = pasteMenu;
        PlanList.PreviewKeyDown += (_, e) =>
        {
            if (!_ready || e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control || e.OriginalSource is TextBoxBase) return;
            e.Handled = true;
            if (!e.IsRepeat) PasteYouTubeLinkClick(PlanList, new RoutedEventArgs());
        };
        PlanList.PreviewKeyDown += (_, e) =>
        {
            if (!_ready || e.Key != Key.Delete || Keyboard.Modifiers != ModifierKeys.None
                || !PlanList.IsKeyboardFocusWithin || e.OriginalSource is TextBoxBase) return;
            e.Handled = true;
            if (!e.IsRepeat) RemoveCueClick(PlanList, new RoutedEventArgs());
        };
        PlanList.SelectionChanged += (_, _) =>
        {
            if (!ReferenceEquals(PlanList.SelectedItem, _orderActionCue)) HideOrderActions();
        };
        _plan.CollectionChanged += (_, _) =>
        {
            if (_orderActionCue is not null && !_plan.Contains(_orderActionCue)) HideOrderActions();
        };
    }

    private void ShowOrderActions(Cue cue)
    {
        if (!_plan.Contains(cue)) return;
        _orderActionCue = cue;
        OrderActionsPanel.Tag = cue;
        OrderActionsPanel.ToolTip = cue.Title;
        // Reserve the same space while hidden so the first click cannot move the
        // clicked row before a second click or the start of a drag.
        OrderActionsPanel.Visibility = Visibility.Visible;
    }

    private void HideOrderActions()
    {
        _orderActionCue = null;
        OrderActionsPanel.Tag = null;
        OrderActionsPanel.ToolTip = null;
        OrderActionsPanel.Visibility = Visibility.Hidden;
    }

    private void PlayOrderActionClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || _orderActionCue is not Cue cue || !_plan.Contains(cue)) return;
        PlanList.SelectedItem = cue;
        if (_session.Prepared?.Id != cue.Id) PrepareCue(cue);
        ProjectPreparedFromInput();
    }

    private void DeleteOrderActionClick(object sender, RoutedEventArgs e)
    {
        if (!_ready || _orderActionCue is not Cue cue || !_plan.Contains(cue)) return;

        _plan.Remove(cue);
        HideOrderActions();
        UpdatePlanCount();
        if (SaveWorkspace()) SetStatus("Elemento retirado del orden.");
    }
}
