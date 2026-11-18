using System.IO;
using Culto.Core;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunLocalMediaFolderServiceTests()
    {
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "CultoFolderScan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "Subcarpeta"));
        try
        {
            File.WriteAllBytes(Path.Combine(fixtureRoot, "012 vacaciones.MP4"), []);
            File.WriteAllBytes(Path.Combine(fixtureRoot, "003 música.mp3"), []);
            File.WriteAllBytes(Path.Combine(fixtureRoot, "Subcarpeta", "Foto.PNG"), []);
            File.WriteAllText(Path.Combine(fixtureRoot, "Ignorar.txt"), "unsupported");

            Test("PC folder scan includes nested supported media without assigning hymn numbers", () =>
            {
                var result = LocalMediaFolders.Scan(fixtureRoot);
                Check(result.Error is null && result.Items.Count == 3, "Scan lost supported media or included an unsupported file");
                Check(result.Items.All(c => c.HymnNumber is null), "Ordinary numbered filenames became hymns");
                Check(result.Items.Any(c => c.Kind == CueKind.Image && c.Title == "Foto"), "Nested image was not found");
                Check(result.Items.All(c => c.FilePath is not null && Path.IsPathFullyQualified(c.FilePath)), "Media paths are not absolute");
            });

            Test("PC folder scan filters media kinds and keeps cue identities across refreshes", () =>
            {
                var first = LocalMediaFolders.Scan(fixtureRoot);
                var repeated = LocalMediaFolders.Scan(Path.Combine(fixtureRoot, "."));
                Check(first.Items.Select(c => c.Id).SequenceEqual(repeated.Items.Select(c => c.Id)), "Refresh changed IDs for the same media paths");
                Check(first.Items.Select(c => c.Id).Distinct().Count() == 3, "Different media files share an ID");
                var videos = LocalMediaFolders.Scan(fixtureRoot, CueKind.Video);
                Check(videos.Items.Count == 1 && videos.Items[0].Kind == CueKind.Video, "Videos filter includes other kinds");
                Check(videos.Items[0].Id == first.Items.Single(c => c.Kind == CueKind.Video).Id, "Filtering changes a cue ID");
                var images = LocalMediaFolders.Scan(fixtureRoot, CueKind.Image);
                Check(images.Items.Count == 1 && images.Items[0].Kind == CueKind.Image, "Images filter includes other kinds");
            });

            Test("Folder discovery publishes usable files before finishing its recursive scan", () =>
            {
                var snapshots = new List<LocalMediaScan>();
                var result = LocalMediaFolders.Scan(fixtureRoot, progress: snapshots.Add);
                Check(snapshots.Count > 0 && snapshots[0].Items.Count > 0
                    && snapshots[0].Items.Count < result.Items.Count, "Folder stayed empty until the full scan finished");
                using var cancellation = new CancellationTokenSource();
                var canceled = false;
                try { LocalMediaFolders.Scan(fixtureRoot, cancellationToken: cancellation.Token, progress: _ => cancellation.Cancel()); }
                catch (OperationCanceledException) { canceled = true; }
                Check(canceled, "Cancellation after partial discovery did not stop the scan");
            });

            Test("PC folder scan distinguishes unavailable folders from empty folders and accepts cancellation", () =>
            {
                var empty = Path.Combine(fixtureRoot, "Vacia");
                Directory.CreateDirectory(empty);
                var emptyResult = LocalMediaFolders.Scan(empty);
                Check(emptyResult.Items.Count == 0 && emptyResult.Error is null, "Empty available folder reported as unavailable");
                var missing = LocalMediaFolders.Scan(Path.Combine(fixtureRoot, "Inexistente"));
                Check(missing.Items.Count == 0 && missing.Error is not null, "Missing folder silently appears empty");
                Check(LocalMediaFolders.Scan("").Error is not null, "Unconfigured folder silently appears empty");
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                var observedCancellation = false;
                try { LocalMediaFolders.Scan(fixtureRoot, cancellationToken: canceled.Token); }
                catch (OperationCanceledException) { observedCancellation = true; }
                Check(observedCancellation, "Superseded scans cannot be canceled");
            });
            Test("Every media picker format is discovered in nested Windows folders", () =>
            {
                var formats = Path.Combine(fixtureRoot, "Formatos");
                Directory.CreateDirectory(Path.Combine(formats, "Nested"));
                foreach (var (patterns, kind) in new[] { (MediaLibrary.AudioPatterns, CueKind.Audio),
                    (MediaLibrary.VideoPatterns, CueKind.Video), (MediaLibrary.ImagePatterns, CueKind.Image) })
                {
                    var expected = patterns.Split(';').Select(pattern => Path.Combine(formats, "Nested", "archivo" + pattern[1..])).ToArray();
                    foreach (var file in expected) File.WriteAllBytes(file, []);
                    var result = LocalMediaFolders.Scan(formats, kind);
                    Check(result.Error is null && result.Items.Select(cue => cue.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected),
                        "Folder scan omitted a selectable " + kind + " format");
                }
            });
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }
}
