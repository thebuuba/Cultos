using System.Diagnostics;

namespace Culto.Desktop.Services;

public static class YouTubeProjectionBrowser
{
    public static void Open(string workspaceDirectory)
    {
        var chrome = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        }.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("No se encontró Google Chrome instalado.");
        using var process = Process.Start(CreateStartInfo(chrome, Path.Combine(workspaceDirectory, "YouTubeChrome"),
            new Uri("https://www.youtube.com/"))) ?? throw new InvalidOperationException("No se pudo abrir YouTube.");
    }

    public static ProcessStartInfo CreateStartInfo(string chromePath, string profileDirectory, Uri address)
    {
        var profile = Path.GetFullPath(profileDirectory);
        Directory.CreateDirectory(profile);
        var start = new ProcessStartInfo(chromePath) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Normal };
        // Isolated startup is essential: an existing personal Chrome process
        // ignores new command-line switches. Web security and sandbox stay enabled.
        foreach (var argument in new[]
        {
            "--user-data-dir=" + profile, "--no-first-run", "--no-default-browser-check",
            "--disable-backgrounding-occluded-windows", "--disable-features=CalculateNativeWinOcclusion",
            "--disable-background-timer-throttling", "--disable-renderer-backgrounding",
            "--disable-background-media-suspend", "--new-window", address.AbsoluteUri
        }) start.ArgumentList.Add(argument);
        return start;
    }
}
