using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Culto.Remote;
using Xunit;
namespace Culto.Remote.Tests;
public sealed class SplitChannelTests
{
    [Fact]
    public async Task OneShotModifierValidatesAtBoundaryAndDeduplicates()
    {
        var id = Guid.NewGuid().ToString();
        var received = new List<string>();
        await using var host = new RemoteHost();
        await host.StartAsync(0, () => new RemoteSnapshot("Culto", "Video", "Logo", false, false, 70, 0, 0, [],
            Order: [new(id, "Video", "Video")], Scenes: [new("logo", "Logo", "logo", true, true), new("youtube", "YouTube", "youtube", true, false)]),
            input => { received.Add(JsonSerializer.Serialize(input, new JsonSerializerOptions(JsonSerializerDefaults.Web))); return Task.FromResult(new CommandResult(true, "OK")); }, true);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{host.Port}") };
        async Task<HttpStatusCode> Post(string json) => (await http.PostAsync("/api/command", new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode;
        foreach (var action in new[] { "project", "scene", "cue", "library" })
        {
            var extra = action == "scene" ? ",\"buttonId\":\"logo\"" : action is "cue" or "library" ? $",\"cueId\":\"{id}\"" : "";
            var json = $"{{\"action\":\"{action}\",\"requestId\":\"{action}\",\"preserveOther\":true{extra}}}";
            Assert.Equal(HttpStatusCode.OK, await Post(json));
            Assert.Equal(HttpStatusCode.OK, await Post(json));
        }
        Assert.Equal(4, received.Count);
        Assert.All(received, json => Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("preserveOther").GetBoolean()));
        Assert.Equal(HttpStatusCode.OK, await Post("""{"action":"return-media","requestId":"return"}"""));
        Assert.Equal(HttpStatusCode.OK, await Post("""{"action":"project","requestId":"false","preserveOther":false}"""));
        foreach (var json in new[] {
            """{"action":"volume","requestId":"bad","value":50,"preserveOther":true}""",
            """{"action":"seek","requestId":"bad","value":0.5,"preserveOther":false}""",
            """{"action":"deck","requestId":"bad","buttonId":"logo","preserveOther":true}""",
            """{"action":"scene","requestId":"bad","buttonId":"youtube","preserveOther":true}""",
            """{"action":"return-media","requestId":"bad","preserveOther":true}""",
            """{"action":"return-media","requestId":"bad","buttonId":"logo"}""",
            """{"action":"return-media","requestId":"bad","value":1}""",
            $"{{\"action\":\"return-media\",\"requestId\":\"bad\",\"cueId\":\"{id}\"}}",
            """{"action":"project","requestId":"bad","preserveOther":"true"}""",
            """{"action":"project","requestId":"bad","preserveOther":true,"path":"C:/private"}"""
        }) Assert.Equal(HttpStatusCode.BadRequest, await Post(json));
        Assert.Equal(6, received.Count);
    }
}
