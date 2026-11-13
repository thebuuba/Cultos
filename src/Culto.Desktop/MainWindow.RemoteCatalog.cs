using Culto.Core;
using Culto.Remote;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<Guid, Cue> _remoteCatalog = [];

    private async Task<IReadOnlyList<RemoteCue>> SearchRemoteCatalog(string source, string query)
    {
        // Folder enumeration stays off the dispatcher, just like the PC browser.
        var paths = await Dispatcher.InvokeAsync(() => new[] { LocalMediaFolders.GetPath(2), LocalMediaFolders.GetPath(6), _workspace.MediaCustomFolderPath }
            .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var files = source == "videos" ? await Task.Run(() => paths.SelectMany(path => LocalMediaFolders.Scan(path!, CueKind.Video).Items).ToArray()) : [];
        return await Dispatcher.InvokeAsync<IReadOnlyList<RemoteCue>>(() =>
        {
            if (_closing) return [];
            IEnumerable<Cue> cues;
            if (source == "bible")
            {
                if (_bible is null || string.IsNullOrWhiteSpace(query)) return [];
                cues = _bible.Lookup(query, out _).Select(verse => _bibleCuesByReference[(verse.Book, verse.Chapter, verse.Verse)]);
            }
            else
            {
                cues = source == "hymns" ? _workspace.Library.Where(cue => cue.HymnNumber is not null)
                    : _workspace.Library.Concat(files).Concat(_folderItems).Where(cue => cue.Kind == CueKind.Video && cue.HymnNumber is null);
                cues = cues.Where(cue => query.Length == 0 || cue.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || cue.HymnNumber?.ToString() == query.TrimStart('0'));
            }
            var selected = cues.DistinctBy(cue => cue.Id).Take(100).ToArray();
            if (_remoteCatalog.Count > 4096) _remoteCatalog.Clear();
            foreach (var cue in selected) _remoteCatalog[cue.Id] = cue;
            return selected.Select(cue => new RemoteCue(cue.Id.ToString(), cue.Title, cue.Kind.ToString(), cue.Kind == CueKind.Bible ? cue.Body : null)).ToArray();
        });
    }
}
