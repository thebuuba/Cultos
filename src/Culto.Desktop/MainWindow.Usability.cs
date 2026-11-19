using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private int _activeContentSource;

    private void InitializeUsabilityShortcuts()
    {
        WorkspaceDock.ActiveContentChanged += (_, _) => UpdateActiveContentSource();
        InputBindings.Add(new KeyBinding(new ActionCommand(FocusActiveSource), Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SelectContentSource(0)), Key.D1, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SelectContentSource(1)), Key.D2, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SelectContentSource(2)), Key.D3, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(() => SelectContentSource(3)), Key.D4, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(ReturnToMedia), Key.F9, ModifierKeys.None));
    }

    private void SelectContentSource(int index)
    {
        if (!_ready || index is < 0 or > 3) return;
        _activeContentSource = index;
        ActivateWorkspacePanel(index switch { 0 => "sources", 1 => "bible", 2 => "text", _ => "youtube" });
        WorkspaceDock.UpdateLayout();
        FocusContentSourceSearch();
    }

    private void UpdateActiveContentSource()
    {
        ResetMediaFolderWhenReturning();
        FrameworkElement[] panels = [SourcesPanel, BiblePanel, TextPanel, YouTubePanel];
        var index = Array.FindIndex(panels, panel => ReferenceEquals(WorkspaceDock.ActiveContent, panel));
        if (index < 0) index = Array.FindIndex(panels, panel => panel.IsKeyboardFocusWithin);
        if (index >= 0) _activeContentSource = index;
    }

    private void FocusActiveSource()
    {
        if (!_ready) return;
        if (ReferenceEquals(WorkspaceDock.ActiveContent, MediaPanel) || WorkspaceDock.ActiveContent is null && MediaPanel.IsKeyboardFocusWithin)
        { MediaSearch.Focus(); MediaSearch.SelectAll(); return; }
        UpdateActiveContentSource();
        if (_activeContentSource == 2 && NotesEditor.Visibility == Visibility.Visible)
        { FlushNotesSave(); ShowNotesList(); }
        SelectContentSource(_activeContentSource);
    }
    private void FocusContentSourceSearch()
    {
        if (_activeContentSource == 3) { YouTubeList.Focus(); return; }
        var target = _activeContentSource switch
        {
            0 => LibrarySearch,
            1 => BibleQuery,
            _ => NotesEditor.Visibility == Visibility.Visible ? NotesBodyBox : NotesSearchBox
        };
        target.Focus();
        if (!ReferenceEquals(target, BibleQuery) || !SelectBibleQueryBook()) target.SelectAll();
    }

    private void UpdateUsabilityState()
    {
        if (!_ready) return;
        UpdatePlaybackAvailability();
        UpdatePowerPointPanel();
        PlanList.LiveCueId = _output is not null ? _session.Live?.Id : null;
        if (FindName("NextLiveText") is TextBlock next)
            next.Text = NextLiveHint();
    }

    private void UpdatePlaybackAvailability()
    {
        var capture = !HasBackgroundFileAudio && _output is not null && _session.Live?.Kind == CueKind.WindowCapture;
        OutputTransportGrid.Visibility = CanControlFileMedia ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.IsEnabled = HasVisibleMediaOutput && !capture;
        PauseButton.Visibility = capture ? Visibility.Collapsed : Visibility.Visible;
        foreach (var control in new FrameworkElement[] { PositionSlider, TimeText, OutputDurationText,
            RestartOutputButton, OutputSkipBackButton, OutputSkipForwardButton, LoopCheck })
            control.Visibility = capture ? Visibility.Collapsed : Visibility.Visible;
        if (capture) UpdateCapturePlayButton();
        if (FindName("MediaPlaybackPanel") is FrameworkElement mediaPanel)
            mediaPanel.Visibility = HasVisibleMediaOutput ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateCapturePlayButton()
    {
        PauseButton.IsEnabled = false;
        PauseButton.Visibility = Visibility.Collapsed;
        var label = "Reproducción controlada desde YouTube";
        PauseButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(PauseButton, label);
    }

    private string NextLiveHint()
    {
        var live = _session.Live;
        if (_output is null || live is null) return "Sin contenido en salida";
        if (live.Kind == CueKind.PowerPoint && _powerPoint?.IsRunning == true)
            return $"Diapositiva {_powerPointSlideIndex} de {_powerPoint.SlideCount}";
        var orderIndex = _plan.ToList().FindIndex(cue => cue.Id == live.Id);
        if (orderIndex >= 0)
            return orderIndex + 1 < _plan.Count ? "Siguiente: " + _plan[orderIndex + 1].Title : "Fin del orden";
        if (_liveReading is not null)
        {
            var readingIndex = _liveReading.ToList().FindIndex(cue => cue.Id == live.Id);
            if (readingIndex >= 0)
                return readingIndex + 1 < _liveReading.Count ? "Siguiente: " + _liveReading[readingIndex + 1].Title : "Fin de la lectura";
        }
        return "Fuera del orden";
    }
}
