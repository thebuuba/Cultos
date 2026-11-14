using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;

namespace Culto.Remote;

public sealed class RemoteHost : IAsyncDisposable
{
    private WebApplication? app;
    private readonly SemaphoreSlim commands = new(1, 1);
    private readonly Dictionary<string, CommandResult> completed = new(StringComparer.Ordinal);
    private readonly Queue<string> completedOrder = new();
    private readonly ConcurrentDictionary<string, (string Name, DateTimeOffset LastSeen)> clients = new();
    private bool started;
    private bool disposed;
    private const int BodyLimit = 16 * 1024;
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
        { "project", "next", "previous", "blackout", "background", "pause", "restart", "volume", "seek", "deck", "cue", "library", "scene" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public int Port { get; private set; }
    public IReadOnlyList<RemoteClientInfo> Clients => clients.Select(client => new RemoteClientInfo(
        client.Key, client.Value.Name, client.Value.LastSeen,
        DateTimeOffset.UtcNow - client.Value.LastSeen < TimeSpan.FromSeconds(15)))
        .OrderByDescending(client => client.LastSeen).ToArray();
    public int ConnectedClientCount => Clients.Count(client => client.IsConnected);

    public async Task StartAsync(int port, Func<RemoteSnapshot> snapshot,
        Func<RemoteCommand, Task<CommandResult>> command, bool loopbackOnly = false,
        Func<string, string, Task<IReadOnlyList<RemoteCue>>>? catalog = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) throw new InvalidOperationException("El mando ya está iniciado.");
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(command);
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        started = true;
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [], EnvironmentName = "Production", ApplicationName = typeof(RemoteHost).Assembly.FullName
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = BodyLimit;
            options.Limits.MaxConcurrentConnections = 32;
            options.Listen(loopbackOnly ? IPAddress.Loopback : IPAddress.Any, port);
        });
        app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            try
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    if (!Authenticated(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await context.Response.WriteAsJsonAsync(new CommandResult(false, "Vuelve a vincular el teléfono desde la consola."), Json);
                        return;
                    }
                    if (!SameOrigin(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new CommandResult(false, "Origen no permitido."), Json);
                        return;
                    }
                    RecordClient(context.Request);
                }
                await next(context);
            }
            catch (BadHttpRequestException error) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = error.StatusCode;
                await context.Response.WriteAsJsonAsync(new CommandResult(false, "Solicitud no válida o demasiado grande."), Json);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new CommandResult(false, "No se pudo completar la solicitud. Revisa la consola."), Json);
            }
        });
        using var stream = typeof(RemoteHost).Assembly.GetManifestResourceStream("Culto.Remote.wwwroot.index.html")
            ?? throw new InvalidOperationException("No se encontró la página del mando.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var page = await reader.ReadToEndAsync();
        app.MapGet("/", () => Results.Content(page, "text/html", Encoding.UTF8));
        foreach (var weight in new[] { "regular", "semibold" })
        {
            using var font = typeof(RemoteHost).Assembly.GetManifestResourceStream($"Culto.Remote.wwwroot.lexend-{weight}.ttf")
                ?? throw new InvalidOperationException("No se encontró la fuente del mando.");
            using var bytes = new MemoryStream(); font.CopyTo(bytes);
            var data = bytes.ToArray();
            app.MapGet($"/assets/lexend-{weight}.ttf", () => Results.File(data, "font/ttf"));
        }
        app.MapGet("/api/state", () => Results.Json(snapshot(), Json));
        app.MapGet("/api/catalog", async (HttpContext context) =>
        {
            var source = context.Request.Query["source"].ToString();
            var query = context.Request.Query["q"].ToString().Trim();
            if (source is not ("bible" or "hymns" or "videos") || query.Length > 200)
                return Results.BadRequest(new CommandResult(false, "Búsqueda no válida."));
            var results = catalog is null ? Array.Empty<RemoteCue>() : await catalog(source, query);
            return Results.Json(results.Take(100), Json);
        });
        app.MapPost("/api/command", async (HttpContext context) =>
        {
            if (context.Request.ContentLength > BodyLimit)
                return Results.Json(new CommandResult(false, "Solicitud demasiado grande."), Json, statusCode: 413);
            if (!context.Request.HasJsonContentType())
                return Results.Json(new CommandResult(false, "Se requiere una solicitud JSON."), Json, statusCode: 400);
            RemoteCommand? input;
            try { input = await context.Request.ReadFromJsonAsync<RemoteCommand>(Json, context.RequestAborted); }
            catch (JsonException) { return InvalidCommand(); }
            if (!ValidCommand(input)) return InvalidCommand();

            await commands.WaitAsync(context.RequestAborted);
            try
            {
                if (completed.TryGetValue(input!.RequestId, out var previous)) return Results.Json(previous, Json);
                if (input.Action == "scene" && snapshot().Scenes?.Any(scene => scene.Id == input.ButtonId && scene.Available) != true)
                    return InvalidCommand();
                if (input.Action == "cue" && snapshot().Order?.Any(cue =>
                        Guid.TryParse(cue.Id, out var cueId) && cueId == Guid.Parse(input.CueId!)) != true)
                    return InvalidCommand();
                if (input.Action == "deck" && !snapshot().Buttons.Any(button =>
                        button.Id == input.ButtonId && (button.Action == "cue"
                            ? Guid.TryParse(button.CueId, out var cueId) && cueId != Guid.Empty
                            : Actions.Contains(button.Action) && button.Action is not ("deck" or "cue" or "library" or "scene"))))
                    return InvalidCommand();
                CommandResult result;
                try { result = await command(input); }
                catch (Exception) { result = new CommandResult(false, "La acción no se completó. Revisa la consola."); }
                completed.Add(input.RequestId, result);
                completedOrder.Enqueue(input.RequestId);
                // Keep the most recent 1024 results for this single operator session.
                if (completedOrder.Count > 1024) completed.Remove(completedOrder.Dequeue());
                return Results.Json(result, Json);
            }
            finally { commands.Release(); }
        });
        try
        {
            await app.StartAsync();
            Port = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()).Port;
        }
        catch
        {
            await app.DisposeAsync();
            app = null;
            throw;
        }
    }

    private void RecordClient(HttpRequest request)
    {
        var header = request.Headers["X-Culto-Client"];
        if (header.Count != 1 || !Guid.TryParseExact(header[0], "N", out var id) || id == Guid.Empty) return;
        var now = DateTimeOffset.UtcNow;
        foreach (var client in clients.Where(client => now - client.Value.LastSeen > TimeSpan.FromHours(1)))
            clients.TryRemove(client.Key, out _);
        var key = id.ToString("N");
        if (clients.Count >= 64 && !clients.ContainsKey(key)) return;
        var agent = request.Headers.UserAgent.ToString();
        var name = agent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ? "iPhone"
            : agent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iPad"
            : agent.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Teléfono Android"
            : agent.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Computadora Windows"
            : agent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) ? "Mac"
            : "Navegador";
        clients[key] = (name, now);
    }

    private bool Authenticated(HttpRequest request)
    {
        var values = request.Headers.Authorization;
        if (values.Count != 1 || values[0] is not { } auth || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(auth[7..]), Encoding.UTF8.GetBytes(Token));
    }

    private static bool SameOrigin(HttpRequest request)
    {
        if (request.Headers["Sec-Fetch-Site"].Any(value => string.Equals(value, "cross-site", StringComparison.OrdinalIgnoreCase))) return false;
        var origins = request.Headers.Origin;
        if (origins.Count == 0) return true;
        return origins.Count == 1 && Uri.TryCreate(origins[0], UriKind.Absolute, out var origin)
            && origin.UserInfo.Length == 0 && origin.AbsolutePath == "/" && origin.Query.Length == 0 && origin.Fragment.Length == 0
            && Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var current)
            && origin.Scheme == current.Scheme && origin.Host.Equals(current.Host, StringComparison.OrdinalIgnoreCase) && origin.Port == current.Port;
    }

    private static bool ValidCommand(RemoteCommand? input)
    {
        if (input is null || input.Action is null || !Actions.Contains(input.Action)
            || string.IsNullOrWhiteSpace(input.RequestId) || input.RequestId.Length > 128
            || input.RequestId.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':'))
            || (input.Action is not ("cue" or "library") && input.CueId is not null)) return false;
        return input.Action switch
        {
            "cue" or "library" => input.ButtonId is null && input.Value is null
                && Guid.TryParse(input.CueId, out var cueId) && cueId != Guid.Empty,
            "volume" => input.ButtonId is null && input.Value is >= 0 and <= 100,
            "seek" => input.ButtonId is null && input.Value is >= 0 and <= 1,
            "deck" or "scene" => input.Value is null && !string.IsNullOrWhiteSpace(input.ButtonId) && input.ButtonId.Length <= 128,
            _ => input.ButtonId is null && input.Value is null
        };
    }

    private static IResult InvalidCommand() => Results.Json(new CommandResult(false, "Comando no válido."), Json, statusCode: 400);

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        clients.Clear();
        if (app is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await app.StopAsync(timeout.Token);
            await app.DisposeAsync();
            app = null;
        }
    }
}
