using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Culto.Core;

namespace Culto.Desktop.Services;

public sealed record LocalMediaScan(IReadOnlyList<Cue> Items, string? Error);

public static class LocalMediaFolders
{
    private static readonly Guid DownloadsFolder = new("374DE290-123F-4565-9164-39C4925E467B");

    public static string GetPath(int filter) => filter switch
    {
        2 => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos, Environment.SpecialFolderOption.DoNotVerify),
        3 => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures, Environment.SpecialFolderOption.DoNotVerify),
        6 => GetDownloadsPath(),
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    public static LocalMediaScan Scan(string path, CueKind? kind = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path))
            return new([], "Windows no tiene una ubicación configurada para esta carpeta.");

        string root;
        try
        {
            root = Path.GetFullPath(path);
            if (!Directory.Exists(root))
                return new([], "La carpeta no está disponible o no se puede acceder a ella.");
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return new([], "La carpeta no está disponible o no se puede acceder a ella.");
        }

        var items = new List<Cue>();
        var pending = new Stack<string>();
        pending.Push(root);
        string? error = null;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            // Junctions and symbolic links can loop or lead outside the chosen source.
                            // OneDrive cloud placeholders also have ReparsePoint; they are not links.
                            if ((attributes & FileAttributes.ReparsePoint) == 0 || new DirectoryInfo(entry).LinkTarget is null)
                                pending.Push(entry);
                            continue;
                        }

                        if (!MediaLibrary.IsSupported(entry)) continue;
                        var cue = MediaLibrary.FromFile(entry);
                        if (kind is not null && cue.Kind != kind) continue;
                        cue.HymnNumber = null;
                        cue.Id = StableId(cue.FilePath!);
                        items.Add(cue);
                    }
                    catch (Exception ex) when (IsFileSystemError(ex))
                    {
                        error ??= "Algunos archivos o subcarpetas no se pudieron leer.";
                    }
                }
            }
            catch (Exception ex) when (IsFileSystemError(ex))
            {
                if (string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
                    return new(items, "La carpeta no está disponible o no se puede acceder a ella.");
                error ??= "Algunos archivos o subcarpetas no se pudieron leer.";
            }
        }

        items.Sort((left, right) =>
        {
            var titleOrder = StringComparer.OrdinalIgnoreCase.Compare(left.Title, right.Title);
            return titleOrder != 0 ? titleOrder : StringComparer.OrdinalIgnoreCase.Compare(left.FilePath, right.FilePath);
        });
        return new(items, error);
    }

    private static Guid StableId(string path)
    {
        var normalized = Path.GetFullPath(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static bool IsFileSystemError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;

    private static string GetDownloadsPath()
    {
        var folder = DownloadsFolder;
        var pointer = IntPtr.Zero;
        try
        {
            // DONT_VERIFY returns the configured location even when an external drive is offline.
            var result = SHGetKnownFolderPath(ref folder, 0x4000, IntPtr.Zero, out pointer);
            return result >= 0 ? Marshal.PtrToStringUni(pointer) ?? "" : "";
        }
        finally
        {
            if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
