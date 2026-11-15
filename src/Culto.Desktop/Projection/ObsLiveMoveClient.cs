using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Culto.Core;

namespace Culto.Desktop.Projection;

internal sealed class ObsLiveMoveClient : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private NamedPipeServerStream? _pipe;
    private ObsMoveProtocol? _protocol;
    private Process? _worker;
    private int _width, _height;
    private bool _disposed;
    private string? _error;
    public async Task RenderAsync(ObsLiveMoveCommand command, Action<byte[]> onFrame, CancellationToken cancellation)
    {
        await _gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(30000L + command.DurationMilliseconds));
            var token = timeout.Token;
            await Connect(command.Width, command.Height, token).ConfigureAwait(false);
            await _protocol!.WriteAsync(ObsMoveMessageType.Command, ObsLiveMoveCommand.Encode(command), token).ConfigureAwait(false);
            bool received = false;
            while (true)
            {
                var message = await _protocol.ReadAsync(token).ConfigureAwait(false) ?? throw new IOException("El motor Move se cerró. " + _error);
                if (message.Type == ObsMoveMessageType.Frame)
                {
                    if (message.Payload.Length != ObsMoveProtocol.FrameByteCount(command.Width, command.Height)) throw new InvalidDataException("Fotograma Move incompleto.");
                    received = true; onFrame(message.Payload);
                }
                else if (message.Type == ObsMoveMessageType.Complete && received) return;
                else if (message.Type == ObsMoveMessageType.Error) throw new InvalidOperationException(Encoding.UTF8.GetString(message.Payload));
                else throw new InvalidDataException("Respuesta inesperada del motor Move en vivo.");
            }
        }
        catch { Stop(); throw; }
        finally { _gate.Release(); }
    }
    private async Task Connect(int width, int height, CancellationToken token)
    {
        if (_worker is { HasExited: false } && _pipe?.IsConnected == true && _width == width && _height == height) return;
        Stop();
        var worker = Path.Combine(AppContext.BaseDirectory, "MoveHost", "Culto.MoveHost.exe");
        var runtime = Path.Combine(AppContext.BaseDirectory, "Runtime", "ObsMove");
        if (!File.Exists(worker)) throw new FileNotFoundException("Falta el motor Move en vivo.", worker);
        var name = "CultoLiveMove-" + Guid.NewGuid().ToString("N");
        _pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var start = new ProcessStartInfo(worker) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(worker)! };
        foreach (var argument in new[] { "--pipe", name, "--runtime", runtime, "--width", width.ToString(), "--height", height.ToString(), "--live-move" }) start.ArgumentList.Add(argument);
        _worker = Process.Start(start) ?? throw new IOException("No se pudo iniciar Move en vivo.");
        _error = null; _ = ReadErrors(_worker);
        await _pipe.WaitForConnectionAsync(token).ConfigureAwait(false); _protocol = new ObsMoveProtocol(_pipe);
        var ready = await _protocol.ReadAsync(token).ConfigureAwait(false);
        if (ready?.Type == ObsMoveMessageType.Error) throw new InvalidOperationException(Encoding.UTF8.GetString(ready.Payload));
        if (ready?.Type != ObsMoveMessageType.Ready) throw new IOException("Move en vivo no confirmó el inicio.");
        _width = width; _height = height;
    }
    private async Task ReadErrors(Process process)
    {
        try { while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line) if (ReferenceEquals(process, _worker)) _error = line.Length <= 512 ? line : line[..512]; }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException) { }
    }
    private void Stop()
    {
        _protocol = null; _pipe?.Dispose(); _pipe = null;
        var worker = _worker; _worker = null;
        if (worker is null) return;
        try { if (!worker.HasExited) worker.Kill(); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        finally { worker.Dispose(); }
    }
    public void Dispose() { _disposed = true; Stop(); }
}
