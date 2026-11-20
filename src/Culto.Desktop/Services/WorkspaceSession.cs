using System.IO;

namespace Culto.Desktop.Services;

// Held for the whole application lifetime, independent of the executable's folder.
internal static class WorkspaceSession
{
    public static FileStream Open(string directory)
    {
        Directory.CreateDirectory(directory);
        return new FileStream(Path.Combine(directory, "workspace.session.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
}
