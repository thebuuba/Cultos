using System;
using System.Threading;

namespace Culto.Desktop.Projection;

internal sealed class DefaultAudioDeviceFollower : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<string?> _currentDevice;
    private readonly Action<string?> _selectDevice;
    private Timer? _timer;
    private string? _selected;
    private bool _initialized, _closed;
    private int _invalidated;

    public DefaultAudioDeviceFollower(Func<string?> currentDevice, Action<string?> selectDevice)
    { _currentDevice = currentDevice; _selectDevice = selectDevice; }

    // Polling also retries adapters which Windows announces before they can open.
    // All native sink work stays outside the Windows notification callback.
    public void Start()
    {
        lock (_gate)
        {
            if (_closed || _timer is not null) return;
            _timer = new Timer(_ => Refresh(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }
    }

    public void Refresh()
    {
        lock (_gate)
        {
            if (_closed) return;
            try
            {
                var current = _currentDevice();
                if (Interlocked.Exchange(ref _invalidated, 0) != 0) _initialized = false;
                if (_initialized && current == _selected) return;
                _selectDevice(current);
                _selected = current; _initialized = true;
            }
            catch (Exception)
            {
                // A disconnected or still-starting endpoint is transient. Do not
                // permanently fail the decoder/browser; retry on the next poll.
                _initialized = false;
            }
        }
    }

    // PlaybackStopped can run on the sink's own thread while Refresh joins it.
    public void Invalidate() => Interlocked.Exchange(ref _invalidated, 1);

    public void Dispose()
    {
        lock (_gate) { _closed = true; _timer?.Dispose(); _timer = null; }
    }
}
