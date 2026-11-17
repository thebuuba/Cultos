using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Culto.Core.Tests;

public class AppUpdateTests
{
    private const string Repository = "test-owner/culto-test";
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("test installer payload");
    private static string Manifest(string version = "0.44.0.0", string? url = null, string? hash = null, long? size = null) => JsonSerializer.Serialize(new {
        version, installerUrl = url ?? $"https://github.com/{Repository}/releases/download/v{version}/CultoStudio-{version}-Windows-x64-Setup.exe",
        sha256 = hash ?? Convert.ToHexString(SHA256.HashData(Payload)), size = size ?? Payload.Length, notes = "Cambios de prueba"
    });
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
    private static HttpClient Http(string manifest, byte[]? payload = null) => new(new Handler(request => new(HttpStatusCode.OK) {
        Content = request.RequestUri!.AbsolutePath.EndsWith("update.json") ? new StringContent(manifest) : new ByteArrayContent(payload ?? Payload)
    }));

    [Theory]
    [InlineData("0.44.0.0", true)]
    [InlineData("0.43.0.0", false)]
    [InlineData("0.42.0.0", false)]
    [InlineData("0.43.0", false)]
    public async Task OnlyNewerStableVersionIsOffered(string version, bool available)
    {
        using var http = Http(Manifest(version));
        var release = await new AppUpdateClient(http, Repository).CheckAsync(new Version(0,43,0,0));
        Assert.Equal(available, release is not null);
    }

    [Theory]
    [InlineData("http://github.com/test-owner/culto-test/releases/download/v0.44.0.0/test.exe")]
    [InlineData("https://evil.example/installer.exe")]
    [InlineData("https://github.com/other/repo/releases/download/v0.44.0.0/test.exe")]
    [InlineData("file:///C:/test.exe")]
    public async Task RejectsInstallerOutsideTrustedRelease(string url)
    {
        using var http = Http(Manifest(url: url));
        await Assert.ThrowsAsync<InvalidDataException>(() => new AppUpdateClient(http, Repository).CheckAsync(new(0,43,0,0)));
    }

    [Theory]
    [InlineData("not-a-version", "0000", 5)]
    [InlineData("0.44.0.0", "invalid", 5)]
    [InlineData("0.44.0.0", "0000", -1)]
    public async Task RejectsMalformedManifest(string version, string hash, long size)
    {
        using var http = Http(Manifest(version, hash: hash, size: size));
        await Assert.ThrowsAsync<InvalidDataException>(() => new AppUpdateClient(http, Repository).CheckAsync(new(0,43,0,0)));
    }

    [Fact]
    public async Task UnpublishedChannelIsNotAnUpdate()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.NotFound)));
        Assert.Null(await new AppUpdateClient(http, Repository).CheckAsync(new(0,43,0,0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadPublishesOnlyVerifiedInstaller(bool corrupt)
    {
        using var http = Http(Manifest(), corrupt ? Enumerable.Repeat((byte)'X', Payload.Length).ToArray() : Payload);
        var client = new AppUpdateClient(http, Repository);
        var release = (await client.CheckAsync(new(0,43,0,0)))!;
        var directory = Path.Combine(Path.GetTempPath(), "CultoUpdateTest-" + Guid.NewGuid().ToString("N"));
        try {
            if (corrupt) {
                await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, directory));
                Assert.Empty(Directory.GetFiles(directory));
            } else {
                var installer = await client.DownloadAsync(release, directory);
                Assert.Equal(Payload, await File.ReadAllBytesAsync(installer));
                Assert.EndsWith(".exe", installer);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CancellationLeavesNoInstaller()
    {
        using var http = Http(Manifest());
        var client = new AppUpdateClient(http, Repository);
        var release = (await client.CheckAsync(new(0,43,0,0)))!;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var directory = Path.Combine(Path.GetTempPath(), "CultoUpdateTest-" + Guid.NewGuid().ToString("N"));
        try { await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync(release, directory, cancellationToken: cancellation.Token)); }
        finally { if (Directory.Exists(directory)) { Assert.Empty(Directory.GetFiles(directory)); Directory.Delete(directory, true); } }
    }

    [Fact]
    public async Task VerifiedDownloadIsReusedWithoutDownloadingAgain()
    {
        var requests = 0;
        using var http = new HttpClient(new Handler(request => {
            requests++;
            return new(HttpStatusCode.OK) { Content = request.RequestUri!.AbsolutePath.EndsWith("update.json") ? new StringContent(Manifest()) : new ByteArrayContent(Payload) };
        }));
        var client = new AppUpdateClient(http, Repository);
        var release = (await client.CheckAsync(new(0,43,0,0)))!;
        var directory = Path.Combine(Path.GetTempPath(), "CultoUpdateTest-" + Guid.NewGuid().ToString("N"));
        try {
            await client.DownloadAsync(release, directory);
            var before = requests;
            await client.DownloadAsync(release, directory);
            Assert.Equal(before, requests);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
