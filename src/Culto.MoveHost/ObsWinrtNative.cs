using System.Runtime.InteropServices;

namespace Culto.MoveHost;

// The same Windows Graphics Capture backend used by OBS window capture.
// Its exported BOOL is a Win32 4-byte BOOL, unlike libobs' C bool.
internal static class ObsWinrtNative
{
    internal const string Library = "culto-libobs-winrt";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool winrt_capture_supported();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint winrt_capture_init_window([MarshalAs(UnmanagedType.Bool)] bool cursor, nint window,
        [MarshalAs(UnmanagedType.Bool)] bool clientArea, [MarshalAs(UnmanagedType.Bool)] bool forceSdr);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void winrt_capture_free(nint capture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool winrt_capture_active(nint capture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void winrt_capture_render(nint capture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern uint winrt_capture_width(nint capture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern uint winrt_capture_height(nint capture);
}
