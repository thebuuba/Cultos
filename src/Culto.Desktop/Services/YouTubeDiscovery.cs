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
    public static bool UsesBrowserAudio(WindowCaptureOptions options) =>
        string.Equals(Path.GetFileName(options.Executable), "chrome.exe", StringComparison.OrdinalIgnoreCase);

    public static bool IsYouTube(WindowCaptureOptions options) =>
        string.Equals(options.Executable, "chrome.exe", StringComparison.OrdinalIgnoreCase)
        && (options.WindowTitle ?? "").Contains("YouTube", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<YouTubeSource> Build(IEnumerable<Cue> savedSources, IReadOnlyList<WindowCaptureTarget> windows,
        WindowCaptureLayout? layout = null, Func<WindowCaptureOptions, bool>? windowStillExists = null)
    {
        // Old saved rows are intentionally ignored; only current Chrome tabs are sources.
        return windows.Where(window => IsYouTube(window.Options)).Select(target =>
        {
            var options=target.Options with { YouTubeOnly=true, ControlAudio=false };
            return new YouTubeSource($"window:{options.ProcessId}:{options.ProcessStartTimeUtcTicks}:{options.WindowHandle}",
                new Cue {Kind=CueKind.WindowCapture,Title=options.WindowTitle,
                    Capture=layout?.ApplyTo(options) ?? options},target,false);
        }).ToArray();
    }

    public static Cue Clone(Cue cue) => new()
    {
        Id = cue.Id, Title = cue.Title, Kind = cue.Kind, Body = cue.Body, Reference = cue.Reference,
        Version = cue.Version, FilePath = cue.FilePath, HymnNumber = cue.HymnNumber, Notes = cue.Notes,
        Capture = cue.Capture is { } capture ? capture with { } : null,
        Browser = cue.Browser is { } browser ? browser with { } : null
    };

    public static Cue ApplyLayout(Cue cue, WindowCaptureLayout? layout)
    {
        if(cue.Kind!=CueKind.WindowCapture || cue.Capture is not {} capture || !UsesBrowserAudio(capture))return cue;
        var copy=Clone(cue);
        copy.Capture=capture with {ControlAudio=false};
        if(!IsYouTube(capture))return copy;
        copy.Capture=copy.Capture with {YouTubeOnly=true};
        if(layout is not null && !WindowCaptureLayout.IsConfigured(capture))copy.Capture=layout.ApplyTo(copy.Capture);
        return copy;
    }
}
