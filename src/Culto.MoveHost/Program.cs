using System.IO.Pipes;
using System.Text;
using Culto.Core;

namespace Culto.MoveHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        ObsMoveProtocol? protocol = null;
        NamedPipeClientStream? pipe = null;
        try
        {
            var options = ParseArguments(args);
            pipe = new NamedPipeClientStream(".", options.Pipe, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(15_000).ConfigureAwait(false);
            protocol = new ObsMoveProtocol(pipe);
            if (options.CaptureWindow)
            {
                using var capture = new ObsWindowCaptureEngine(options.Runtime, options.Width, options.Height);
                await protocol.WriteAsync(ObsMoveMessageType.Ready, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
                var command = await protocol.ReadAsync().ConfigureAwait(false);
                if (command?.Type != ObsMoveMessageType.Command) throw new InvalidDataException("Falta la ventana de captura.");
                await capture.RunAsync(ObsCaptureCommand.Decode(command.Payload), protocol, CancellationToken.None).ConfigureAwait(false);
                return 0;
            }
            if (options.LiveMove)
            {
                using var live = new ObsLiveMoveEngine(options.Runtime, options.Width, options.Height);
                await protocol.WriteAsync(ObsMoveMessageType.Ready, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
                while (await protocol.ReadAsync().ConfigureAwait(false) is { } message)
                {
                    if (message.Type != ObsMoveMessageType.Command) throw new InvalidDataException("Expected live Move command.");
                    await live.RunAsync(ObsLiveMoveCommand.Decode(message.Payload), protocol, CancellationToken.None).ConfigureAwait(false);
                }
                return 0;
            }
            using var engine = new ObsMoveEngine(options.Runtime, options.Width, options.Height);
            await protocol.WriteAsync(ObsMoveMessageType.Ready, ReadOnlyMemory<byte>.Empty).ConfigureAwait(false);
            while (await protocol.ReadAsync().ConfigureAwait(false) is { } message)
            {
                if (message.Type != ObsMoveMessageType.Command) throw new InvalidDataException("MoveHost accepts only command messages.");
                await engine.RunAsync(ObsMoveProtocol.DecodeCommand(message.Payload), protocol, CancellationToken.None).ConfigureAwait(false);
            }
            return 0;
        }
        catch (Exception error)
        {
            if (protocol is not null)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await protocol.WriteAsync(ObsMoveMessageType.Error, Encoding.UTF8.GetBytes(error.Message), timeout.Token).ConfigureAwait(false);
                }
                catch (Exception) { /* A crashed/disconnected parent cannot receive an error. */ }
            }
            return 1;
        }
        finally { pipe?.Dispose(); }
    }

    private sealed record Options(string Pipe, string Runtime, int Width, int Height, bool CaptureWindow, bool LiveMove);
    private static Options ParseArguments(string[] args)
    {
        bool captureWindow = args.Length == 9 && args[8] == "--capture-window";
        bool liveMove = args.Length == 9 && args[8] == "--live-move";
        if (args.Length != 8 && !captureWindow && !liveMove) throw new ArgumentException("Expected --pipe NAME --runtime PATH --width W --height H [--capture-window|--live-move].");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < 8; index += 2)
            if (!values.TryAdd(args[index], args[index + 1])) throw new ArgumentException("Duplicate MoveHost argument.");
        if (!values.TryGetValue("--pipe", out var pipe) || string.IsNullOrWhiteSpace(pipe) || pipe.Length > 200
            || pipe.IndexOfAny(['/', '\\']) >= 0 || !values.TryGetValue("--runtime", out var runtime)
            || !Path.IsPathFullyQualified(runtime) || !values.TryGetValue("--width", out var rawWidth)
            || !values.TryGetValue("--height", out var rawHeight) || !int.TryParse(rawWidth, out int width) || !int.TryParse(rawHeight, out int height))
            throw new ArgumentException("Invalid MoveHost arguments.");
        ObsMoveProtocol.FrameByteCount(width, height);
        return new(pipe, runtime, width, height, captureWindow, liveMove);
    }
}
