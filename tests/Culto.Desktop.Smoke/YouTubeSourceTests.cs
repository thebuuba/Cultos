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

        Test("Only live YouTube windows appear, never saved video titles", () =>
        {
            var saved=new Cue {Kind=CueKind.WindowCapture,Title="Old video",Capture=Target(1).Options};
            Check(YouTubeDiscovery.Build([saved],[]).Count==0,"Saved capture remained without a live window");
            var layout=new WindowCaptureLayout(320,140,20,10,new(0,0,1,1));
            var rows=YouTubeDiscovery.Build([saved],[Target(9,"New video - YouTube",started:200)],layout);
            Check(rows.Count==1 && !rows[0].IsSaved && rows[0].Title=="New video - YouTube","Discovery retained a saved video row");
            Check(WindowCaptureLayout.From(rows[0].Cue.Capture!)==layout,"Automatic detection lost the shared crop");
            Check(rows[0].Cue.Capture!.WindowHandle==9,"Detection retained the old native window");
        });
        Test("Leaving YouTube removes only that live window and returning restores it", () =>
        {
            var saved=new Cue {Kind=CueKind.WindowCapture,Capture=Target(1).Options};
            var left=YouTubeDiscovery.Build([saved],[Target(1,"WhatsApp - Google Chrome"),Target(9)]);
            Check(left.Count==1 && left[0].Cue.Capture!.WindowHandle==9,"Non YouTube tab remained available");
            var returned=YouTubeDiscovery.Build([saved],[Target(1,"New song - YouTube"),Target(9)]);
            Check(returned.Count==2 && returned.All(r=>!r.IsSaved),"Returning to YouTube duplicated saved captures");
            Check(returned.Select(r=>r.Key).Distinct().Count()==2,"Distinct windows share their selection key");
        });
        Test("Shared layout keeps independent order crops", () =>
        {
            var cue=new Cue {Kind=CueKind.WindowCapture,Capture=Target(1).Options with {CropTop=70}};
            var layout=new WindowCaptureLayout(30,140);
            Check(YouTubeDiscovery.ApplyLayout(cue,layout).Capture!.CropTop==70,"Default layout replaced an explicit order crop");
            var uncropped=YouTubeDiscovery.Clone(cue);uncropped.Capture=uncropped.Capture! with {CropTop=0};
            Check(YouTubeDiscovery.ApplyLayout(uncropped,layout).Capture!.CropTop==140 && uncropped.Capture.CropTop==0,"Shared layout changed the original cue");
        });

        Test("Legacy crop saved only in the worship order becomes a reusable layout without changing the order", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "CultoYouTubeLayoutMigration-" + Guid.NewGuid().ToString("N"));
            var previous = Environment.GetEnvironmentVariable("CULTO_DATA_DIR");
            try
            {
                Environment.SetEnvironmentVariable("CULTO_DATA_DIR", directory);
                var store = new WorkspaceStore();
                var source = new Cue { Kind = CueKind.WindowCapture, Capture = Target(1).Options };
                var order = YouTubeDiscovery.Clone(source); order.Id = Guid.NewGuid();
                order.Capture = order.Capture! with { CropLeft = 320, CropTop = 140, Transform = new(0, 0, 1, 1) };
                store.Save(new WorkspaceData { YouTubeSources = [source], Plan = new ServicePlan { Items = [order] } });
                var reloaded = store.Load();
                Check(reloaded.YouTubeLayout == WindowCaptureLayout.From(order.Capture), "Legacy order cut was not recovered");
                Check(reloaded.Plan.Items[0].Id == order.Id && reloaded.Plan.Items[0].Capture == order.Capture
                    && reloaded.YouTubeSources[0].Capture!.CropLeft == 0, "Migration mutated an independent source or order item");
                var other = YouTubeDiscovery.Clone(order); other.Id = Guid.NewGuid(); other.Capture = other.Capture! with { CropTop = 250 };
                reloaded.YouTubeLayout = null; reloaded.Plan.Items.Add(other); store.Save(reloaded);
                Check(store.Load().YouTubeLayout is null, "Migration arbitrarily chose between different legacy cuts");
            }
            finally
            {
                Environment.SetEnvironmentVariable("CULTO_DATA_DIR", previous);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
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
