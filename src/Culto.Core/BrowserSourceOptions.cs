namespace Culto.Core;

public sealed record BrowserSourceOptions
{
    public string Location { get; init; } = "";
    public bool IsLocalFile { get; init; }
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public int FramesPerSecond { get; init; } = 30;
    public string CustomCss { get; init; } = "body { background: transparent; margin: 0; overflow: hidden; }";
    public bool AudioEnabled { get; init; }

    public BrowserSourceOptions Normalize()
    {
        if (Width is < 64 or > 4096 || Height is < 64 or > 4096)
            throw new ArgumentException("El ancho y el alto deben estar entre 64 y 4096 píxeles.");
        if (FramesPerSecond is < 1 or > 60)
            throw new ArgumentException("Los FPS deben estar entre 1 y 60.");

        var location = (Location ?? "").Trim();
        if (string.IsNullOrEmpty(location))
            throw new ArgumentException("Indica una URL HTTP/HTTPS o un archivo HTML local.");
        if (IsLocalFile)
        {
            if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && !Path.IsPathRooted(location))
                throw new ArgumentException("Selecciona una ruta de archivo HTML local, no una URL.");
            var extension = Path.GetExtension(location);
            if (!extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("El archivo local debe tener extensión .html o .htm.");
            try { location = Path.GetFullPath(location); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new ArgumentException("La ruta del archivo HTML local no es válida.", exception);
            }
        }
        else if (!Uri.TryCreate(location, UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(address.Host))
            throw new ArgumentException("La URL debe ser una dirección HTTP o HTTPS válida.");

        return this with { Location = location, CustomCss = CustomCss ?? "" };
    }

    public void Validate()
    {
        var source = Normalize();
        if (source.IsLocalFile && !File.Exists(source.Location))
            throw new ArgumentException("El archivo HTML local no existe o no está disponible.");
    }

    public string GetAddress()
    {
        var source = Normalize();
        return source.IsLocalFile ? new Uri(source.Location).AbsoluteUri : source.Location;
    }
}
