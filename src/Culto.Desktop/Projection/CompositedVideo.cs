using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Culto.Desktop.Projection;

// WPF must own the output pixels: a native child HWND cannot participate in a dissolve.
// Preview still uses native VideoView. Queue at most one presentation, dropping late frames.
internal sealed class CompositedVideo : Image, IDisposable
{
    private readonly SemaphoreSlim _bufferGate = new(1, 1);
    private IntPtr _allocation, _pixels;
    private int _width, _height, _pitch, _bytes, _queued, _epoch;
    private bool _disposed;
    private WriteableBitmap? _bitmap;
    public event Action? FramePresented;

    public CompositedVideo() => Stretch = Stretch.Uniform;

    public void Attach(MediaPlayer player)
    {
        player.SetVideoFormatCallbacks(Configure, Cleanup);
        player.SetVideoCallbacks(Lock, Unlock, Display);
    }

    private uint Configure(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        _bufferGate.Wait();
        try
        {
            FreeBuffer();
            if (_disposed || width == 0 || height == 0) return 0;
            // Bound frame copying to full HD while retaining the source aspect ratio.
            double scale = Math.Min(1, Math.Min(1920d / width, 1080d / height));
            width = (uint)Math.Max(1, Math.Round(width * scale));
            height = (uint)Math.Max(1, Math.Round(height * scale));
            _width = (int)width; _height = (int)height;
            pitches = (width * 4 + 31) & ~31u;
            lines = (height + 31) & ~31u;
            _pitch = (int)pitches; _bytes = checked((int)(pitches * lines));
            _allocation = Marshal.AllocHGlobal(_bytes + 31);
            _pixels = new IntPtr((_allocation.ToInt64() + 31) & ~31L);
            Marshal.Copy(new byte[] { (byte)'R', (byte)'V', (byte)'3', (byte)'2' }, 0, chroma, 4);
            return 1;
        }
        finally { _bufferGate.Release(); }
    }

    private IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        _bufferGate.Wait();
        Marshal.WriteIntPtr(planes, _pixels);
        return IntPtr.Zero;
    }

    private void Unlock(IntPtr opaque, IntPtr picture, IntPtr planes) => _bufferGate.Release();

    private void Display(IntPtr opaque, IntPtr picture)
    {
        if (_disposed || Dispatcher.HasShutdownStarted || Interlocked.Exchange(ref _queued, 1) != 0) return;
        var epoch = Volatile.Read(ref _epoch);
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            Interlocked.Exchange(ref _queued, 0);
            if (_disposed || epoch != _epoch || !_bufferGate.Wait(0)) return;
            bool presented = false;
            try
            {
                if (_pixels == IntPtr.Zero) return;
                if (_bitmap is null || _bitmap.PixelWidth != _width || _bitmap.PixelHeight != _height)
                    _bitmap = new WriteableBitmap(_width, _height, 96, 96, PixelFormats.Bgr32, null);
                _bitmap.WritePixels(new Int32Rect(0, 0, _width, _height), _pixels, _bytes, _pitch);
                Source = _bitmap;
                presented = true;
            }
            finally { _bufferGate.Release(); }
            if (presented) FramePresented?.Invoke();
        }));
    }

    public void Clear()
    {
        Interlocked.Increment(ref _epoch);
        Source = null;
    }

    private void Cleanup(ref IntPtr opaque)
    {
        _bufferGate.Wait();
        try { FreeBuffer(); }
        finally { _bufferGate.Release(); }
    }

    private void FreeBuffer()
    {
        if (_allocation != IntPtr.Zero) Marshal.FreeHGlobal(_allocation);
        _allocation = _pixels = IntPtr.Zero;
    }

    // Owner stops/disposes VLC before disposing the pixel buffer.
    internal void BeginDispose()
    {
        _disposed = true; Clear(); _bitmap = null;
    }
    internal void FinishDispose() { var opaque = IntPtr.Zero; Cleanup(ref opaque); }
    public void Dispose() { BeginDispose(); FinishDispose(); }
}
