using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Culto.Remote;
using Xunit;

namespace Culto.Remote.Tests;

public sealed class RemoteTests
{
    private const string FirstCueId = "f5caec32-38ea-4f5e-9074-6359fc995456";
    private const string SecondCueId = "1081838f-6514-45c0-92da-6cb12ca45f18";

    private static RemoteSnapshot OrderSnapshot() =>
        new("Culto de prueba", "Lectura", "Bienvenida", false, false, 70, 3, 10, [],
            [new(FirstCueId, "Bienvenida", "Text"), new(SecondCueId, "Lectura", "Scripture")],
            FirstCueId, SecondCueId, HasOutput: true, IsBackground: true, HasExternalOutput: true);

    private static RemoteSnapshot Snapshot(string live = "Sin salida") =>
        new("Culto de prueba", "Bienvenida", live, false, true, 70, 3, 10,
            [new DeckButton("welcome", "Bienvenida", "#43D9BE", "project", "cue-1")]);

    private static async Task<(RemoteHost Host, HttpClient Client)> Start(
        Func<RemoteCommand, Task<CommandResult>>? command = null, Func<RemoteSnapshot>? snapshot = null)
    {
        var host = new RemoteHost();
        await host.StartAsync(0, snapshot ?? (() => Snapshot()), command ?? (_ => Task.FromResult(new CommandResult(true, "Listo"))), true);
        var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{host.Port}") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        return (host, client);
    }

