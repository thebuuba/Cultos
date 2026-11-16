using System.IO;
using System.Text.Json;
using Culto.Core;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static int RunBundledContentTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "CultoBundle-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "Otra PC", "App");
        var content = Path.Combine(app, "Content");
        var profile = Path.Combine(root, "Profile");
        Directory.CreateDirectory(content);
        try
        {
            File.WriteAllText(Path.Combine(content, "001 Himno.mkv"), "Synthetic fixture");
            File.WriteAllText(Path.Combine(content, "Fondo.png"), "Synthetic fixture");
            JsonFileStore.Save(Path.Combine(content, "version.json"), new BibleDocument
                { Name = "Biblia incluida", Verses = [new("Juan", 3, 16, "Texto de prueba")] });
            var manifest = new BundledContent.Manifest { SchemaVersion = 1,
                Hymns = [new() { Number = 1, Title = "001 Himno de prueba", Path = "001 Himno.mkv" }],
                Bibles = ["version.json"], DefaultBible = "version.json", Backgrounds = ["Fondo.png"] };
            var manifestPath = Path.Combine(content, "content-manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
            Test("Full installation seeds portable hymn paths, Bibles and backgrounds without personal settings", () =>
            {
                var workspace = new WorkspaceData();
                BundledContent.Initialize(workspace, app, profile);
                var hymn = workspace.Library.Single();
                Check(hymn.HymnNumber == 1 && hymn.FilePath == Path.Combine(content, "001 Himno.mkv"), "Hymn did not use its installed path");
                Check(workspace.BackgroundImages.Single() == Path.Combine(content, "Fondo.png"), "Background points outside installed content");
                Check(new BibleVersionStore(profile).List().Single().Name == "Biblia incluida", "Bundled Bible was not installed");
                Check(JsonFileStore.Load<BibleDocument>(Path.Combine(profile, "biblia.json"))?.Name == "Biblia incluida", "Default Bible is not ready");
                Check(!workspace.FirstRunCompleted && workspace.ChurchName == "" && workspace.BackgroundImagePath is null,
                    "Bundled content bypasses welcome or carries church identity");
            });
            Test("Full content rejects escaping paths before touching the profile", () =>
            {
                manifest.Hymns[0].Path = "../outside.mkv";
                File.WriteAllText(Path.Combine(app, "outside.mkv"), "Not content");
                File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
                var rejected = false;
                var untouched = Path.Combine(root, "Untouched");
                try { BundledContent.Initialize(new WorkspaceData(), app, untouched); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected && !Directory.Exists(untouched), "Invalid manifest wrote profile data");
            });
            Test("Existing church profile stays intact and the lightweight distribution still works", () =>
            {
                var previousDirectory = Environment.GetEnvironmentVariable("CULTO_DATA_DIR");
                try
                {
                    Environment.SetEnvironmentVariable("CULTO_DATA_DIR", profile);
                    var store = new WorkspaceStore();
                    store.Save(new WorkspaceData { ChurchName = "Mi iglesia", Volume = 43,
                        Plan = new ServicePlan { Name = "Culto guardado" } });
                    var bytes = File.ReadAllBytes(Path.Combine(profile, "workspace.json"));
                    var loaded = store.Load();
                    Check(loaded.ChurchName == "Mi iglesia" && loaded.Volume == 43 && loaded.Plan.Name == "Culto guardado", "Update replaced church profile");
                    Check(bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(profile, "workspace.json"))), "Load rewrote existing profile");
                    var empty = new WorkspaceData();
                    BundledContent.Initialize(empty, Path.Combine(root, "Lightweight"), profile);
                    Check(empty.Library.Count == 0, "Missing bundle unexpectedly populated content");
                }
                finally { Environment.SetEnvironmentVariable("CULTO_DATA_DIR", previousDirectory); }
            });
            if (Environment.GetEnvironmentVariable("CULTO_BUNDLE_APPLICATION") is { Length: > 0 } actualApp)
            {
                Test("The full distribution opens all 613 hymns, 11 Bible versions and 8 backgrounds offline", () =>
                {
                    var actualProfile = Path.Combine(root, "FullPackageProfile");
                    var workspace = new WorkspaceData();
                    BundledContent.Initialize(workspace, actualApp, actualProfile);
                    Check(workspace.Library.Count == 613 && workspace.Library.All(cue => cue.HymnNumber.HasValue && File.Exists(cue.FilePath)),
                        "The shipped hymnal is incomplete or contains a broken path");
                    Check(workspace.Library.Select(cue => cue.HymnNumber).Distinct().Count() == 613, "The shipped hymnal has duplicate numbers");
                    Check(new BibleVersionStore(actualProfile).List().Count == 11, "The shipped Bible library is incomplete");
                    Check(workspace.BackgroundImages.Count == 8 && workspace.BackgroundImages.All(File.Exists), "The shipped backgrounds are incomplete");
                    Check(JsonFileStore.Load<BibleDocument>(Path.Combine(actualProfile, "biblia.json"))!.Name.Contains("1960"), "Default Bible is not RVR1960");
                    Check(workspace.Library.All(cue => cue.FilePath!.StartsWith(Path.GetFullPath(actualApp), StringComparison.OrdinalIgnoreCase)),
                        "The package contains a distributor-specific media path");
                });
            }
        }
        finally { Directory.Delete(root, true); }
        return failures == 0 ? 0 : 1;
    }
}
