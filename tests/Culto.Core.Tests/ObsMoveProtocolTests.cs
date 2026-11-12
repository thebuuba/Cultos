using System.Buffers.Binary;
using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class ObsMoveProtocolTests
{
    [Fact]
    public async Task WritesTheDocumentedByteAndLittleEndianHeader()
    {
        using var stream = new MemoryStream();
        await new ObsMoveProtocol(stream).WriteAsync(ObsMoveMessageType.Frame, new byte[300]);
        Assert.Equal(new byte[] { 2, 44, 1, 0, 0 }, stream.ToArray()[..5]);
    }

    [Fact]
    public async Task RoundTripsPartialReadsAndCleanEndOfStream()
    {
        using var stream = new MemoryStream();
        var writer = new ObsMoveProtocol(stream);
        var command = new ObsMoveCommand("salida ñ.png", "entrada.png", 650);
        await writer.WriteAsync(ObsMoveMessageType.Command, ObsMoveProtocol.EncodeCommand(command));
        await writer.WriteAsync(ObsMoveMessageType.Frame, new byte[] { 0, 127, 255, 255 });
        await writer.WriteAsync(ObsMoveMessageType.Complete, ReadOnlyMemory<byte>.Empty);
        stream.Position = 0;
        var reader = new ObsMoveProtocol(new FragmentedStream(stream));
        Assert.Equal(command, ObsMoveProtocol.DecodeCommand((await reader.ReadAsync())!.Payload));
        Assert.Equal(new byte[] { 0, 127, 255, 255 }, (await reader.ReadAsync())!.Payload);
        Assert.Equal(ObsMoveMessageType.Complete, (await reader.ReadAsync())!.Type);
        Assert.Null(await reader.ReadAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(67_108_865)]
    public async Task RejectsMaliciousLengthsBeforeAllocating(int length)
    {
        var bytes = new byte[5]; bytes[0] = 2;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), length);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await new ObsMoveProtocol(new MemoryStream(bytes)).ReadAsync());
    }

    [Theory]
    [InlineData(new byte[] { 2, 4 })]
    [InlineData(new byte[] { 2, 4, 0, 0, 0, 1, 2 })]
    public async Task RejectsTruncatedHeadersAndPayloads(byte[] bytes) =>
        await Assert.ThrowsAsync<EndOfStreamException>(async () => await new ObsMoveProtocol(new MemoryStream(bytes)).ReadAsync());

    [Theory]
    [InlineData(new byte[] { 8, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 5, 1, 0, 0, 0, 0 })]
    public async Task RejectsUnknownTypesAndNonemptyControlPayloads(byte[] bytes) =>
        await Assert.ThrowsAsync<InvalidDataException>(async () => await new ObsMoveProtocol(new MemoryStream(bytes)).ReadAsync());

    [Fact]
    public async Task ConcurrentWritersCannotInterleaveHeadersAndPayloads()
    {
        using var stream = new MemoryStream();
        var protocol = new ObsMoveProtocol(new FragmentedStream(stream));
        await Task.WhenAll(Enumerable.Range(1, 20).Select(async id =>
            await protocol.WriteAsync(ObsMoveMessageType.Frame, Enumerable.Repeat((byte)id, 100).ToArray())));
        stream.Position = 0;
        var reader = new ObsMoveProtocol(stream);
        var ids = new HashSet<byte>();
        while (await reader.ReadAsync() is { } message)
        {
            Assert.Equal(100, message.Payload.Length);
            Assert.All(message.Payload, value => Assert.Equal(message.Payload[0], value));
            Assert.True(ids.Add(message.Payload[0]));
        }
        Assert.Equal(20, ids.Count);
    }

    [Fact]
    public void ValidatesCommandsAndFrameBounds()
    {
        Assert.Equal(1920 * 1080 * 4, ObsMoveProtocol.FrameByteCount(1920, 1080));
        Assert.Throws<ArgumentOutOfRangeException>(() => ObsMoveProtocol.FrameByteCount(int.MaxValue, int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => ObsMoveProtocol.FrameByteCount(0, 1080));
        Assert.Throws<InvalidDataException>(() => ObsMoveProtocol.EncodeCommand(new("", "b.png", 1)));
        Assert.Throws<InvalidDataException>(() => ObsMoveProtocol.EncodeCommand(new("a.png", "b.png", 0)));
        Assert.Throws<InvalidDataException>(() => ObsMoveProtocol.EncodeCommand(new("a.png", "b.png", 60_001)));
    }

    private sealed class FragmentedStream(Stream inner) : Stream
    {
        public override bool CanRead => true; public override bool CanWrite => true; public override bool CanSeek => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken token) => inner.FlushAsync(token);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(2, count));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => inner.ReadAsync(buffer[..Math.Min(2, buffer.Length)], token);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        { foreach (var value in buffer.ToArray()) { await Task.Yield(); inner.WriteByte(value); } }
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
