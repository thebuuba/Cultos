using System.Runtime.InteropServices;
using System.Text;

namespace Culto.Desktop.Services;

// HWND ownership is deliberately required. A process-wide Office toolbar is not
// ours even if it appeared after Pause. Its actual attribution still needs native QA.
internal sealed class PowerPointToolbarSuppressor
{
    private readonly PowerPointWindowSnapshot _show;
    private PowerPointToolbarSuppressor(PowerPointWindowSnapshot show) => _show = show;

    public static PowerPointToolbarSuppressor? Capture(nint showHandle)
    {
        var show = ReadWindow(showHandle);
        return show is null ? null : new PowerPointToolbarSuppressor(show);
    }

    public void HideAttributedToolbars()
    {
        if (!SameIdentity(_show, ReadWindow(_show.Handle))) return;
        EnumWindows((candidate, _) =>
        {
            GetWindowThreadProcessId(candidate, out var process);
            if (process != _show.ProcessId) return true;
            var first = ReadChain(candidate);
            if (!PowerPointToolbarPolicy.CanHide(_show.Handle, _show.ProcessId, candidate, first)) return true;
            // Revalidate right before mutation; do not rely on an earlier window list.
            var current = ReadChain(candidate);
            if (!SameIdentity(_show, ReadWindow(_show.Handle))
                || !PowerPointToolbarPolicy.CanHide(_show.Handle, _show.ProcessId, candidate, current)
                || first.Count != current.Count
                || first.Any(pair => !current.TryGetValue(pair.Key, out var value) || pair.Value != value)) return true;
            const uint hideWithoutActivation = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0080 | 0x0200;
            SetWindowPos(candidate, 0, 0, 0, 0, 0, hideWithoutActivation);
            return true;
        }, 0);
    }

    private Dictionary<nint, PowerPointWindowSnapshot> ReadChain(nint candidate)
    {
        var windows = new Dictionary<nint, PowerPointWindowSnapshot>();
        var current = candidate;
        while (current != 0 && !windows.ContainsKey(current))
        {
            var window = ReadWindow(current);
            if (window is null) break;
            windows.Add(current, window);
            if (current == _show.Handle || window.ProcessId != _show.ProcessId) break;
            if (current == candidate && (!window.IsVisible || window.ClassName != "MsoCommandBar")) break;
            current = window.Owner;
        }
        return windows;
    }

    private static bool SameIdentity(PowerPointWindowSnapshot expected, PowerPointWindowSnapshot? actual) =>
        actual is not null && expected.Handle == actual.Handle && expected.ProcessId == actual.ProcessId
        && expected.ThreadId == actual.ThreadId && expected.ClassName == actual.ClassName && expected.Title == actual.Title;

    private static PowerPointWindowSnapshot? ReadWindow(nint handle)
    {
        if (handle == 0 || !IsWindow(handle)) return null;
        var thread = GetWindowThreadProcessId(handle, out var process);
        if (thread == 0 || process == 0) return null;
        var type = new StringBuilder(256);
        if (GetClassName(handle, type, type.Capacity) == 0) return null;
        var title = new StringBuilder(1024);
        GetWindowText(handle, title, title.Capacity);
        var snapshot = new PowerPointWindowSnapshot(handle, GetWindow(handle, 4), process, thread,
            type.ToString(), title.ToString(), IsWindowVisible(handle)); // GW_OWNER, never GA_ROOT of the shared app.
        return IsWindow(handle) ? snapshot : null;
    }

    private delegate bool WindowCallback(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint handle, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint handle, StringBuilder type, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint handle, StringBuilder title, int count);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint handle, nint after, int left, int top, int width, int height, uint flags);
}
