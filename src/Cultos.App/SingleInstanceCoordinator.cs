using System.IO.Pipes;
using System.Threading;

namespace Cultos.App;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\Cultos.SingleInstance";
    private const string PipeName = "Cultos.Activate";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();

    public bool IsPrimary { get; }

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        IsPrimary = createdNew;
    }

    public void StartListening(Action activate)
    {
        if (!IsPrimary) return;

        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(_cts.Token);
                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync(_cts.Token);
                    if (string.Equals(command, "ACTIVATE", StringComparison.Ordinal))
                    {
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(activate);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Fallo en el canal de instancia única", ex);
                    await Task.Delay(250);
                }
            }
        });
    }

    public async Task SignalPrimaryAsync()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                await client.ConnectAsync(400);
                await using var writer = new StreamWriter(client) { AutoFlush = true };
                await writer.WriteLineAsync("ACTIVATE");
                return;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(150);
            }
            catch
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (IsPrimary)
        {
            try { _mutex.ReleaseMutex(); } catch { }
        }
        _mutex.Dispose();
    }
}
