using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void InitializeUsabilityShortcuts()
    {
        InputBindings.Add(new KeyBinding(new ActionCommand(FocusActiveSource), Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SelectContentSource(0)), Key.D1, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SelectContentSource(1)), Key.D2, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SelectContentSource(2)), Key.D3, ModifierKeys.Control));
    }

    private void SelectContentSource(int index)
    {
        if (!_ready) return;
        ContentTabs.SelectedIndex = index;
        ContentTabs.UpdateLayout();
        FocusActiveSource();
    }

    private void FocusActiveSource()
    {
        if (!_ready) return;
        var target = ContentTabs.SelectedIndex switch
        {
            0 => LibrarySearch,
            1 => BibleQuery,
            _ => TextBodyBox
        };
        target.Focus();
        target.SelectAll();
    }

    private void UpdateUsabilityState()
    {
        if (!_ready) return;
        var liveMedia = _outputWindow is not null && _session.Live?.Kind is CueKind.Video or CueKind.Audio;
        if (FindName("MediaPlaybackPanel") is FrameworkElement mediaPanel)
            mediaPanel.Visibility = liveMedia ? Visibility.Visible : Visibility.Collapsed;
        PreviewPauseButton.Visibility = _session.Prepared?.Kind is CueKind.Video or CueKind.Audio ? Visibility.Visible : Visibility.Collapsed;
        PreviewPlaybackPanel.Visibility = PreviewPauseButton.Visibility;
        if (FindName("ScreenNavigationPanel") is FrameworkElement navigation)
            navigation.Visibility = Visibility.Visible;
        PlanList.LiveCueId = _outputWindow is not null ? _session.Live?.Id : null;
        if (FindName("NextLiveText") is TextBlock next)
            next.Text = NextLiveHint();
    }

    private string NextLiveHint()
    {
        var live = _session.Live;
        if (_outputWindow is null || live is null) return "Prepara un contenido y pulsa Proyectar para comenzar.";
        var orderIndex = _plan.ToList().FindIndex(cue => cue.Id == live.Id);
        if (orderIndex >= 0)
            return orderIndex + 1 < _plan.Count ? "Siguiente: " + _plan[orderIndex + 1].Title : "Fin del orden · prepara otro contenido.";
        if (_liveReading is not null)
        {
            var readingIndex = _liveReading.ToList().FindIndex(cue => cue.Id == live.Id);
            if (readingIndex >= 0)
                return readingIndex + 1 < _liveReading.Count ? "Siguiente: " + _liveReading[readingIndex + 1].Title : "Fin de la lectura · busca otro pasaje.";
        }
        return "Fuera del orden · prepara el siguiente contenido.";
    }
}
