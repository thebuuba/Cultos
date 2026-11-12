using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Culto.Core;

namespace Culto.Desktop.Services;

public sealed record WindowCaptureTarget(WindowCaptureOptions Options, bool IsMinimized, int Width, int Height)
{
    public string DisplayName => Options.WindowTitle;
    public string Detail => Options.Executable + (IsMinimized ? " · Minimizada" : "");
}

public static class WindowCaptureCatalog
{
    public static (int Width, int Height)? GetSourceSize(long handle) =>
        IsWindow((nint)handle) && GetClientRect((nint)handle, out var bounds)
            && bounds.Right > 0 && bounds.Bottom > 0 ? (bounds.Right, bounds.Bottom) : null;

    public static bool WindowStillExists(WindowCaptureOptions selected)
    {
        var window = (nint)selected.WindowHandle;
        if (!IsWindow(window)) return false;
        GetWindowThreadProcessId(window, out uint processId);
        if (processId != selected.ProcessId) return false;
        if (selected.ProcessStartTimeUtcTicks <= 0) return true;
        try
        {
            using var process = Process.GetProcessById(selected.ProcessId);
            return process.StartTime.ToUniversalTime().Ticks == selected.ProcessStartTimeUtcTicks;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            // An inaccessible process is not proof that the selected window closed.
            return true;
        }
    }

    public static IReadOnlyList<WindowCaptureTarget> List()
    {
        var result = new List<WindowCaptureTarget>();
        var shell = GetShellWindow();
        EnumWindows((window, _) =>
        {
            if (window == shell || !IsWindowVisible(window)) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            var target = Read(window);
            if (target is not null) result.Add(target);
            return true;
        }, 0);
        return result.OrderBy(item => item.IsMinimized).ThenBy(item => item.Options.WindowTitle, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    // Never substitute a different window just because it belongs to the same browser.
    public static WindowCaptureOptions Resolve(WindowCaptureOptions saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var exact = Read(new nint(saved.WindowHandle));
        if (exact is not null && exact.Options.ProcessId == saved.ProcessId && SameApplication(exact.Options, saved)
            && saved.ProcessStartTimeUtcTicks > 0 && exact.Options.ProcessStartTimeUtcTicks == saved.ProcessStartTimeUtcTicks)
        {
            if (exact.IsMinimized) throw new InvalidOperationException("Restaura la ventana antes de capturarla.");
            return CopySelection(saved, exact.Options);
        }
        var matches = List().Where(item => SameApplication(item.Options, saved)
            && string.Equals(item.Options.WindowTitle, saved.WindowTitle, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(matches.Length == 0
                ? "La ventana ya no está disponible. Vuelve a elegirla en Capturar ventana."
                : "Hay varias ventanas con el mismo título. Vuelve a elegir la ventana.");
        if (matches[0].IsMinimized) throw new InvalidOperationException("Restaura la ventana antes de capturarla.");
        return CopySelection(saved, matches[0].Options);
    }

    // A running capture is pinned to its resolved identity. A temporarily hidden
    // window can return later; a closed/reused HWND must never fall back by title.
    public static WindowCaptureOptions? ResolveForReconnect(WindowCaptureOptions selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        selected.ValidateTarget();
        var exact = Read((nint)selected.WindowHandle, includeHidden: true);
        if (exact is null || exact.Options.ProcessId != selected.ProcessId || !SameApplication(exact.Options, selected)
            || selected.ProcessStartTimeUtcTicks <= 0 || exact.Options.ProcessStartTimeUtcTicks != selected.ProcessStartTimeUtcTicks)
            throw new InvalidOperationException("La ventana seleccionada se cerró o cambió. Vuelve a elegirla en Capturar ventana.");
        return exact.IsMinimized || !IsWindowVisible((nint)selected.WindowHandle) ? null : CopySelection(selected, exact.Options);
    }

    public static WindowCaptureOptions CopySelection(WindowCaptureOptions saved, WindowCaptureOptions current) => saved with
    {
        WindowHandle = current.WindowHandle, ProcessId = current.ProcessId,
        ProcessStartTimeUtcTicks = current.ProcessStartTimeUtcTicks,
        WindowTitle = current.WindowTitle, WindowClass = current.WindowClass, Executable = current.Executable
    };

    private static bool SameApplication(WindowCaptureOptions first, WindowCaptureOptions second) =>
        string.Equals(first.Executable, second.Executable, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.WindowClass, second.WindowClass, StringComparison.Ordinal);

    private static WindowCaptureTarget? Read(nint window, bool includeHidden = false)
    {
        if (window == 0 || !IsWindow(window) || !includeHidden && !IsWindowVisible(window)) return null;
        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0 || processId == Environment.ProcessId || processId > int.MaxValue) return null;
        int length = GetWindowTextLength(window);
        if (length <= 0 && !includeHidden) return null;
        var title = new StringBuilder(Math.Clamp(length + 1, 1, 4096));
        if (GetWindowText(window, title, title.Capacity) <= 0 && !includeHidden) return null;
        var type = new StringBuilder(256);
        if (GetClassName(window, type, type.Capacity) <= 0 || !GetClientRect(window, out var bounds)) return null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var executable = Path.GetFileName(process.MainModule?.FileName);
            if (string.IsNullOrWhiteSpace(executable)) return null;
            return new(new WindowCaptureOptions
            {
                WindowHandle = window.ToInt64(), ProcessId = (int)processId,
                ProcessStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                WindowTitle = title.ToString(), WindowClass = type.ToString(), Executable = executable
            }, IsIconic(window), Math.Max(0, bounds.Right - bounds.Left), Math.Max(0, bounds.Bottom - bounds.Top));
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        { return null; }
    }

    private delegate bool EnumWindowCallback(nint window, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out NativeRect rectangle);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
}
