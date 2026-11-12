using Culto.Core;

namespace Culto.Desktop.Services;

public sealed record YouTubeSource(string Key, Cue Cue, WindowCaptureTarget? Target, bool IsSaved)
{
    public bool IsAvailable => Target is { IsMinimized: false };
    public string Title => Cue.Title;
    public string Detail => Target is { } target
        ? $"{(IsSaved ? "Guardada" : "Detectada")} · {(target.Options.Executable.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) ? "Chrome" : Path.GetFileNameWithoutExtension(target.Options.Executable))}" + (target.IsMinimized ? " · Minimizada" : "")
        : "Guardada · no disponible; abre la ventana o edita la captura";
}

// Discovery only describes windows. Starting a capture always needs an operator action.
public static class YouTubeDiscovery
{
    public static bool IsYouTube(WindowCaptureOptions options) =>
        string.Equals(options.Executable, "chrome.exe", StringComparison.OrdinalIgnoreCase)
        && (options.WindowTitle ?? "").Contains("YouTube", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<YouTubeSource> Build(IEnumerable<Cue> savedSources, IReadOnlyList<WindowCaptureTarget> windows)
    {
        var saved = savedSources.Where(cue => cue.Kind == CueKind.WindowCapture).ToArray();
        var result = new List<YouTubeSource>();
        var matched = new HashSet<long>();
        foreach (var cue in saved)
        {
            WindowCaptureTarget? target = null;
            if (cue.Capture is { } source)
            {
                var applicationWindows = windows.Where(window => SameApplication(source, window.Options)).ToArray();
                var exact = applicationWindows.FirstOrDefault(window => SameWindow(source, window.Options));
                var eligible = applicationWindows.Where(window => !IsYouTube(source) || IsYouTube(window.Options)).ToArray();
                target = eligible.FirstOrDefault(window => ReferenceEquals(window, exact));
                if (exact is null && saved.Count(other => other.Capture is { } options
                    && SameApplication(source, options) && options.WindowTitle == source.WindowTitle) == 1)
                {
                    var titles = eligible.Where(window => window.Options.WindowTitle == source.WindowTitle).ToArray();
                    if (titles.Length == 1) target = titles[0];
                }
            }
            var draft = Clone(cue);
            if (target is not null)
            {
                matched.Add(target.Options.WindowHandle);
                draft.Capture = Rebind(cue.Capture!, target.Options);
            }
            result.Add(new("saved:" + cue.Id, draft, target, true));
        }
        foreach (var target in windows.Where(window => IsYouTube(window.Options) && !matched.Contains(window.Options.WindowHandle)))
        {
            var options = target.Options;
            result.Add(new($"window:{options.ProcessId}:{options.ProcessStartTimeUtcTicks}:{options.WindowHandle}",
                new Cue { Kind = CueKind.WindowCapture, Title = options.WindowTitle, Capture = options with { } }, target, false));
        }
        return result;
    }

    public static Cue Clone(Cue cue) => new()
    {
        Id = cue.Id, Title = cue.Title, Kind = cue.Kind, Body = cue.Body, Reference = cue.Reference,
        Version = cue.Version, FilePath = cue.FilePath, HymnNumber = cue.HymnNumber, Notes = cue.Notes,
        Capture = cue.Capture is { } capture ? capture with { } : null
    };

    private static bool SameWindow(WindowCaptureOptions saved, WindowCaptureOptions current) =>
        saved.WindowHandle == current.WindowHandle && saved.ProcessId == current.ProcessId
        && saved.ProcessStartTimeUtcTicks > 0 && saved.ProcessStartTimeUtcTicks == current.ProcessStartTimeUtcTicks;

    private static bool SameApplication(WindowCaptureOptions first, WindowCaptureOptions second) =>
        string.Equals(first.Executable, second.Executable, StringComparison.OrdinalIgnoreCase)
        && string.Equals(first.WindowClass, second.WindowClass, StringComparison.Ordinal);

    private static WindowCaptureOptions Rebind(WindowCaptureOptions saved, WindowCaptureOptions current) => saved with
    {
        WindowHandle = current.WindowHandle, ProcessId = current.ProcessId,
        ProcessStartTimeUtcTicks = current.ProcessStartTimeUtcTicks,
        WindowTitle = current.WindowTitle, WindowClass = current.WindowClass, Executable = current.Executable
    };
}
