using System.Windows;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void BibleStyleClick(object sender, RoutedEventArgs e)
    {
        var example = _session.Prepared?.Kind == CueKind.Bible ? _session.Prepared
            : (BibleResults.SelectedItem as BibleResult)?.Cue ?? _bibleReading.FirstOrDefault()
                ?? new Cue { Kind = CueKind.Bible, Title = "Vista previa", Body = "Texto bíblico", Reference = "Referencia", Version = "Biblia" };
        var dialog = new BibleStyleDialog(_workspace.BibleStyle, example, _workspace.Transitions) { Owner = this };
        if (dialog.ShowDialog() == true) ApplyBibleStyle(dialog.EditedStyle);
    }

    private bool ApplyBibleStyle(BibleVisualStyle style)
    {
        var previous = _workspace.BibleStyle;
        var next = style.Normalize();
        _workspace.BibleStyle = next;
        if (!SaveWorkspace()) { _workspace.BibleStyle = previous; return false; }
        _preview.BibleStyle = next.Normalize();
        if (_session.Prepared?.Kind == CueKind.Bible) _preview.ShowCue(_session.Prepared);
        SetStatus("Estilo de la Biblia guardado.");
        return true;
    }

    private void ShowLiveCue()
        => QueueLiveCue();

    private void ShowLiveCueCore(TransitionOptions transitions, BibleVisualStyle bibleStyle)
    {
        if (_output is null) return;
        _output.BibleStyle = bibleStyle;
        _output.Transitions = transitions;
        _output.BackgroundImagePath = _workspace.BackgroundImagePath;
        if (_session.Live?.Kind != CueKind.PowerPoint) StopPowerPointOutput(keepCueRequest: true);
        if (_session.IsBlackout || _session.IsTextHidden) _output.ShowCue(_session.Live, _session.IsBlackout, _session.IsTextHidden);
        else if (_session.Live is not null) _output.TransitionTo(_session.Live);
        else _output.ShowCue(_session.Live);
        if (_session.Live?.Kind == CueKind.PowerPoint) ShowPowerPointOutput();
    }
}
