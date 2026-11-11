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

namespace Culto.Remote;

public sealed class RemoteHost : IAsyncDisposable
{
    private WebApplication? app;
    private readonly SemaphoreSlim commands = new(1, 1);
    private readonly Dictionary<string, CommandResult> completed = new(StringComparer.Ordinal);
    private readonly Queue<string> completedOrder = new();
    private bool started;
    private bool disposed;
    private const int BodyLimit = 16 * 1024;
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
        { "project", "next", "previous", "blackout", "background", "pause", "restart", "volume", "seek", "deck" };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public int Port { get; private set; }

    public async Task StartAsync(int port, Func<RemoteSnapshot> snapshot,
        Func<RemoteCommand, Task<CommandResult>> command, bool loopbackOnly = false)
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
        app.MapGet("/api/state", () => Results.Json(snapshot(), Json));
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
                if (input.Action == "deck" && !snapshot().Buttons.Any(button =>
                        button.Id == input.ButtonId && (button.Action == "cue"
                            ? Guid.TryParse(button.CueId, out var cueId) && cueId != Guid.Empty
                            : Actions.Contains(button.Action) && button.Action != "deck")))
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
            || input.RequestId.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':'))) return false;
        return input.Action switch
        {
            "volume" => input.ButtonId is null && input.Value is >= 0 and <= 100,
            "seek" => input.ButtonId is null && input.Value is >= 0 and <= 1,
            "deck" => input.Value is null && !string.IsNullOrWhiteSpace(input.ButtonId) && input.ButtonId.Length <= 128,
            _ => input.ButtonId is null && input.Value is null
        };
    }

    private static IResult InvalidCommand() => Results.Json(new CommandResult(false, "Comando no válido."), Json, statusCode: 400);

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (app is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await app.StopAsync(timeout.Token);
            await app.DisposeAsync();
            app = null;
        }
    }
}
