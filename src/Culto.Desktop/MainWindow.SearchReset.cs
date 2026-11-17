using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<TextBox, DispatcherTimer> _searchResetTimers = [];

    private void CancelSearchReset(TextBox search)
    {
        if (_searchResetTimers.Remove(search, out var timer)) timer.Stop();
    }

    private void SchedulePlayedSearchReset(Cue cue)
    {
        TextBox? search = ReferenceEquals(LibraryList.SelectedItem, cue) ? LibrarySearch
            : ReferenceEquals(MediaList.SelectedItem, cue) ? MediaSearch : null;
        if (search is null || string.IsNullOrWhiteSpace(search.Text)) return;
        CancelSearchReset(search);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            CancelSearchReset(search);
            if (IsLoaded) search.Clear();
        };
        _searchResetTimers.Add(search, timer);
        timer.Start();
    }
}