    [Fact]
    public async Task StatePublishesOrderedCuesAndProjectionStatus()
    {
        var (host, client) = await Start(snapshot: OrderSnapshot);
        await using var cleanup = host; using var http = client;
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/state"));
        var state = json.RootElement;
        Assert.True(state.TryGetProperty("order", out var order));
        Assert.Equal(2, order.GetArrayLength());
        Assert.Equal(FirstCueId, order[0].GetProperty("id").GetString());
        Assert.Equal("Bienvenida", order[0].GetProperty("title").GetString());
        Assert.Equal("Text", order[0].GetProperty("kind").GetString());
        Assert.Equal(SecondCueId, order[1].GetProperty("id").GetString());
        Assert.Equal("Lectura", order[1].GetProperty("title").GetString());
        Assert.Equal("Scripture", order[1].GetProperty("kind").GetString());
        Assert.Equal(FirstCueId, state.GetProperty("liveCueId").GetString());
        Assert.Equal(SecondCueId, state.GetProperty("preparedCueId").GetString());
        Assert.True(state.GetProperty("hasOutput").GetBoolean());
        Assert.True(state.GetProperty("isBackground").GetBoolean());
        Assert.True(state.GetProperty("hasExternalOutput").GetBoolean());
        Assert.Equal(new[] { "id", "kind", "title" }, order[0].EnumerateObject().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task CueSelectionDispatchesAndConcurrentRetriesExecuteOnce()
    {
        var calls = 0;
        RemoteCommand? received = null;
        var (host, client) = await Start(input =>
        {
            Interlocked.Increment(ref calls); received = input;
            return Task.FromResult(new CommandResult(true, "Proyectado"));
        }, OrderSnapshot);
        await using var cleanup = host; using var http = client;
        var payload = new RemoteCommand("cue", "select-cue", CueId: SecondCueId);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/command", payload)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, calls);
        Assert.Equal(payload, received);
    }

    [Fact]
    public async Task DeletedCueIsRejectedBeforeDispatch()
    {
        var calls = 0;
        var current = OrderSnapshot();
        var (host, client) = await Start(_ =>
        {
            calls++; return Task.FromResult(new CommandResult(true, "Proyectado"));
        }, () => current);
        await using var cleanup = host; using var http = client;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/command", new { action = "cue", requestId = "before-delete", cueId = FirstCueId })).StatusCode);
        current = Snapshot();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/command", new { action = "cue", requestId = "after-delete", cueId = FirstCueId })).StatusCode);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    [InlineData("not-a-guid", null, null)]
    [InlineData("00000000-0000-0000-0000-000000000000", null, null)]
    [InlineData("44e07557-2287-477c-ad7d-f4d31d15d702", null, null)]
    [InlineData(FirstCueId, "welcome", null)]
    [InlineData(FirstCueId, null, 0.5)]
    public async Task InvalidCueSelectionsNeverReachCallback(string? cueId, string? buttonId, double? value)
    {
        var calls = 0;
        var (host, client) = await Start(_ => { calls++; return Task.FromResult(new CommandResult(true, "No ejecutar")); }, OrderSnapshot);
        await using var cleanup = host; using var http = client;
        var response = await client.PostAsJsonAsync("/api/command", new { action = "cue", requestId = "invalid-cue", cueId, buttonId, value });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("project", null, null)]
    [InlineData("next", null, null)]
    [InlineData("previous", null, null)]
    [InlineData("blackout", null, null)]
    [InlineData("background", null, null)]
    [InlineData("pause", null, null)]
    [InlineData("restart", null, null)]
    [InlineData("volume", null, 65.0)]
    [InlineData("seek", null, 0.5)]
    [InlineData("deck", "welcome", null)]
    public async Task OtherActionsRejectCueId(string action, string? buttonId, double? value)
    {
        var calls = 0;
        var (host, client) = await Start(_ => { calls++; return Task.FromResult(new CommandResult(true, "No ejecutar")); });
        await using var cleanup = host; using var http = client;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/command", new { action, requestId = "extra-cue", buttonId, value, cueId = FirstCueId })).StatusCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CueSelectionRequiresAuthenticationAndSameOrigin()
    {
        var calls = 0;
        var (host, client) = await Start(_ => { calls++; return Task.FromResult(new CommandResult(true, "No ejecutar")); }, OrderSnapshot);
        await using var cleanup = host; using var http = client;
        var payload = new { action = "cue", requestId = "protected-cue", cueId = FirstCueId };
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/command", payload)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        client.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/command", payload)).StatusCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task StateAndCommandsRequireBearerToken()
    {
        var (host, client) = await Start(); await using var cleanup = host; using var http = client;
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/command", new RemoteCommand("next", "one"))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "incorrecto");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
    }

    [Fact]
    public async Task CallbackUpdatesPublicStateAndDuplicateOnlyExecutesOnce()
    {
        var calls = 0;
        var (host, client) = await Start(_ => { Interlocked.Increment(ref calls); return Task.FromResult(new CommandResult(true, "Proyectado")); }, () => Snapshot(calls == 0 ? "Sin salida" : "Bienvenida"));
        await using var cleanup = host; using var http = client;
        var command = new RemoteCommand("project", "same-request");
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/command", command)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, calls);
        var state = await client.GetFromJsonAsync<RemoteSnapshot>("/api/state");
        Assert.Equal("Bienvenida", state!.LiveTitle);
        var body = await client.GetStringAsync("/api/state");
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("planName", out _));
        Assert.False(json.RootElement.TryGetProperty("filePath", out _));
        Assert.False(json.RootElement.TryGetProperty("notes", out _));
    }

    [Theory]
    [InlineData("{\"action\":\"shell\",\"requestId\":\"1\"}")]
    [InlineData("{\"action\":\"cue\",\"requestId\":\"1\"}")]
    [InlineData("{\"action\":\"next\",\"requestId\":\"\"}")]
    [InlineData("{\"action\":\"volume\",\"requestId\":\"1\",\"value\":101}")]
    [InlineData("{\"action\":\"seek\",\"requestId\":\"1\",\"value\":-0.1}")]
    [InlineData("{\"action\":\"deck\",\"requestId\":\"1\",\"buttonId\":\"missing\"}")]
    [InlineData("{\"action\":\"next\",\"requestId\":\"1\",\"path\":\"secret\"}")]
    [InlineData("{broken")]
    [InlineData("null")]
    public async Task InvalidCommandsNeverReachCallback(string body)
    {
        var calls = 0;
        var (host, client) = await Start(_ => { calls++; return Task.FromResult(new CommandResult(true, "Listo")); });
        await using var cleanup = host; using var http = client;
        var response = await client.PostAsync("/api/command", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://127.0.0.1:1")]
    [InlineData("http://localhost")]
    [InlineData("null")]
    public async Task DifferentOriginIsForbiddenEvenWithToken(string origin)
    {
        var (host, client) = await Start(); await using var cleanup = host; using var http = client;
        client.DefaultRequestHeaders.Add("Origin", origin);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/command", new RemoteCommand("next", "one"))).StatusCode);
    }

    [Fact]
    public async Task MatchingOriginIsAcceptedButCrossSiteFetchIsForbidden()
    {
        var (host, client) = await Start(); await using var cleanup = host; using var http = client;
        client.DefaultRequestHeaders.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/state")).StatusCode);
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/state")).StatusCode);
    }

    [Fact]
    public async Task OversizedBodyIsRejected()
    {
        var (host, client) = await Start(); await using var cleanup = host; using var http = client;
        var response = await client.PostAsync("/api/command", new StringContent(new string(' ', 17000), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task MobilePageIsAvailableWithoutTokenAndTokenIsRandomPerSession()
    {
        var (host, client) = await Start(); await using var cleanup = host; using var http = client;
        await using var second = new RemoteHost();
        Assert.NotEqual(host.Token, second.Token);
        Assert.True(host.Token.Length >= 43);
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain(host.Token, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CallbackExceptionsDoNotDisclosePrivateDetailsOrRepeatCommand()
    {
        var calls = 0;
        var (host, client) = await Start(_ => { calls++; throw new InvalidOperationException("C:\\private\\notes.txt"); });
        await using var cleanup = host; using var http = client;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/command", new RemoteCommand("next", "fail-once"));
            var result = await response.Content.ReadFromJsonAsync<CommandResult>();
            Assert.False(result!.Success);
            Assert.DoesNotContain("private", result.Message);
        }
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CommandsAreSerializedWithoutBlockingStateReads()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var concurrent = 0;
        var (host, client) = await Start(async _ =>
        {
            if (Interlocked.Increment(ref calls) > 1) Interlocked.Increment(ref concurrent);
            entered.TrySetResult();
            await release.Task;
            Interlocked.Decrement(ref calls);
            return new CommandResult(true, "Listo");
        });
        await using var cleanup = host; using var http = client;
        var first = client.PostAsJsonAsync("/api/command", new RemoteCommand("next", "first"));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = client.PostAsJsonAsync("/api/command", new RemoteCommand("previous", "second"));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/state").WaitAsync(TimeSpan.FromSeconds(3))).StatusCode);
            release.TrySetResult();
            await Task.WhenAll(first, second);
            Assert.Equal(0, concurrent);
        }
        finally { release.TrySetResult(); }
    }

    [Theory]
    [InlineData("project", null, null)]
    [InlineData("next", null, null)]
    [InlineData("previous", null, null)]
    [InlineData("blackout", null, null)]
    [InlineData("background", null, null)]
    [InlineData("pause", null, null)]
    [InlineData("restart", null, null)]
    [InlineData("volume", null, 65.0)]
    [InlineData("seek", null, 0.5)]
    [InlineData("deck", "welcome", null)]
    public async Task ValidCommandsArriveAtCallback(string action, string? buttonId, double? value)
    {
        RemoteCommand? received = null;
        var (host, client) = await Start(input => { received = input; return Task.FromResult(new CommandResult(true, "Listo")); });
        await using var cleanup = host; using var http = client;
        var sent = new RemoteCommand(action, "valid-command", buttonId, value);
        var response = await client.PostAsJsonAsync("/api/command", sent);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(sent, received);
    }

    [Fact]
    public async Task ConfiguredCueDeckTargetReachesCallbackAndDeduplicates()
    {
        var calls = 0;
        RemoteCommand? received = null;
        var button = new DeckButton("local-content", "Contenido local", "#43D9BE", "cue", "f5caec32-38ea-4f5e-9074-6359fc995456");
        var (host, client) = await Start(input =>
        {
            calls++; received = input;
            return Task.FromResult(new CommandResult(true, "Contenido proyectado"));
        }, () => Snapshot() with { Buttons = [button] });
        await using var cleanup = host; using var http = client;
        var command = new RemoteCommand("deck", "content-request", button.Id);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/command", command);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<CommandResult>())!.Success);
        }
        Assert.Equal(command, received);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("cue", null)]
    [InlineData("cue", "")]
    [InlineData("cue", "not-a-local-id")]
    [InlineData("cue", "00000000-0000-0000-0000-000000000000")]
    [InlineData("shell", "f5caec32-38ea-4f5e-9074-6359fc995456")]
    [InlineData("deck", "f5caec32-38ea-4f5e-9074-6359fc995456")]
    public async Task InvalidConfiguredDeckTargetsNeverReachCallback(string action, string? cueId)
    {
        var calls = 0;
        var button = new DeckButton("invalid-target", "Inválido", "#43D9BE", action, cueId);
        var (host, client) = await Start(_ =>
        {
            calls++; return Task.FromResult(new CommandResult(true, "No ejecutar"));
        }, () => Snapshot() with { Buttons = [button] });
        await using var cleanup = host; using var http = client;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/command", new RemoteCommand("deck", "invalid-target-request", button.Id))).StatusCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ChunkedOversizedBodyIsAlsoRejected()
    {
        var (host, client) = await Start(); await using var cleanup = host; using var http = client;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/command");
        request.Headers.TransferEncodingChunked = true;
        request.Content = new StringContent("{\"action\":\"" + new string('x', 17000) + "\"}", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task DisposingHostClosesListeningSocket()
    {
        var (host, client) = await Start(); using var http = client;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/state")).StatusCode);
        await host.DisposeAsync();
        await host.DisposeAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/api/state"));
    }
}
