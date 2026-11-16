using System.Diagnostics;

namespace Culto.Desktop.Services;

public static partial class YouTubeProjectionBrowser
{
    public static void Open(string workspaceDirectory, Culto.Core.WindowCaptureOptions? preferred = null)
    {
        var existing = SelectExistingWindow(WindowCaptureCatalog.List(), preferred ?? _lastWindow);
        if (existing is not null)
        {
            if (existing.IsMinimized) ShowWindow((nint)existing.Options.WindowHandle, 9);
            SetForegroundWindow((nint)existing.Options.WindowHandle);
            _lastWindow = existing.Options;
            return;
        }
        var chrome = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        }.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("No se encontró Google Chrome instalado.");
        var start = new ProcessStartInfo(chrome) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Normal };
        start.ArgumentList.Add("https://www.youtube.com/");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo abrir YouTube.");
    }

    public static ProcessStartInfo CreateStartInfo(string chromePath, string profileDirectory, Uri address)
    {
        var profile = Path.GetFullPath(profileDirectory);
        Directory.CreateDirectory(profile);
        var start = new ProcessStartInfo(chromePath) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Normal };
        // Explicit isolated launch for capture tests. Interactive YouTube uses Open
        // and NavigateAsync so the user's existing profile and extensions stay active.
        foreach (var argument in new[]
        {
            "--user-data-dir=" + profile, "--no-first-run", "--no-default-browser-check",
            "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1",
            "--disable-backgrounding-occluded-windows", "--disable-features=CalculateNativeWinOcclusion",
            "--disable-background-timer-throttling", "--disable-renderer-backgrounding",
            "--disable-background-media-suspend", "--new-window", address.AbsoluteUri
        }) start.ArgumentList.Add(argument);
        return start;
    }
}
