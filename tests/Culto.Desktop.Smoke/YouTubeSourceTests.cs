using System.IO;
using Culto.Core;
using Culto.Desktop.Services;

internal static partial class Program
{
    // No Window, native enumeration, capture worker or browser is created.
    private static int RunYouTubeSourceTests()
    {
        WindowCaptureTarget Target(long handle, string title = "Video - YouTube - Google Chrome", string executable = "chrome.exe", long started = 100) =>
            new(new WindowCaptureOptions { WindowHandle = handle, ProcessId = 42, ProcessStartTimeUtcTicks = started,
                WindowTitle = title, Executable = executable, WindowClass = "Chrome_WidgetWin_1" }, false, 1280, 720);

        Test("YouTube discovery accepts only Chrome windows whose title contains YouTube", () =>
        {
            var rows = YouTubeDiscovery.Build([], [Target(1), Target(2, "youtube", "CHROME.EXE"),
                Target(3, "Chrome home"), Target(4, "YouTube", "msedge.exe")]);
            Check(rows.Count == 2 && rows.All(row => row.IsAvailable && !row.IsSaved), "Discovery matched another app or omitted Chrome/YouTube");
            Check(rows.Select(row => row.Cue.Capture!.WindowHandle).SequenceEqual(new long[] { 1, 2 }), "Discovery lost distinct windows");
        });

        Test("Rediscovery preserves saved crop and transform without modifying the saved source", () =>
        {
            var original = Target(1).Options with { CropLeft = 37, CropTop = 80, ControlAudio = false,
                Transform = new(.1, .2, .7, .6) };
            var saved = new Cue { Title = "Mi video", Kind = CueKind.WindowCapture, Capture = original };
            var row = YouTubeDiscovery.Build([saved], [Target(9, started: 200)]).Single();
            Check(row.IsSaved && row.IsAvailable && row.Cue.Id == saved.Id, "Unique saved source did not reconnect");
            Check(row.Cue.Capture is { WindowHandle: 9, CropLeft: 37, CropTop: 80, ControlAudio: false }
                && row.Cue.Capture.Transform == original.Transform, "Rediscovery dropped the visual or audio settings");
            row.Cue.Capture!.CropLeft = 10;
            Check(saved.Capture!.WindowHandle == 1 && saved.Capture.CropLeft == 37, "Discovery or its draft mutated the saved cue");
        });

        Test("Ambiguous same-title windows remain independent and do not inherit another capture", () =>
        {
            var saved = new Cue { Kind = CueKind.WindowCapture, Title = "Guardada", Capture = Target(1).Options with { CropTop = 90 } };
            var rows = YouTubeDiscovery.Build([saved], [Target(8, started: 200), Target(9, started: 200)]);
            Check(rows.Count == 3 && !rows[0].IsAvailable, "Ambiguous saved source selected a window automatically");
            Check(rows.Skip(1).All(row => !row.IsSaved && row.Cue.Capture!.CropTop == 0), "A different window inherited the saved crop");
            Check(rows.Skip(1).Select(row => row.Key).Distinct().Count() == 2, "Duplicate titles share a row identity");
        });

        Test("An exact browser lifetime matches its own window after its title changes", () =>
        {
            var saved = new Cue { Kind = CueKind.WindowCapture, Title = "Mi captura", Capture = Target(1).Options with { CropTop = 50 } };
            var row = YouTubeDiscovery.Build([saved], [Target(1, "Otro video - YouTube")]).Single();
            Check(row.IsAvailable && row.Cue.Capture is { CropTop: 50, WindowTitle: "Otro video - YouTube" }, "Exact identity was lost after a title change");
            Check(!YouTubeDiscovery.Build([saved], [Target(1, "Otra página")]).Single().IsAvailable,
                "A saved YouTube row silently captured a non-YouTube tab");
        });

        Test("Two saved captures with the same title never guess which restarted window belongs to them", () =>
        {
            var first = new Cue { Kind = CueKind.WindowCapture, Capture = Target(1).Options with { CropTop = 30 } };
            var second = new Cue { Kind = CueKind.WindowCapture, Capture = Target(2).Options with { CropTop = 80 } };
            var rows = YouTubeDiscovery.Build([first, second], [Target(8, started: 200)]);
            Check(rows.Count == 3 && rows.Take(2).All(row => !row.IsAvailable), "A restarted window inherited an ambiguous saved preset");
            Check(rows.Last().IsAvailable && !rows.Last().IsSaved, "The unambiguous physical window cannot be selected explicitly");
        });

        Test("A browser changing away from YouTube never redirects its saved capture to another browser window", () =>
        {
            var saved = new Cue { Kind = CueKind.WindowCapture, Capture = Target(1).Options with { CropTop = 75 } };
            var rows = YouTubeDiscovery.Build([saved], [Target(1, "Correo - Google Chrome"), Target(9)]);
            Check(rows.Count == 2 && !rows[0].IsAvailable && rows[1].Cue.Capture!.WindowHandle == 9 && !rows[1].IsSaved,
                "A different YouTube window inherited the original window's saved capture");
        });

        Test("Capture migration moves library sources independently of the order and survives reload", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "CultoYouTubeSmoke-" + Guid.NewGuid().ToString("N"));
            var previous = Environment.GetEnvironmentVariable("CULTO_DATA_DIR");
            try
            {
                Environment.SetEnvironmentVariable("CULTO_DATA_DIR", directory);
                var store = new WorkspaceStore();
                var capture = new Cue { Kind = CueKind.WindowCapture, Title = "Captura guardada", Capture = Target(1).Options with { CropTop = 65, Transform = new(0, .1, .8, .8) } };
                store.Save(new WorkspaceData { Plan = new ServicePlan { Items = [capture] }, Library = [capture, new Cue { Title = "Himno" }] });
                var workspace = store.Load();
                Check(workspace.Library.Count == 1 && workspace.YouTubeSources.Count == 1, "Capture stayed in the general library");
                var migrated = workspace.YouTubeSources.Single();
                Check(migrated.Id == capture.Id && migrated.Capture!.Transform == capture.Capture.Transform, "Migration lost settings or identity");
                migrated.Capture!.CropTop = 10;
                Check(workspace.Plan.Items.Single().Capture!.CropTop == 65, "Editing the migrated source changed the order");
                store.Save(workspace);
                Check(store.Load().YouTubeSources is [{ Capture.CropTop: 10 }], "Saved YouTube sources did not survive reload");
            }
            finally
            {
                Environment.SetEnvironmentVariable("CULTO_DATA_DIR", previous);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
        Console.WriteLine($"YouTube source smoke: {failures} failure(s)");
        return failures == 0 ? 0 : 1;
    }
}
