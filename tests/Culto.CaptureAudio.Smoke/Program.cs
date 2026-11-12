using System.Diagnostics;
using Culto.Desktop.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;

if (args.Contains("--tone"))
{
    using var player = new WasapiOut(AudioClientShareMode.Shared, 40);
    player.Init(new Tone());
    player.Play();
    Console.WriteLine("READY");
    Console.ReadLine();
    return;
}
if (args.Contains("--ancestor"))
{
    using var child = await StartTone();
    Console.WriteLine("READY");
    Console.WriteLine(child.Id);
    Console.ReadLine();
    await StopTone(child);
    return;
}

// Browser windows belong to a parent process; the actual audio often lives in
// a child. Match that topology, not only the simpler one-process playback case.
using var first = await StartTone("--ancestor");
int audioPid = int.Parse((await first.StandardOutput.ReadLineAsync())!);
using var unrelated = await StartTone();
using var devices = new MMDeviceEnumerator();
using var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
AudioSessionControl Find(int processId)
{
    device.AudioSessionManager.RefreshSessions();
    var sessions = device.AudioSessionManager.Sessions;
    for (int i = 0; i < sessions.Count; i++)
    {
        var session = sessions[i];
        if (session.GetProcessID == processId) return session;
        session.Dispose();
    }
    throw new Exception("No audio session for fixture " + processId);
}
using var firstSession = Find(audioPid);
using var otherSession = Find(unrelated.Id);
float original = firstSession.SimpleAudioVolume.Volume;
bool originalMute = firstSession.SimpleAudioVolume.Mute;
float otherVolume = otherSession.SimpleAudioVolume.Volume;
bool otherMute = otherSession.SimpleAudioVolume.Mute;
try
{
    firstSession.SimpleAudioVolume.Volume = .7f;
    firstSession.SimpleAudioVolume.Mute = false;
    using var manager = new CapturedApplicationAudio();
    using var a = manager.Acquire(first.Id);
    await Until(() => a.HasSessions && firstSession.SimpleAudioVolume.Mute, "acquisition controls the selected process's audio child");
    Check(otherSession.SimpleAudioVolume.Volume == otherVolume && otherSession.SimpleAudioVolume.Mute == otherMute,
        "an unrelated process keeps its original audio");
    a.SetGain(.5);
    await Until(() => !firstSession.SimpleAudioVolume.Mute && Math.Abs(firstSession.SimpleAudioVolume.Volume - .35) < .005,
        "gain halves the actual Windows session amplitude relative to its saved level");
    using var b = manager.Acquire(first.Id);
    b.SetGain(.5);
    await Until(() => Math.Abs(firstSession.SimpleAudioVolume.Volume - .7) < .005,
        "two overlapping captures of the same process sum once without amplifying above the saved level");
    a.Dispose();
    await Until(() => Math.Abs(firstSession.SimpleAudioVolume.Volume - .35) < .005,
        "retiring one capture preserves the other capture's audio");
    b.Dispose();
    await Until(() => firstSession.SimpleAudioVolume.Mute,
        "retiring the last capture keeps the browser silent while off screen");
    manager.RestoreAll();
    Check(!firstSession.SimpleAudioVolume.Mute && Math.Abs(firstSession.SimpleAudioVolume.Volume - .7) < .005,
        "explicit release restores the original session volume and mute");
    firstSession.SimpleAudioVolume.Mute = true;
    using (var originallyMuted = manager.Acquire(first.Id))
    {
        originallyMuted.SetGain(1);
        await Until(() => originallyMuted.HasSessions, "an initially muted application is discovered");
        Check(firstSession.SimpleAudioVolume.Mute, "capture does not unmute audio that the user had muted originally");
    }
    manager.RestoreAll();
    Check(firstSession.SimpleAudioVolume.Mute, "release preserves the application's original mute state");
    firstSession.SimpleAudioVolume.Mute = false;
    using var muted = manager.Acquire(first.Id);
    await Until(() => muted.HasSessions && firstSession.SimpleAudioVolume.Mute, "reacquisition controls a fresh session lease");
    manager.Dispose();
    Check(!firstSession.SimpleAudioVolume.Mute && Math.Abs(firstSession.SimpleAudioVolume.Volume - .7) < .005,
        "application shutdown restores original browser audio");
    Check(otherSession.SimpleAudioVolume.Volume == otherVolume && otherSession.SimpleAudioVolume.Mute == otherMute,
        "all operations leave unrelated audio unchanged");
}
finally
{
    firstSession.SimpleAudioVolume.Volume = original;
    firstSession.SimpleAudioVolume.Mute = originalMute;
    await StopTone(first);
    await StopTone(unrelated);
}

static async Task<Process> StartTone(string mode = "--tone")
{
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(mode);
    var process = Process.Start(start)!;
    if (await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) != "READY")
        throw new Exception("Synthetic audio fixture failed: " + await process.StandardError.ReadToEndAsync());
    await Task.Delay(200);
    return process;
}
static async Task StopTone(Process process)
{
    if (process.HasExited) return;
    await process.StandardInput.WriteLineAsync("STOP");
    if (await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(2000)) is var completed && !process.HasExited)
        process.Kill();
}
static void Check(bool value, string text)
{
    if (!value) throw new Exception("FAIL: " + text);
    Console.WriteLine("PASS: " + text);
}
static async Task Until(Func<bool> condition, string text)
{
    var clock = Stopwatch.StartNew();
    while (!condition() && clock.ElapsedMilliseconds < 5000) await Task.Delay(20);
    Check(condition(), text);
}
sealed class Tone : WaveProvider32
{
    private long _sample;
    public Tone() : base(48000, 2) { }
    public override int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i += 2)
        {
            float sample = (float)(.02 * Math.Sin(2 * Math.PI * 440 * _sample++ / 48000));
            buffer[offset + i] = buffer[offset + i + 1] = sample;
        }
        return count;
    }
}
