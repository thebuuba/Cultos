using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly Dictionary<int, string> _folderPaths = [];
    private readonly Dictionary<int, (string Path, LocalMediaScan Scan)> _folderCache = [];
    private IReadOnlyList<Cue> _folderItems = [];
    private int _activeFolderFilter = -1;
    private bool _folderLoading;
    private string? _folderError;
    private CancellationTokenSource? _folderScanCancellation;
    private FileSystemWatcher? _folderWatcher;
    private DispatcherTimer? _folderRefreshTimer;

    private static bool IsFolderFilter(int filter) => filter is 2 or 3 or 6 or 8 or 9 or 10;
    private string CurrentFolderPath => _folderPaths.GetValueOrDefault(_activeFolderFilter, "");

    private void SetLibraryFolder(int filter)
    {
        if (_activeFolderFilter == filter) return;
        DisposeLibraryFolders();
        _activeFolderFilter = filter;
        _folderItems = [];
        _folderError = null;
        _folderLoading = false;
        if (!IsFolderFilter(filter)) return;
        if (!_folderPaths.ContainsKey(filter)) _folderPaths[filter] = LocalMediaFolders.GetPath(filter);
        if (_folderCache.TryGetValue(filter, out var cached)
            && string.Equals(cached.Path, CurrentFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            _folderItems = cached.Scan.Items;
            _folderError = cached.Scan.Error;
        }
        WatchLibraryFolder();
        _ = LoadLibraryFolderAsync();
    }

    private async Task LoadLibraryFolderAsync()
    {
        if (_closing || !IsFolderFilter(_activeFolderFilter)) return;
        _folderScanCancellation?.Cancel();
        _folderScanCancellation?.Dispose();
        var cancellation = _folderScanCancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var filter = _activeFolderFilter;
        var path = CurrentFolderPath;
        var showProgress = _folderItems.Count == 0;
        _folderLoading = true;
        // Keep folder changes responsive before scanning files off the UI thread.
        await Task.Yield();
        if (token.IsCancellationRequested || _closing) return;
        RefreshMedia();
        try
        {
            CueKind? kind = filter switch { 2 => CueKind.Video, 3 => CueKind.Image, _ => null };
            IProgress<LocalMediaScan> progress = new Progress<LocalMediaScan>(partial =>
            {
                if (!showProgress || token.IsCancellationRequested || _closing || filter != _activeFolderFilter || !_folderLoading) return;
                _folderItems = partial.Items;
                RefreshMedia();
            });
            var result = await Task.Run(() => LocalMediaFolders.Scan(path, kind, token,
                showProgress ? progress.Report : null), token);
            if (token.IsCancellationRequested || _closing || filter != _activeFolderFilter) return;
            _folderItems = result.Items;
            _folderError = result.Error;
            _folderCache[filter] = (path, result);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception)
        {
            if (token.IsCancellationRequested || _closing) return;
            _folderItems = [];
            _folderError = "No se pudo leer la carpeta.";
        }
        if (token.IsCancellationRequested || _closing) return;
        _folderLoading = false;
        RefreshMedia();
        if (_folderWatcher is null) WatchLibraryFolder();
    }

    private void RefreshFolderClick(object sender, RoutedEventArgs e) => _ = LoadLibraryFolderAsync();

    private void WatchLibraryFolder()
    {
        var path = CurrentFolderPath;
        if (!Directory.Exists(path)) return;
        try
        {
            _folderRefreshTimer ??= CreateFolderRefreshTimer();
            _folderWatcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            _folderWatcher.Created += FolderFilesChanged;
            _folderWatcher.Deleted += FolderFilesChanged;
            _folderWatcher.Renamed += FolderFilesChanged;
            _folderWatcher.Changed += FolderFilesChanged;
            _folderWatcher.Error += (sender, _) =>
            {
                if (Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(() =>
                {
                    if (_closing || !ReferenceEquals(sender, _folderWatcher)) return;
                    _folderWatcher?.Dispose();
                    _folderWatcher = null;
                    QueueFolderRefresh();
                });
            };
            _folderWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _folderWatcher?.Dispose();
            _folderWatcher = null;
        }
    }

    private DispatcherTimer CreateFolderRefreshTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        timer.Tick += (_, _) => { timer.Stop(); _ = LoadLibraryFolderAsync(); };
        return timer;
    }

    private void FolderFilesChanged(object sender, FileSystemEventArgs e)
    {
        if (!ReferenceEquals(sender, _folderWatcher)) return;
        var supported = MediaLibrary.IsSupported(e.FullPath);
        // Logs, source files and application caches inside Documents must not
        // repeatedly cancel discovery of the operator's media.
        if (e.ChangeType == WatcherChangeTypes.Changed && !supported) return;
        var oldPath = e is RenamedEventArgs renamed ? renamed.OldFullPath : e.FullPath;
        if (supported || MediaLibrary.IsSupported(oldPath) || Directory.Exists(e.FullPath)
            || _folderItems.Any(cue => cue.FilePath?.StartsWith(oldPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) == true)) QueueFolderRefresh();
    }

    private void QueueFolderRefresh()
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_closing || !IsFolderFilter(_activeFolderFilter)) return;
            _folderRefreshTimer?.Stop();
            _folderRefreshTimer?.Start();
        });
    }

    private void DisposeLibraryFolders()
    {
        _folderScanCancellation?.Cancel();
        _folderScanCancellation?.Dispose();
        _folderScanCancellation = null;
        _folderWatcher?.Dispose();
        _folderWatcher = null;
        _folderRefreshTimer?.Stop();
    }
}
