using System.Threading;
using System.Text.Json;
using Culto.Remote;

namespace Culto.Desktop.Services;

// The descriptor is ephemeral: only one desktop owns it, and credentials rotate on enable.
public sealed class StreamDeckPluginBridge : IAsyncDisposable
{
    private readonly string _directory;
    private readonly Func<RemoteSnapshot> _snapshot;
    private readonly Func<RemoteCommand, Task<CommandResult>> _command;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private FileStream? _lease;
    private RemoteHost? _host;
    private bool _published, _disposed;
    private string DescriptorPath => Path.Combine(_directory, "streamdeck-connection.json");

    public StreamDeckPluginBridge(string directory, Func<RemoteSnapshot> snapshot, Func<RemoteCommand, Task<CommandResult>> command)
    {
        _directory = Path.GetFullPath(directory);
        _snapshot = snapshot;
        _command = command;
    }

    public async Task StartAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_host is not null) return;
            Directory.CreateDirectory(_directory);
            // A stale lease file is harmless; the OS releases the exclusive handle after a crash.
            _lease = new FileStream(Path.Combine(_directory, "streamdeck-connection.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            var temporary = DescriptorPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Delete(DescriptorPath); // Remove an old session descriptor while this process owns the lease.
                _host = new RemoteHost();
                await _host.StartAsync(0, _snapshot, _command, loopbackOnly: true, requireToken: true);
                var descriptor = JsonSerializer.Serialize(new { schemaVersion = 1, address = "http://127.0.0.1:" + _host.Port,
                    token = _host.Token, processId = Environment.ProcessId });
                await File.WriteAllTextAsync(temporary, descriptor);
                File.Move(temporary, DescriptorPath, overwrite: true);
                _published = true;
            }
            catch
            {
                try { if (_host is not null) await _host.DisposeAsync(); }
                finally { _host = null; _lease.Dispose(); _lease = null; }
                throw;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { _lifecycle.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_published) { File.Delete(DescriptorPath); _published = false; }
            }
            finally
            {
                try { if (_host is not null) await _host.DisposeAsync(); }
                finally { _host = null; _lease?.Dispose(); _lease = null; }
            }
        }
        finally { _lifecycle.Release(); }
    }
}
