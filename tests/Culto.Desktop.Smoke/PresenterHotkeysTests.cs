using System.Runtime.InteropServices;
using System.Windows.Interop;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunPresenterHotkeysTests(MainWindow window)
    {
        Call(window, "DisposePresenterHotkeys");
        var handle = new WindowInteropHelper(window).Handle;
        const int nextId = 0x4351;
        const int previousId = 0x4352;
        const int reservationId = 0x4353;
        var directions = new List<int>();
        using var presenter = new PresenterHotkeys(handle, directions.Add);
        try
        {
            Test("Presenter dispatches next/previous without activating a window and releases disabled keys", () =>
            {
                Check(presenter.TryEnable(false, out var error), error ?? "Could not enable presenter");
                var foreground = PresenterGetForegroundWindow();
                PresenterSendMessage(handle, 0x0312, nextId, 0x22 << 16);
                PresenterSendMessage(handle, 0x0312, previousId, 0x21 << 16);
                Check(directions.SequenceEqual(new[] { 1, -1 }), "Presenter direction mapping is incorrect");
                Check(PresenterGetForegroundWindow() == foreground, "Presenter changed the foreground window");
                presenter.Disable();
                PresenterSendMessage(handle, 0x0312, nextId, 0x22 << 16);
                Check(directions.Count == 2, "A queued hotkey advanced after disable");
                Check(PresenterRegisterHotKey(handle, reservationId, 0x4000, 0x22), "Disabling did not release PageDown");
                PresenterUnregisterHotKey(handle, reservationId);
            });
            Test("Presenter releases the first registration if the second key is unavailable", () =>
            {
                Check(PresenterRegisterHotKey(handle, reservationId, 0x4000, 0x21), "Could not reserve PageUp for conflict test");
                try
                {
                    Check(!presenter.TryEnable(false, out var error) && !string.IsNullOrWhiteSpace(error), "A key conflict must report failure");
                    Check(!presenter.IsEnabled, "Partial registration was left enabled");
                    Check(PresenterRegisterHotKey(handle, reservationId + 1, 0x4000, 0x22), "Conflict left PageDown captured");
                    PresenterUnregisterHotKey(handle, reservationId + 1);
                }
                finally { PresenterUnregisterHotKey(handle, reservationId); }
            });
            Test("Presenter arrow mode ignores queued messages from the previous key mapping", () =>
            {
                Check(presenter.TryEnable(true, out var error), error ?? "Could not enable arrow presenter");
                directions.Clear();
                PresenterSendMessage(handle, 0x0312, nextId, 0x22 << 16);
                PresenterSendMessage(handle, 0x0312, nextId, 0x27 << 16);
                PresenterSendMessage(handle, 0x0312, previousId, 0x25 << 16);
                Check(directions.SequenceEqual(new[] { 1, -1 }), "Arrow mode accepted a stale PageDown message or lost arrow navigation");
                presenter.Dispose();
                Check(PresenterRegisterHotKey(handle, reservationId, 0x4000, 0x27), "Disposal did not release Right arrow");
                PresenterUnregisterHotKey(handle, reservationId);
            });
        }
        finally
        {
            presenter.Dispose();
            PresenterUnregisterHotKey(handle, reservationId);
            PresenterUnregisterHotKey(handle, reservationId + 1);
            Call(window, "SetupPresenterHotkeys");
        }
    }

    [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PresenterRegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PresenterUnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint PresenterSendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern nint PresenterGetForegroundWindow();
}
