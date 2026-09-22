using System.IO;
using System.Text;

namespace Cultos.App;

public static class AppLogger
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Cultos", "logs");
    private static readonly string LogPath = Path.Combine(Folder, "cultos.log");

    public static void Error(string context, Exception exception)
    {
        Write("ERROR", context + Environment.NewLine + exception);
    }

    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                RotateIfNeeded();
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < 2 * 1024 * 1024) return;
        var previous = Path.Combine(Folder, "cultos.previous.log");
        if (File.Exists(previous)) File.Delete(previous);
        File.Move(LogPath, previous);
    }
}
