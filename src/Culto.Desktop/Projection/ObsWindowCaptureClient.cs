using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Culto.Core;

namespace Culto.Desktop.Projection;

// A private windowless worker holds OBS/WGC. No OBS installation or external
// projector window is needed, and only the explicitly selected HWND is captured.
internal sealed class ObsWindowCaptureClient : IDisposable
{
    private static string DefaultWorker => Path.Combine(AppContext.BaseDirectory, "MoveHost", "Culto.MoveHost.exe");
    private static string DefaultRuntime => Path.Combine(AppContext.BaseDirectory, "Runtime", "ObsMove");
    private readonly string _workerPath, _runtimePath;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private Process? _worker;
    private NamedPipeServerStream? _pipe;
    private ObsMoveProtocol? _protocol;
    private int _refreshCount;
    private string? _lastWorkerError;
    private bool _disposed, _running;

    public ObsWindowCaptureClient(string? workerPath = null, string? runtimePath = null)
    { _workerPath = workerPath ?? DefaultWorker; _runtimePath = runtimePath ?? DefaultRuntime; }
    public static bool IsAvailable => Available(DefaultWorker, DefaultRuntime);
    private static bool Available(string worker, string runtime) => File.Exists(worker)
        && File.Exists(Path.Combine(runtime, "bin", "64bit", "obs.dll"))
        && File.Exists(Path.Combine(runtime, "bin", "64bit", "libobs-winrt.dll"));

    public async Task CaptureAsync(WindowCaptureOptions source, int width, int height,
        Action<byte[]> onFrame, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onFrame);
        source.ValidateTarget();
        int frameBytes = ObsMoveProtocol.FrameByteCount(width, height);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_running) throw new InvalidOperationException("Esta captura ya está activa.");
            _running = true;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            if (!Available(_workerPath, _runtimePath))
                throw new FileNotFoundException("Falta el motor de captura incluido en la distribución del programa.");
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            startup.CancelAfter(TimeSpan.FromSeconds(20));
            string pipeName = "CultoCapture-" + Guid.NewGuid().ToString("N");
            var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var start = new ProcessStartInfo(_workerPath)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(_workerPath)!
            };
            foreach (var argument in new[] { "--pipe", pipeName, "--runtime", _runtimePath,
                "--width", width.ToString(), "--height", height.ToString(), "--capture-window" })
                start.ArgumentList.Add(argument);
            Process process;
            lock (_gate)
            {
                if (_disposed) { pipe.Dispose(); throw new OperationCanceledException(lifetime.Token); }
                _pipe = pipe;
                process = _worker = Process.Start(start) ?? throw new IOException("No se pudo iniciar la captura de ventana.");
            }
            _ = DrainErrors(process);
            await pipe.WaitForConnectionAsync(startup.Token).ConfigureAwait(false);
            var protocol = new ObsMoveProtocol(pipe);
            var ready = await protocol.ReadAsync(startup.Token).ConfigureAwait(false);
            ThrowWorkerError(ready);
            if (ready?.Type != ObsMoveMessageType.Ready) throw new IOException("El motor de captura no confirmó su inicio.");
            await protocol.WriteAsync(ObsMoveMessageType.Command, ObsCaptureCommand.Encode(new(source)), startup.Token).ConfigureAwait(false);
            lock(_gate)_protocol=protocol;
            while (true)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                var message = await protocol.ReadAsync(deadline.Token).ConfigureAwait(false);
                ThrowWorkerError(message);
                if(message?.Type==ObsMoveMessageType.CaptureRefreshed){Interlocked.Increment(ref _refreshCount);continue;}
                if (message is null) throw new IOException("La captura se cerró. " + _lastWorkerError);
                if (message.Type != ObsMoveMessageType.Frame || message.Payload.Length != frameBytes)
                    throw new InvalidDataException("El motor de captura entregó una imagen incorrecta.");
                lifetime.Token.ThrowIfCancellationRequested();
                onFrame(message.Payload);
            }
        }
        catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
        { throw new TimeoutException("La captura no entregó imagen a tiempo. Comprueba que la ventana esté abierta y sin minimizar."); }
        finally { StopWorker(); lock (_gate) _running = false; }
    }

    private static void ThrowWorkerError(ObsMoveMessage? message)
    {
        if (message?.Type == ObsMoveMessageType.Error)
            throw new InvalidOperationException(Encoding.UTF8.GetString(message.Payload));
    }

    public async Task RefreshAsync()
    {
        ObsMoveProtocol? protocol;
        lock(_gate)protocol=_disposed ? null : _protocol;
        if(protocol is null)return;
        try { await protocol.WriteAsync(ObsMoveMessageType.RefreshCapture,ReadOnlyMemory<byte>.Empty,_lifetime.Token).ConfigureAwait(false); }
        catch(Exception error) when(error is IOException or ObjectDisposedException or OperationCanceledException) { }
    }

    private async Task DrainErrors(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
                _lastWorkerError = line.Length <= 512 ? line : line[..512];
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    private void StopWorker()
    {
        Process? process;
        lock (_gate) { _protocol=null; _pipe?.Dispose(); _pipe = null; process = _worker; _worker = null; }
        if (process is null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: false); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        finally { process.Dispose(); }
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _lifetime.Cancel(); StopWorker();
    }
}
