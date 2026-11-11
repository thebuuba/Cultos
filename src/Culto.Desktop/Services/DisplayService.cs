using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Culto.Desktop.Services;

public sealed record DisplayInfo(string DeviceName, string Label, int Left, int Top, int Width, int Height, bool IsPrimary);

public static class DisplayService
{
    public static IReadOnlyList<DisplayInfo> GetDisplays() => Forms.Screen.AllScreens.Select((screen, index) =>
        new DisplayInfo(screen.DeviceName, $"Pantalla {index + 1} · {screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? " · principal" : "")}",
            screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, screen.Primary)).ToArray();

    public static void PlaceWindow(Window window, DisplayInfo display, bool fullscreen)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(display);
        window.WindowState = WindowState.Normal;
        window.WindowStyle = fullscreen ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        window.ResizeMode = fullscreen ? ResizeMode.NoResize : ResizeMode.CanResize;
        window.Topmost = fullscreen;
        void Place()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int width = fullscreen ? display.Width : Math.Min(1280, Math.Max(320, display.Width - 100));
            int height = fullscreen ? display.Height : Math.Min(800, Math.Max(240, display.Height - 120));
            int left = display.Left + (display.Width - width) / 2;
            int top = display.Top + (display.Height - height) / 2;
            // Screen bounds and SetWindowPos both use physical pixels under PerMonitorV2.
            if (!SetWindowPos(handle, IntPtr.Zero, left, top, width, height, 0x0010 | 0x0004))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "No se pudo colocar la salida en la pantalla seleccionada.");
        }
        if (new WindowInteropHelper(window).Handle == IntPtr.Zero)
        {
            EventHandler? initialized = null;
            initialized = (_, _) =>
            {
                window.SourceInitialized -= initialized;
                Place();
                // Reapply after WPF's initial layout and WM_DPICHANGED processing.
                window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Place));
            };
            window.SourceInitialized += initialized;
        }
        else { Place(); window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Place)); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
