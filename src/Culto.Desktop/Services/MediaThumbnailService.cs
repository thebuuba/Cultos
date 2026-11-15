using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Culto.Core;

namespace Culto.Desktop.Services;

// Shell extraction can access disk and codecs. It never runs on WPF's UI thread
// and never creates a media player. Both queued work and retained pixels are bounded.
internal static class MediaThumbnailService
{
    internal sealed record Thumbnail(BitmapSource? Image, TimeSpan? Duration);
    private const int CacheCapacity = 128;
    private const int ThumbnailSize = 256;
    private sealed record Work(string Path, CueKind Kind, CancellationToken Cancellation,
        TaskCompletionSource<Thumbnail?> Completion);
    private readonly record struct Key(string Path, long Modified, long Length);
    private sealed record Cached(Key Key, Thumbnail Result, long RetryAfter);
    private static readonly Channel<Work> Queue = Channel.CreateBounded<Work>(new BoundedChannelOptions(64)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false });
    private static readonly Dictionary<Key, LinkedListNode<Cached>> Cache = [];
    private static readonly LinkedList<Cached> Recent = [];

    static MediaThumbnailService()
    {
        var worker = new Thread(Run) { IsBackground = true, Name = "Culto media thumbnails" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    public static async Task<BitmapSource?> GetAsync(string path, CueKind kind, CancellationToken cancellation)
        => (await GetDetailsAsync(path, kind, cancellation).ConfigureAwait(false))?.Image;

    public static async Task<Thumbnail?> GetDetailsAsync(string path, CueKind kind, CancellationToken cancellation)
    {
        if (kind is not (CueKind.Image or CueKind.Video) || string.IsNullOrWhiteSpace(path)) return null;
        cancellation.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<Thumbnail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Queue.Writer.WriteAsync(new Work(path, kind, cancellation, completion), cancellation).ConfigureAwait(false);
        return await completion.Task.WaitAsync(cancellation).ConfigureAwait(false);
    }

    private static void Run()
    {
        var initialized = CoInitializeEx(IntPtr.Zero, 2) >= 0; // COINIT_APARTMENTTHREADED
        try
        {
            while (Queue.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                while (Queue.Reader.TryRead(out var work))
                {
                    if (work.Cancellation.IsCancellationRequested)
                    { work.Completion.TrySetCanceled(work.Cancellation); continue; }
                    Thumbnail? result = null;
                    try { result = Read(work); }
                    catch (Exception error)
                    { System.Diagnostics.Debug.WriteLine(error); /* A bad codec must not terminate the worker or application. */ }
                    work.Completion.TrySetResult(result);
                }
            }
        }
        finally { if (initialized) CoUninitialize(); }
    }

    private static Thumbnail? Read(Work work)
    {
        var file = new FileInfo(work.Path);
        if (!file.Exists) return null;
        var key = new Key(file.FullName.ToUpperInvariant(), file.LastWriteTimeUtc.Ticks, file.Length);
        if (Cache.TryGetValue(key, out var node))
        {
            if (node.Value.Result.Image is not null || Environment.TickCount64 < node.Value.RetryAfter)
            {
                Recent.Remove(node); Recent.AddFirst(node);
                return node.Value.Result;
            }
            Recent.Remove(node); Cache.Remove(key);
        }
        var offline = (file.Attributes & FileAttributes.Offline) != 0;
        BitmapSource? image = null;
        if (work.Kind == CueKind.Image && !offline)
        {
            try
            {
                using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                var landscape = frame.PixelWidth >= frame.PixelHeight;
                stream.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (landscape) bitmap.DecodePixelWidth = ThumbnailSize; else bitmap.DecodePixelHeight = ThumbnailSize;
                bitmap.StreamSource = stream;
                bitmap.EndInit(); bitmap.Freeze(); image = bitmap;
            }
            catch (Exception error) when (error is IOException or NotSupportedException or ArgumentException or FormatException)
            { /* Shell can support an image format that WPF does not decode. */ }
        }
        if (work.Cancellation.IsCancellationRequested) return null;
        image ??= ReadShell(file.FullName, offline);
        var result = new Thumbnail(image, work.Kind == CueKind.Video && !offline ? ReadDuration(file.FullName) : null);
        var cached = new LinkedListNode<Cached>(new(key, result, Environment.TickCount64 + 30000));
        Recent.AddFirst(cached); Cache[key] = cached;
        while (Cache.Count > CacheCapacity && Recent.Last is { } oldest)
        { Recent.RemoveLast(); Cache.Remove(oldest.Value.Key); }
        return result;
    }

    private static TimeSpan? ReadDuration(string path)
    {
        var storeId = typeof(IPropertyStore).GUID;
        IPropertyStore? store = null;
        var value = new PropertyValue();
        try
        {
            if (SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0x40, ref storeId, out store) < 0 || store is null) return null;
            // System.Media.Duration is a VT_UI8 count of 100-nanosecond units.
            var key = new PropertyKey { Format = new Guid("64440490-4c8b-11d1-8b70-080036b11a03"), Id = 3 };
            return store.GetValue(ref key, out value) >= 0 && value.Type == 21 && value.Unsigned is > 0 and <= long.MaxValue
                ? TimeSpan.FromTicks((long)value.Unsigned) : null;
        }
        finally
        {
            PropVariantClear(ref value);
            if (store is not null) Marshal.FinalReleaseComObject(store);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropertyValue
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public ulong Unsigned;
    }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr context, uint flags, ref Guid id,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? store);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropertyValue value);

    private static BitmapSource? ReadShell(string path, bool cachedOnly)
    {
        var factoryId = typeof(IShellItemImageFactory).GUID;
        IShellItemImageFactory? factory = null;
        var bitmapHandle = IntPtr.Zero;
        try
        {
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref factoryId, out factory) < 0 || factory is null) return null;
            // THUMBNAILONLY avoids mistaking a file association icon for a frame.
            // Offline placeholders use cached thumbnails, without downloading video.
            var flags = 0x8u | (cachedOnly ? 0x10u : 0u);
            if (factory.GetImage(new NativeSize(ThumbnailSize, ThumbnailSize), flags, out bitmapHandle) < 0
                || bitmapHandle == IntPtr.Zero) return null;
            var bitmap = Imaging.CreateBitmapSourceFromHBitmap(bitmapHandle, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            if (bitmapHandle != IntPtr.Zero) DeleteObject(bitmapHandle);
            if (factory is not null) Marshal.FinalReleaseComObject(factory);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize(int width, int height)
    { public readonly int Width = width; public readonly int Height = height; }

    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    { [PreserveSig] int GetImage(NativeSize size, uint flags, out IntPtr bitmap); }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint concurrency);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
}
