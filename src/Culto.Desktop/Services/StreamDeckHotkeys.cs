using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Culto.Desktop.Services;

public sealed class StreamDeckHotkeys : IDisposable
{
    private const int FirstId = 0x4360, FirstKey = 0x7c;
    private static readonly string[] Actions = ["project", "previous", "next", "background", "blackout", "pause", "restart",
        "volume-down", "volume-up", "return-media", "scene-youtube", "scene-image"];
    private readonly nint _handle;
    private readonly HwndSource _source;
    private readonly Action<string> _execute;
    private int _registered;
    private bool _disposed;
    public bool IsEnabled => _registered == Actions.Length;

    public StreamDeckHotkeys(nint handle, Action<string> execute)
    {
        _handle = handle;
        _source = HwndSource.FromHwnd(handle) ?? throw new ArgumentException("La ventana de Stream Deck no está disponible.", nameof(handle));
        _execute = execute;
        _source.AddHook(HandleMessage);
    }

    public bool TryEnable(out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Disable(); error = null;
        for (var index = 0; index < Actions.Length; index++)
        {
            if (!RegisterHotKey(_handle, FirstId + index, 0x4000, (uint)(FirstKey + index)))
            {
                error = $"F{13 + index}: " + new Win32Exception(Marshal.GetLastWin32Error()).Message;
                Disable(); return false;
            }
            _registered++;
        }
        return true;
    }

    public void Disable()
    {
        while (_registered > 0) UnregisterHotKey(_handle, FirstId + --_registered);
    }

    private nint HandleMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (!IsEnabled || message != 0x0312) return 0;
        var index = (long)wParam - FirstId;
        if (index < 0 || index >= Actions.Length || ((long)lParam & 0xffff) != 0
            || (((long)lParam >> 16) & 0xffff) != FirstKey + index) return 0;
        handled = true;
        _execute(Actions[index]);
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disable(); _source.RemoveHook(HandleMessage); _disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
