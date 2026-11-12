using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Culto.Desktop.Services;

/// <summary>Captures a presenter's keyboard pair without changing the foreground window.</summary>
public sealed class PresenterHotkeys : IDisposable
{
    private const int NextId = 0x4351;
    private const int PreviousId = 0x4352;
    private const int WmHotkey = 0x0312;
    private const uint NoRepeat = 0x4000;
    private readonly nint _handle;
    private readonly HwndSource _source;
    private readonly Action<int> _advance;
    private uint _nextKey;
    private uint _previousKey;
    private bool _disposed;

    public bool IsEnabled { get; private set; }

    public PresenterHotkeys(nint handle, Action<int> advance)
    {
        _handle = handle;
        _source = HwndSource.FromHwnd(handle) ?? throw new ArgumentException("La ventana del presentador no está disponible.", nameof(handle));
        _advance = advance;
        _source.AddHook(HandleMessage);
    }

    public bool TryEnable(bool useArrows, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Disable();
        error = null;
        _nextKey = useArrows ? 0x27u : 0x22u; // Right / PageDown
        _previousKey = useArrows ? 0x25u : 0x21u; // Left / PageUp
        if (!RegisterHotKey(_handle, NextId, NoRepeat, _nextKey))
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }
        if (!RegisterHotKey(_handle, PreviousId, NoRepeat, _previousKey))
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            UnregisterHotKey(_handle, NextId);
            return false;
        }
        IsEnabled = true;
        return true;
    }

    public void Disable()
    {
        if (!IsEnabled) return;
        IsEnabled = false;
        UnregisterHotKey(_handle, NextId);
        UnregisterHotKey(_handle, PreviousId);
    }

    private nint HandleMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (!IsEnabled || message != WmHotkey) return 0;
        var key = (uint)((long)lParam >> 16) & 0xffff;
        var direction = (int)wParam == NextId && key == _nextKey ? 1
            : (int)wParam == PreviousId && key == _previousKey ? -1 : 0;
        if (direction == 0) return 0;
        handled = true;
        _advance(direction);
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disable();
        _source.RemoveHook(HandleMessage);
        _disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
