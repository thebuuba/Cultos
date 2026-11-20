using Culto.Desktop.Services;
using Xunit;

namespace Culto.Core.Tests;

public sealed class WorkspaceSessionTests
{
    [Fact]
    public void OnlyOneSessionCanUseAProfileAndClosingReleasesIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CultoSessionTest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var workspace = Path.Combine(directory, "workspace.json");
            File.WriteAllText(workspace, "saved custom settings");
            using (var first = WorkspaceSession.Open(directory))
            {
                Assert.Throws<IOException>(() => { using var second = WorkspaceSession.Open(directory); });
                using var independent = WorkspaceSession.Open(Path.Combine(directory, "other"));
                Assert.Equal("saved custom settings", File.ReadAllText(workspace));
            }
            using var reopened = WorkspaceSession.Open(directory);
            Assert.Equal("saved custom settings", File.ReadAllText(workspace));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
