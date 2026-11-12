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

    private sealed record Options(string Pipe, string Runtime, int Width, int Height);
    private static Options ParseArguments(string[] args)
    {
        if (args.Length != 8) throw new ArgumentException("Expected --pipe NAME --runtime PATH --width W --height H.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
            if (!values.TryAdd(args[index], args[index + 1])) throw new ArgumentException("Duplicate MoveHost argument.");
        if (!values.TryGetValue("--pipe", out var pipe) || string.IsNullOrWhiteSpace(pipe) || pipe.Length > 200
            || pipe.IndexOfAny(['/', '\\']) >= 0 || !values.TryGetValue("--runtime", out var runtime)
            || !Path.IsPathFullyQualified(runtime) || !values.TryGetValue("--width", out var rawWidth)
            || !values.TryGetValue("--height", out var rawHeight) || !int.TryParse(rawWidth, out int width) || !int.TryParse(rawHeight, out int height))
            throw new ArgumentException("Invalid MoveHost arguments.");
        ObsMoveProtocol.FrameByteCount(width, height);
        return new(pipe, runtime, width, height);
    }
}
