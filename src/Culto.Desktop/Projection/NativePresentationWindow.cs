using System;
using System.Runtime.InteropServices;

namespace Culto.Desktop.Projection;

internal static class NativePresentationWindow
{
    // Windowed PowerPoint keeps animations running in the background, but adds
    // a status dock. Only touch children of the owned slideshow, never Office UI.
    public static void FitContent(IntPtr window)
    {
        var dock = FindWindowEx(window, IntPtr.Zero, "MsoCommandBarDock", "MsoDockBottom");
        var content = FindWindowEx(window, IntPtr.Zero, "MDIClient", null);
        if (dock == IntPtr.Zero || content == IntPtr.Zero) return;
        if (IsWindowVisible(dock)) ShowWindow(dock, 0);
        if (!GetClientRect(window, out var client) || !GetWindowRect(content, out var current)) return;
        var origin = new NativePoint();
        ClientToScreen(window, ref origin);
        if (current.Left == origin.X && current.Top == origin.Y
            && current.Right - current.Left == client.Right && current.Bottom - current.Top == client.Bottom) return;
        SetWindowPos(content, IntPtr.Zero, 0, 0, Math.Max(1, client.Right), Math.Max(1, client.Bottom), 0x0010 | 0x0004);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string type, string? title);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
