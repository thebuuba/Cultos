using System.Diagnostics;
using System.IO.Pipes;
using Culto.Core;

// Run from the repository root after building Culto.MoveHost. No desktop UI is started.
var root = Path.GetFullPath(".");
using var source = SharedVideoFrame.Create(32, 32);
using var other = SharedVideoFrame.Create(32, 32);
byte[] Solid(byte b, byte g, byte r) => Enumerable.Range(0, 1024).SelectMany(_ => new byte[] { b, g, r, 255 }).ToArray();
var red = Solid(0, 0, 255); var blue = Solid(255, 0, 0);
var green = Solid(0, 255, 0); var yellow = Solid(0, 255, 255);
source.Write(red); other.Write(green);
var pipeName = "CultoLiveCheck-" + Guid.NewGuid().ToString("N");
using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
var start = new ProcessStartInfo(Path.Combine(root, ".tools/dotnet/dotnet.exe"))
{
    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
};
foreach (var arg in new[] { Path.Combine(root, "src/Culto.MoveHost/bin/Debug/net10.0-windows/Culto.MoveHost.dll"),
    "--pipe", pipeName, "--runtime", Path.Combine(root, "artifacts/ObsMoveRuntime"),
    "--width", "320", "--height", "180", "--live-move" }) start.ArgumentList.Add(arg);
using var worker = Process.Start(start)!;
var stdout = worker.StandardOutput.ReadToEndAsync(); var stderr = worker.StandardError.ReadToEndAsync();
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
bool passed = false;
try
{
    await server.WaitForConnectionAsync(deadline.Token);
    var wire = new ObsMoveProtocol(server);
    if ((await wire.ReadAsync(deadline.Token))?.Type != ObsMoveMessageType.Ready) throw new Exception("Missing Ready.");
    async Task<List<byte[]>> Render(ObsLiveMoveCommand command, bool update)
    {
        await wire.WriteAsync(ObsMoveMessageType.Command, ObsLiveMoveCommand.Encode(command), deadline.Token);
        using var updating = new CancellationTokenSource();
        var producer = Task.Run(async () =>
        {
            for (int i = 0; update && !updating.IsCancellationRequested; i++)
            {
                source.Write((i / 4 % 2) == 0 ? red : blue);
                other.Write((i / 4 % 2) == 0 ? green : yellow);
                await Task.Delay(25);
            }
        });
        var frames = new List<byte[]>();
        try
        {
            while (await wire.ReadAsync(deadline.Token) is { } message)
            {
                if (message.Type == ObsMoveMessageType.Complete) return frames;
                if (message.Type == ObsMoveMessageType.Error) throw new Exception(System.Text.Encoding.UTF8.GetString(message.Payload));
                if (message.Type == ObsMoveMessageType.Frame) frames.Add(message.Payload);
            }
            throw new Exception("Worker disconnected before Complete.");
        }
        finally { updating.Cancel(); await producer; }
    }
    var a = new ObsLiveLayer("shared-camera", source.MapName, 32, 32, 0, 40, 64, 64);
    var matched = await Render(new(320, 180, 1500, new(), [a], [a with { X = 256 }]), true);
    var positions = new HashSet<int>(); int reds = 0, blues = 0;
    foreach (var frame in matched)
    {
        int min = 320; bool r = false, b = false;
        for (int x = 0; x < 320; x++)
        {
            int p = (70 * 320 + x) * 4;
            if (frame[p + 2] > 180) { min = Math.Min(x, min); r = true; }
            if (frame[p] > 180) { min = Math.Min(x, min); b = true; }
        }
        if (min < 320) positions.Add(min);
        if (r) reds++; if (b) blues++;
    }
    Console.WriteLine($"Shared live source: frames={matched.Count}, red={reds}, blue={blues}, positions={positions.Count}");
    if (matched.Count < 10 || reds < 3 || blues < 3 || positions.Count < 8 || positions.Max() < 240)
        throw new Exception("Shared live source did not keep changing while its native transform animated.");

    var swipe = new MoveOptions { In = new() { Position = 72, Zoom = 100, Easing = 0 },
        Out = new() { Position = 68, Zoom = 100, Easing = 0 } };
    var outLayer = a with { SourceId = "outgoing-only", X = 0, Y = 0, Width = 320, Height = 180 };
    var inLayer = outLayer with { SourceId = "incoming-only", MapName = other.MapName };
    var unmatched = await Render(new(320, 180, 1500, swipe, [outLayer], [inLayer]), true);
    var colors = new HashSet<string>();
    foreach (var frame in unmatched.Skip(3).SkipLast(3))
        for (int x = 0; x < 320; x++)
        {
            int p = (90 * 320 + x) * 4;
            if (frame[p] > 200 && frame[p + 1] < 20 && frame[p + 2] < 20) colors.Add("blue-out");
            if (frame[p + 2] > 200 && frame[p + 1] < 20 && frame[p] < 20) colors.Add("red-out");
            if (frame[p + 1] > 200 && frame[p + 2] < 20) colors.Add("green-in");
            if (frame[p + 1] > 200 && frame[p + 2] > 200) colors.Add("yellow-in");
        }
    Console.WriteLine($"Unmatched live sources: frames={unmatched.Count}, colors={string.Join(',', colors)}");
    if (colors.Count != 4) throw new Exception("Incoming/outgoing native sources stopped updating during Move.");

    source.Write(red);
    var translucent = a with { X = 256, Height = 32, Rotation = 90, Opacity = .5 };
    var opacity = await Render(new(320, 180, 350, new(), [a], [translucent]), false);
    int center = (56 * 320 + 288) * 4;
    int outside = (56 * 320 + 260) * 4;
    var last = opacity[^1];
    Console.WriteLine($"Rotated half-opacity destination: center R={last[center + 2]}, outside R={last[outside + 2]}");
    if (last[center + 2] is < 120 or > 136 || last[outside + 2] > 10)
        throw new Exception("Layer opacity or center rotation disagrees with WPF geometry.");
    server.Dispose(); await worker.WaitForExitAsync(deadline.Token);
    if (worker.ExitCode != 0) throw new Exception("Worker exit code: " + worker.ExitCode);
    passed = true;
    Console.WriteLine("Native live Move smoke passed; three commands reused one worker.");
}
finally
{
    if (!worker.HasExited) worker.Kill(true);
    var errors = await stderr; var logs = await stdout;
    if (!passed) { Console.WriteLine(errors); Console.WriteLine(logs.Length > 8000 ? logs[^8000..] : logs); }
}
