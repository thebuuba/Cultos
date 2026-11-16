using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using Culto.Core;

namespace Culto.Desktop.Services;

public static partial class YouTubeProjectionBrowser
{
    private static readonly SemaphoreSlim Navigation = new(1);
    private static WindowCaptureOptions? _lastWindow;
    private static (WindowCaptureOptions Target, int[]? Document)? _previousDocument;

    public static WindowCaptureTarget? SelectExistingWindow(IReadOnlyList<WindowCaptureTarget> windows, WindowCaptureOptions? preferred = null)
    {
        if (preferred is not null)
        {
            var exact = windows.FirstOrDefault(window => window.Options.WindowHandle == preferred.WindowHandle
                && window.Options.ProcessId == preferred.ProcessId && window.Options.ProcessStartTimeUtcTicks == preferred.ProcessStartTimeUtcTicks
                && window.Options.Executable.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact;
        }
        var youtube = windows.Where(window => YouTubeDiscovery.IsYouTube(window.Options)).ToArray();
        if (youtube.Length > 1) throw new InvalidOperationException("Hay varias ventanas de YouTube. Selecciona la que usas para proyectar en el panel YouTube.");
        return youtube.SingleOrDefault();
    }

    public static async Task<WindowCaptureOptions> NavigateAsync(string workspaceDirectory, string url, CancellationToken token = default,
        WindowCaptureOptions? preferred = null)
    {
        if (!YouTubeLink.TryParse(url, out var parsed)) throw new ArgumentException("El enlace no corresponde a un video de YouTube.");
        var link = parsed!;
        await Navigation.WaitAsync(token);
        var foreground = GetForegroundWindow();
        nint activated = 0;
        try
        {
            var target = SelectExistingWindow(WindowCaptureCatalog.List(), preferred ?? _lastWindow);
            if (target is null)
            {
                Open(workspaceDirectory);
                var opened = DateTime.UtcNow.AddSeconds(15);
                while (target is null && DateTime.UtcNow < opened)
                {
                    await Task.Delay(200, token);
                    target = SelectExistingWindow(WindowCaptureCatalog.List());
                }
                if (target is null) throw new InvalidOperationException("Abre YouTube en tu ventana habitual de Chrome y vuelve a intentarlo.");
            }
            var handle = (nint)target.Options.WindowHandle;
            activated = handle;
            if (target.IsMinimized) ShowWindow(handle, 9);
            SetForegroundWindow(handle);
            for (var attempt = 0; attempt < 10 && GetForegroundWindow() != handle; attempt++) await Task.Delay(50, token);
            if (GetForegroundWindow() != handle)
                throw new InvalidOperationException("No se pudo activar la ventana de YouTube. Selecciónala en Chrome y vuelve a intentarlo.");
            token.ThrowIfCancellationRequested();
            // Navigate the existing tab, keeping its profile, extensions and userscripts.
            // SendKeys runs on the UI thread; accessibility reads run on a worker.
            System.Windows.Forms.SendKeys.SendWait("^l");
            if (GetForegroundWindow() != handle) throw new InvalidOperationException("La ventana activa cambió. Vuelve a proyectar el enlace.");
            var addressBar = await Task.Run(() => FindAddressBar(handle), token);
            if (addressBar is null) throw new InvalidOperationException("No se pudo seleccionar la barra de direcciones de Chrome. Vuelve a proyectar el enlace.");
            await Task.Run(() =>
            {
                addressBar.SetFocus();
                ((ValuePattern)addressBar.GetCurrentPattern(ValuePattern.Pattern)).SetValue(link!.Url);
            }, token);
            token.ThrowIfCancellationRequested();
            if (GetForegroundWindow() != handle) throw new InvalidOperationException("La ventana activa cambió. Vuelve a proyectar el enlace.");
            if (!await Task.Run(() => addressBar.Current.HasKeyboardFocus, token))
                throw new InvalidOperationException("La barra de direcciones perdió el foco. Vuelve a proyectar el enlace.");
            var previousDocument = await Task.Run(() => ReadDocumentId(handle), token);
            if (GetForegroundWindow() != handle) throw new InvalidOperationException("La ventana activa cambió. Vuelve a proyectar el enlace.");
            _previousDocument = (target.Options, previousDocument);
            System.Windows.Forms.SendKeys.SendWait("{ENTER}");
            _lastWindow = target.Options;
            var until = DateTime.UtcNow.AddSeconds(25);
            while (DateTime.UtcNow < until)
            {
                token.ThrowIfCancellationRequested();
                var current = WindowCaptureCatalog.List().FirstOrDefault(window => window.Options.WindowHandle == target.Options.WindowHandle
                    && window.Options.ProcessId == target.Options.ProcessId && window.Options.ProcessStartTimeUtcTicks == target.Options.ProcessStartTimeUtcTicks);
                if (current is null) throw new InvalidOperationException("La ventana de YouTube se cerró.");
                var address = await Task.Run(() => FindAddressBar(handle) is { } bar ? ReadAddress(bar) : null, token);
                if (YouTubeLink.TryParse(address, out var loaded) && loaded!.VideoId == link.VideoId && loaded.StartSeconds == link.StartSeconds
                    && YouTubeDiscovery.IsYouTube(current.Options) && !current.IsMinimized)
                {
                    return current.Options with { ControlAudio = false, YouTubeOnly = true };
                }
                await Task.Delay(100, token);
            }
            throw new InvalidOperationException("YouTube no terminó de cargar el video. Revisa la ventana y la conexión a internet.");
        }
        finally
        {
            if (foreground != 0 && activated != 0 && GetForegroundWindow() == activated) SetForegroundWindow(foreground);
            Navigation.Release();
        }
    }

    public static async Task EnsurePlaybackAsync(WindowCaptureOptions target, CancellationToken token = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        var playingSamples = 0;
        var previous = _previousDocument is { } navigation && navigation.Target.WindowHandle == target.WindowHandle
            && navigation.Target.ProcessId == target.ProcessId && navigation.Target.ProcessStartTimeUtcTicks == target.ProcessStartTimeUtcTicks
            ? navigation.Document : null;
        while (DateTime.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            if (!WindowCaptureCatalog.WindowStillExists(target)) throw new InvalidOperationException("La ventana de YouTube se cerró.");
            var playing = await Task.Run(() => StartPausedPlayer((nint)target.WindowHandle, previous), token);
            playingSamples = playing ? playingSamples + 1 : 0;
            if (playingSamples >= 2) return;
            await Task.Delay(300, token);
        }
        throw new InvalidOperationException("YouTube no pudo iniciar la reproducción. Revisa si el video pide iniciar sesión o confirmar un aviso.");
    }

    private static int[]? ReadDocumentId(nint handle)
    {
        try
        {
            return AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document))?.GetRuntimeId();
        }
        catch (Exception error) when (error is ElementNotAvailableException or COMException) { return null; }
    }

    private static bool StartPausedPlayer(nint handle, int[]? previousDocument)
    {
        try
        {
            var root = AutomationElement.FromHandle(handle);
            if (previousDocument is not null)
            {
                var document = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
                if (document is null || document.GetRuntimeId().SequenceEqual(previousDocument)) return false;
            }
            var main = root.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ClassNameProperty, "ytp-play-button ytp-button")));
            if (main is not null && main.Current.IsEnabled)
            {
                var name = main.Current.Name.Trim();
                if (name.StartsWith("Paus", StringComparison.OrdinalIgnoreCase)) return true;
                if ((name.StartsWith("Play", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Reproducir", StringComparison.OrdinalIgnoreCase))
                    && main.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
                { ((InvokePattern)invoke).Invoke(); return false; }
            }
            var buttons = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            AutomationElement? play = null;
            foreach (AutomationElement button in buttons)
            {
                if (!button.Current.IsEnabled) continue;
                var type = button.Current.ClassName;
                if (!type.Contains("ytp-play-button", StringComparison.Ordinal) && !type.Contains("ytp-large-play-button", StringComparison.Ordinal)) continue;
                var name = button.Current.Name.Trim();
                if (name.StartsWith("Paus", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.StartsWith("Play", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Reproducir", StringComparison.OrdinalIgnoreCase)) play ??= button;
            }
            // Invoke the player's explicit Play button, never a blind play/pause shortcut.
            if (play is not null && play.TryGetCurrentPattern(InvokePattern.Pattern, out var action)) ((InvokePattern)action).Invoke();
            return false;
        }
        catch (Exception error) when (error is ElementNotAvailableException or COMException) { return false; }
    }

    private static AutomationElement? FindAddressBar(nint handle)
    {
        try
        {
            return AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ClassNameProperty, "OmniboxViewViews"));
        }
        catch (Exception error) when (error is ElementNotAvailableException or COMException)
        { throw new InvalidOperationException("No se pudo acceder a la barra de direcciones de Chrome.", error); }
    }

    private static string? ReadAddress(AutomationElement addressBar)
    {
        try
        {
            if (!addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) return null;
            var address = ((ValuePattern)pattern).Current.Value;
            return address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? address : "https://" + address;
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or COMException) { return null; }
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint handle, int command);
}
