using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Culto.Remote;
using Xunit;

namespace Culto.Remote.Tests;

public sealed class RemoteSceneTests
{
    // Deserialize the public wire format to exercise old/new snapshot compatibility.
    private static RemoteSnapshot State() => JsonSerializer.Deserialize<RemoteSnapshot>("""
        {"PlanName":"Culto","PreparedTitle":"","LiveTitle":"","IsBlackout":false,"IsPaused":false,
         "Volume":70,"Position":0,"Duration":0,"Buttons":[],
         "Scenes":[{"Id":"youtube","Label":"YouTube","Kind":"youtube","Available":true,"Active":false,"Detail":"Video en Chrome"},
                   {"Id":"image","Label":"Imagen","Kind":"image","Available":false,"Active":false}]}
        """)!;

    [Fact]
    public async Task PublishedSceneDispatchesOnceAndUnavailableOrUnknownSceneNeverDispatches()
    {
        var calls = 0;
        await using var host = new RemoteHost();
        await host.StartAsync(0, State, input => { calls++; return Task.FromResult(new CommandResult(true, input.ButtonId!)); }, true);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{host.Port}") };
        var command = new RemoteCommand("scene", "youtube-once", ButtonId: "youtube");
        var response = await client.PostAsJsonAsync("/api/command", command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<CommandResult>())!.Success);
        await client.PostAsJsonAsync("/api/command", command);
        Assert.Equal(1, calls);
        foreach (var id in new[] { "image", "missing", "C:/private.html" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/command",
                command with { RequestId = Guid.NewGuid().ToString("N"), ButtonId = id })).StatusCode);
        Assert.Equal(1, calls);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/state"));
        Assert.Equal("youtube", json.RootElement.GetProperty("scenes")[0].GetProperty("id").GetString());
    }
}
