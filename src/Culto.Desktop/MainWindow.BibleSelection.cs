using System.Windows;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private bool _updatingBibleSelection;
    private BibleResult[] _selectedBibleResults = [];
    private Cue? _selectedBibleCue;
    private IReadOnlyList<Cue>? _selectedBibleReading;
    private int _bibleProjectionClickTimestamp = -1;

    private BibleResult[] SelectedBibleResults() => BibleResults.Items.OfType<BibleResult>()
        .Where(result => BibleResults.SelectedItems.Contains(result)).ToArray();

    private Cue? SelectedBibleCue()
    {
        var selected = SelectedBibleResults();
        if (selected.Length == 0) return null;
        if (_selectedBibleCue is not null && selected.SequenceEqual(_selectedBibleResults)) return _selectedBibleCue;
        if (selected.Length > 1 && _bible is null) return null;
        var cue = selected.Length == 1 ? selected[0].Cue : _bible!.ToCue(selected.Select(result => result.Verse));
        _selectedBibleResults = selected;
        _selectedBibleCue = cue;
        _selectedBibleReading = _bibleReading;
        if (selected.Length > 1)
        {
            var ids = selected.Select(result => result.Cue.Id).ToHashSet();
            var reading = _bibleReading.ToList();
            var first = reading.FindIndex(item => ids.Contains(item.Id));
            var last = reading.FindLastIndex(item => ids.Contains(item.Id));
            // Next starts after the selected passage; Previous before it. The
            // combined ID must belong to this reading, not the catalog's singles.
            _selectedBibleReading = first >= 0
                ? reading.Take(first).Append(cue).Concat(reading.Skip(last + 1)).ToArray() : [cue];
        }
        return cue;
    }

    private void PrepareBibleSelection()
    {
        if (SelectedBibleCue() is { } cue) PrepareCue(cue, _selectedBibleReading);
    }

    private void UpdateBibleSelectionSummary()
    {
        var count = BibleResults.SelectedItems.Count;
        BibleSelectionText.Text = count == 0 ? "" : count == 1
            ? (BibleResults.SelectedItem as BibleResult)?.Reference ?? ""
            : $"{count} versículos · {SelectedBibleCue()?.Reference}";
        BibleSelectionText.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AddSelectedBibleButton.Content = count > 1 ? "+ Selección al orden" : "+ Versículo al orden";
    }
}
