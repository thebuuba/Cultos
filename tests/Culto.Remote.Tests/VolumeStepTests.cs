using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Culto.Remote;
using Xunit;

namespace Culto.Remote.Tests;

public sealed class VolumeStepTests
{
    [Fact]
    public async Task VolumeStepsExecuteSeriallyAndDuplicateRequestExecutesOnce()
    {
        var volume = 50d; var calls = 0;
        await using var host = new RemoteHost();
        await host.StartAsync(0, () => new("Test", "", "", false, false, (int)volume, 0, 0, []), async command =>
        {
            calls++;
            var current = volume;
            await Task.Delay(20);
            volume = Math.Clamp(current + command.Value!.Value, 0, 100);
            return new(true, "Applied");
        }, loopbackOnly: true);
        using var http = Client(host);
        var up = new RemoteCommand("volume-step", "up-once", Value: 5);
        var responses = await Task.WhenAll(http.PostAsJsonAsync("/api/command", up),
            http.PostAsJsonAsync("/api/command", new RemoteCommand("volume-step", "down-once", Value: -5)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(50, volume);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/command", up)).StatusCode);
        Assert.Equal(50, volume);
        Assert.Equal(2, calls);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/command", new RemoteCommand("volume-step", "max", Value: 100))).StatusCode);
        Assert.Equal(100, volume);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/command", new RemoteCommand("volume-step", "min", Value: -100))).StatusCode);
        Assert.Equal(0, volume);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"value\":null")]
    [InlineData(",\"value\":101")]
    [InlineData(",\"value\":-101")]
    [InlineData(",\"value\":1e400")]
    [InlineData(",\"value\":\"NaN\"")]
    [InlineData(",\"value\":\"Infinity\"")]
    [InlineData(",\"value\":5,\"buttonId\":\"unexpected\"")]
    [InlineData(",\"value\":5,\"cueId\":\"f5caec32-38ea-4f5e-9074-6359fc995456\"")]
    public async Task InvalidVolumeStepNeverReachesDesktop(string fields)
    {
        var calls = 0;
        await using var host = new RemoteHost();
        await host.StartAsync(0, () => new("Test", "", "", false, false, 50, 0, 0, []), command =>
        { calls++; return Task.FromResult(new CommandResult(true, "Unexpected")); }, loopbackOnly: true);
        using var http = Client(host);
        using var content = new StringContent("{\"action\":\"volume-step\",\"requestId\":\"invalid-step\"" + fields + "}", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync("/api/command", content)).StatusCode);
        Assert.Equal(0, calls);
    }

    private static HttpClient Client(RemoteHost host)
    {
        var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{host.Port}") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
        return http;
    }
}
