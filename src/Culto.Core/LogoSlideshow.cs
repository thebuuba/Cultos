namespace Culto.Core;

/// <summary>Chooses logo images without owning a clock, renderer, or file system.</summary>
public sealed class LogoSlideshow
{
    private string[] _paths = [];
    private int _index;
    private double _intervalMilliseconds;
    private long? _holdStartedAt;
    private long? _lastSample;

    public bool IsRunning => _paths.Length > 0;
    public string? CurrentPath => IsRunning ? _paths[_index] : null;

    /// <summary>Snapshots the playlist and returns the first image to project.</summary>
    public string? Start(IEnumerable<string> paths, string? selectedPath, LogoPlaybackOptions options)
    {
        Stop();
        _paths = paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _index = Math.Max(0, Array.FindIndex(_paths, path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase)));
        _intervalMilliseconds = options.Normalize().IntervalSeconds * 1000;
        return CurrentPath;
    }

    /// <summary>
    /// Samples a monotonic clock (for example Stopwatch.ElapsedMilliseconds).
    /// A returned path must be projected before the next sample. The hold starts
    /// on the first sample with no transition, including after each image change.
    /// Late ticks advance one image; outdated samples are ignored.
    /// </summary>
    public string? Advance(long elapsedMilliseconds, bool transitionInProgress)
    {
        if (!IsRunning || _paths.Length == 1 || elapsedMilliseconds < 0 || elapsedMilliseconds < _lastSample)
            return null;

        _lastSample = elapsedMilliseconds;
        if (transitionInProgress)
        {
            _holdStartedAt = null;
            return null;
        }

        if (_holdStartedAt is null)
        {
            _holdStartedAt = elapsedMilliseconds;
            return null;
        }

        if (elapsedMilliseconds - _holdStartedAt.Value < _intervalMilliseconds)
            return null;

        _index = (_index + 1) % _paths.Length;
        _holdStartedAt = null;
        return CurrentPath;
    }

    public void Stop()
    {
        _paths = [];
        _index = 0;
        _holdStartedAt = null;
        _lastSample = null;
    }
}
