using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Culto.Core;

namespace Culto.Desktop.Projection;

// The native plugin runs in an isolated, windowless process. It never attaches
// to an installed/running OBS, changes its scenes, or opens a projector window.
internal sealed class ObsMoveTransitionClient : IDisposable
{
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private NamedPipeServerStream? _pipe;
    private ObsMoveProtocol? _protocol;
    private Process? _worker;
    private int _width, _height;
    private bool _disposed;
    private string? _lastWorkerError;
    private static string WorkerPath => Path.Combine(AppContext.BaseDirectory, "MoveHost", "Culto.MoveHost.exe");
    private static string RuntimePath => Path.Combine(AppContext.BaseDirectory, "Runtime", "ObsMove");
    public static bool IsAvailable => File.Exists(WorkerPath)
        && File.Exists(Path.Combine(RuntimePath, "bin", "64bit", "obs.dll"))
        && File.Exists(Path.Combine(RuntimePath, "obs-plugins", "64bit", "move-transition.dll"));

    public async Task RenderAsync(string outgoing, string incoming, int width, int height, int milliseconds,
        Action<byte[]> onFrame, CancellationToken cancellationToken, MoveOptions? move = null)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(30_000L + milliseconds));
            var token = timeout.Token;
            await EnsureConnected(width, height, token).ConfigureAwait(false);
            var command = new ObsMoveCommand(outgoing, incoming, milliseconds, move?.Normalize());
            await _protocol!.WriteAsync(ObsMoveMessageType.Command, ObsMoveProtocol.EncodeCommand(command), token).ConfigureAwait(false);
            var frameLength = ObsMoveProtocol.FrameByteCount(width, height);
            var receivedFrame = false;
            while (true)
            {
                var message = await _protocol.ReadAsync(token).ConfigureAwait(false)
                    ?? throw new IOException("El motor Move se cerró. " + _lastWorkerError);
                switch (message.Type)
                {
                    case ObsMoveMessageType.Frame:
                        if (message.Payload.Length != frameLength) throw new InvalidDataException("Move entregó un fotograma de tamaño incorrecto.");
                        receivedFrame = true;
                        onFrame(message.Payload);
                        break;
                    case ObsMoveMessageType.Complete:
                        if (!receivedFrame) throw new InvalidDataException("Move terminó sin entregar imagen.");
                        return;
                    case ObsMoveMessageType.Error:
                        throw new InvalidOperationException(Encoding.UTF8.GetString(message.Payload));
                    default: throw new InvalidDataException("Respuesta inesperada del motor Move.");
                }
            }
        }
        catch { StopWorker(); throw; }
        finally { _requestLock.Release(); }
    }

    private async Task EnsureConnected(int width, int height, CancellationToken token)
    {
        if (_worker is { HasExited: false } && _pipe?.IsConnected == true && _width == width && _height == height) return;
        StopWorker();
        if (!IsAvailable) throw new FileNotFoundException("Falta el motor OBS/Move incluido en la distribución del programa.");
        _ = ObsMoveProtocol.FrameByteCount(width, height);
        var pipeName = "CultoMove-" + Guid.NewGuid().ToString("N");
        _pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var start = new ProcessStartInfo(WorkerPath)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(WorkerPath)!
        };
        foreach (var argument in new[] { "--pipe", pipeName, "--runtime", RuntimePath, "--width", width.ToString(), "--height", height.ToString() })
            start.ArgumentList.Add(argument);
        _lastWorkerError = null;
        _worker = Process.Start(start) ?? throw new IOException("No se pudo iniciar el motor Move.");
        _ = DrainErrors(_worker);
        await _pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
        _protocol = new ObsMoveProtocol(_pipe);
        var ready = await _protocol.ReadAsync(token).ConfigureAwait(false);
        if (ready?.Type == ObsMoveMessageType.Error) throw new InvalidOperationException(Encoding.UTF8.GetString(ready.Payload));
        if (ready?.Type != ObsMoveMessageType.Ready) throw new IOException("El complemento Move no confirmó su inicio.");
        _width = width; _height = height;
    }

    private async Task DrainErrors(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                if (ReferenceEquals(_worker, process)) _lastWorkerError = line.Length <= 512 ? line : line[..512];
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    private void StopWorker()
    {
        _protocol = null;
        _pipe?.Dispose(); _pipe = null;
        var process = _worker; _worker = null;
        if (process is null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: false); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        finally { process.Dispose(); }
    }
    public void Dispose() { _disposed = true; StopWorker(); }
}
