using System.Buffers.Binary;
using System.Text.Json;

namespace Culto.Core;

public enum ObsMoveMessageType : byte { Command = 1, Frame = 2, Complete = 3, Error = 4, Ready = 5, RefreshCapture = 6, CaptureRefreshed = 7 }
public sealed record ObsMoveCommand(string OutgoingPath, string IncomingPath, int DurationMilliseconds, MoveOptions? Move = null);
public sealed record ObsMoveMessage(ObsMoveMessageType Type, byte[] Payload);

/// <summary>One reader, serialized writers; byte type + signed int32 LE length + payload.</summary>
public sealed class ObsMoveProtocol(Stream stream)
{
    public const int MaximumPayloadBytes = 64 * 1024 * 1024;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static int FrameByteCount(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPayloadBytes / 4)
            throw new ArgumentOutOfRangeException(nameof(width), "Move frame dimensions exceed the protocol limit.");
        return checked(width * height * 4);
    }

    public static byte[] EncodeCommand(ObsMoveCommand command)
    {
        ValidateCommand(command);
        return JsonSerializer.SerializeToUtf8Bytes(command, JsonOptions);
    }

    public static ObsMoveCommand DecodeCommand(ReadOnlySpan<byte> payload)
    {
        var command = JsonSerializer.Deserialize<ObsMoveCommand>(payload, JsonOptions)
            ?? throw new InvalidDataException("Missing Move command.");
        ValidateCommand(command);
        return command;
    }

    private static void ValidateCommand(ObsMoveCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.OutgoingPath) || string.IsNullOrWhiteSpace(command.IncomingPath)
            || command.DurationMilliseconds is < 1 or > 60_000)
            throw new InvalidDataException("Move requires two snapshot paths and a duration from 1 to 60000 ms.");
    }

    public async ValueTask<ObsMoveMessage?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var header = new byte[5];
        if (await stream.ReadAsync(header.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), cancellationToken).ConfigureAwait(false);
        var type = (ObsMoveMessageType)header[0];
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
        ValidateFrame(type, length);
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return new(type, payload);
    }

    public async ValueTask WriteAsync(ObsMoveMessageType type, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ValidateFrame(type, payload.Length);
        var header = new byte[5];
        header[0] = (byte)type;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), payload.Length);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _writeGate.Release(); }
    }

    private static void ValidateFrame(ObsMoveMessageType type, int length)
    {
        if (type is < ObsMoveMessageType.Command or > ObsMoveMessageType.CaptureRefreshed)
            throw new InvalidDataException("Unknown Move message type.");
        if (length is < 0 or > MaximumPayloadBytes) throw new InvalidDataException("Move payload length exceeds limit.");
        if (type is ObsMoveMessageType.Ready or ObsMoveMessageType.Complete or ObsMoveMessageType.RefreshCapture or ObsMoveMessageType.CaptureRefreshed && length != 0)
            throw new InvalidDataException("Move control messages must have empty payloads.");
    }
}
