using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Culto.Core;

namespace Culto.MoveHost;

internal sealed class ObsFramePump : IDisposable
{
    internal sealed record Frame(byte[] Buffer, int Length, ulong Timestamp, float Progress) : IDisposable
    { public void Dispose() => ArrayPool<byte>.Shared.Return(Buffer); }
    private readonly Channel<Frame> _frames = Channel.CreateBounded<Frame>(
        new BoundedChannelOptions(2) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest },
        itemDropped: frame => frame.Dispose());
    private readonly object _callbackGate = new();
    private readonly nint _video, _transition;
    private readonly int _width, _height, _byteCount;
    private readonly ObsNative.VideoCallback _callback;
    private bool _connected, _disposed;

    public ObsFramePump(nint transition, int width, int height)
    {
        _transition = transition; _width = width; _height = height;
        _byteCount = ObsMoveProtocol.FrameByteCount(width, height);
        _video = ObsNative.obs_get_video();
        _callback = OnFrame; // Root the delegate until after disconnect and all callbacks finish.
        if (_video == 0 || !ObsNative.video_output_connect(_video, 0, _callback, 0))
            throw new InvalidOperationException("OBS could not connect its raw BGRA output callback.");
        _connected = true;
    }

    public ValueTask<Frame> ReadAsync(CancellationToken token) => _frames.Reader.ReadAsync(token);

    private void OnFrame(nint parameter, nint nativeFrame)
    {
        lock (_callbackGate)
        {
            if (_disposed) return;
            byte[]? rented = null;
            try
            {
                // video_data: data[8] pointers, linesize[8] uint32s, timestamp uint64 (x64).
                var plane = Marshal.ReadIntPtr(nativeFrame);
                int stride = Marshal.ReadInt32(nativeFrame, 8 * IntPtr.Size);
                ulong timestamp = unchecked((ulong)Marshal.ReadInt64(nativeFrame, 8 * IntPtr.Size + 8 * sizeof(uint)));
                int rowBytes = checked(_width * 4);
                if (plane == 0 || stride < rowBytes) throw new InvalidDataException("OBS returned an invalid BGRA video plane.");
                rented = ArrayPool<byte>.Shared.Rent(_byteCount);
                for (int row = 0; row < _height; row++)
                    Marshal.Copy(nint.Add(plane, checked(row * stride)), rented, row * rowBytes, rowBytes);
                var frame = new Frame(rented, _byteCount, timestamp, ObsNative.obs_transition_get_time(_transition));
                rented = null;
                if (!_frames.Writer.TryWrite(frame)) frame.Dispose();
            }
            catch (Exception error)
            {
                // Never unwind a managed exception through libobs' native callback.
                if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
                _frames.Writer.TryComplete(error);
            }
        }
    }

    public void Dispose()
    {
        if (_connected) { _connected = false; ObsNative.video_output_disconnect(_video, _callback, 0); }
        lock (_callbackGate) { _disposed = true; _frames.Writer.TryComplete(); }
        while (_frames.Reader.TryRead(out var frame)) frame.Dispose();
        GC.KeepAlive(_callback);
    }
}
