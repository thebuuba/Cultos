using System.Text.Json;

namespace Culto.Core;

public static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    // ponytail: un bloqueo para el almacén monousuario; dividir por ruta si aumenta la concurrencia.
    private static readonly object Gate = new();

    public static void Save<T>(string path, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (Gate)
        {
            path = Path.GetFullPath(path);
            var backup = path + ".bak";
            var validMain = false;
            if (File.Exists(path))
            {
                try { Read<T>(path); validMain = true; }
                catch (InvalidDataException)
                {
                    if (!File.Exists(backup)) throw;
                    Read<T>(backup); // No sustituir la única copia válida por el principal dañado.
                }
            }
            else if (File.Exists(backup)) Read<T>(backup);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, validMain ? backup : null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public static T? Load<T>(string path)
    {
        lock (Gate)
        {
            if (File.Exists(path))
            {
                try { return Read<T>(path); }
                catch (InvalidDataException) { if (!File.Exists(path + ".bak")) throw; }
            }
            return File.Exists(path + ".bak") ? Read<T>(path + ".bak") : default;
        }
    }

    private static T Read<T>(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options) ?? throw new InvalidDataException("El archivo JSON no contiene datos recuperables.");
        }
        catch (JsonException exception) { throw new InvalidDataException("El archivo JSON está dañado y no se pudo recuperar.", exception); }
    }
}
