using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace Culto.Core;

/// <summary>Single writer, single reader BGRA mailbox. Readers never wait for the UI writer.</summary>
[SupportedOSPlatform("windows")]
public sealed class SharedVideoFrame : IDisposable
{
    private const int HeaderBytes = 16;
    private const uint Magic = 0x43564c31;
    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _view;
    private readonly byte[]? _scratch;
    private readonly bool _writer;
    private long _sequence;
    public string MapName { get; }
    public int ByteCount { get; }
    private SharedVideoFrame(string name, int width, int height, bool writer)
    {
        MapName = name; ByteCount = ObsMoveProtocol.FrameByteCount(width, height); _writer = writer;
        _map = writer ? MemoryMappedFile.CreateNew(name, HeaderBytes + (long)ByteCount, MemoryMappedFileAccess.ReadWrite)
            : MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
        try
        {
            _view = _map.CreateViewAccessor(0, HeaderBytes + (long)ByteCount, writer ? MemoryMappedFileAccess.ReadWrite : MemoryMappedFileAccess.Read);
            if (writer) _view.Write(0, Magic);
            else
            {
                if (_view.ReadUInt32(0) != Magic) throw new InvalidDataException("Invalid live frame map.");
                _scratch = new byte[ByteCount];
            }
        }
        catch { _view?.Dispose(); _map.Dispose(); throw; }
    }
    public static SharedVideoFrame Create(int width, int height) => new("Local\\CultoLive-" + Guid.NewGuid().ToString("N"), width, height, true);
    public static SharedVideoFrame Open(string mapName, int width, int height) => new(mapName, width, height, false);
    public void Write(byte[] bgra)
    {
        if (!_writer) throw new InvalidOperationException("This mailbox is read-only.");
        if (bgra.Length != ByteCount) throw new ArgumentException("Expected one complete BGRA frame.", nameof(bgra));
        _view.Write(8, ++_sequence); Thread.MemoryBarrier();
        CopyPixels(bgra, write: true);
        Thread.MemoryBarrier(); _view.Write(8, ++_sequence);
    }
    /// <summary>Returns false when no completed frame exists or the producer is updating. Destination stays unchanged.</summary>
    public bool TryRead(byte[] destination)
    {
        if (_writer) throw new InvalidOperationException("Open a reader for this mailbox.");
        if (destination.Length != ByteCount) throw new ArgumentException("Expected one complete BGRA frame.", nameof(destination));
        for (int attempt = 0; attempt < 3; attempt++)
        {
            long before = _view.ReadInt64(8); Thread.MemoryBarrier();
            if (before == 0 || (before & 1) != 0) continue;
            CopyPixels(_scratch!, write: false); Thread.MemoryBarrier();
            if (before != _view.ReadInt64(8)) continue;
            _scratch!.CopyTo(destination, 0); return true;
        }
        return false;
    }
    private void CopyPixels(byte[] pixels, bool write)
    {
        // SafeBuffer's generic array access copies one element at a time. BGRA
        // needs a bulk byte copy; retain the view handle throughout native access.
        var handle = _view.SafeMemoryMappedViewHandle;
        bool retained = false;
        try
        {
            handle.DangerousAddRef(ref retained);
            var address = handle.DangerousGetHandle() + checked((nint)(_view.PointerOffset + HeaderBytes));
            if (write) Marshal.Copy(pixels, 0, address, ByteCount);
            else Marshal.Copy(address, pixels, 0, ByteCount);
        }
        finally { if (retained) handle.DangerousRelease(); }
    }
    public void Dispose() { _view.Dispose(); _map.Dispose(); }
}
