using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Culto.Desktop.Projection;

// Only the endpoint sink is replaced. Its owner retains the PCM queue, pause,
// gain and DSP state; no Windows endpoint/session volume is ever written.
internal sealed class DefaultAudioWaveOutput : IDisposable
{
    private readonly DefaultAudioDeviceFollower _follower;
    private WasapiOut? _output;
    private MMDevice? _device;

    internal static string? CurrentDeviceId()
    {
        try
        {
            using var devices = new MMDeviceEnumerator();
            using var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            return device.ID;
        }
        catch (COMException) { return null; } // No render device while unplugging.
    }

    public DefaultAudioWaveOutput(IWaveProvider source, int latency)
    {
        _follower = new DefaultAudioDeviceFollower(CurrentDeviceId, id =>
        {
            StopOutput();
            if (id is null) return;
            using var devices = new MMDeviceEnumerator();
            _device = devices.GetDevice(id);
            try
            {
                var output = new WasapiOut(_device, AudioClientShareMode.Shared, true, latency);
                _output = output;
                output.Init(source);
                output.PlaybackStopped += (_, args) =>
                {
                    if (args.Exception is not null && ReferenceEquals(_output, output)) _follower!.Invalidate();
                };
                output.Play();
            }
            catch { StopOutput(); throw; }
        });
        _follower.Start();
    }

    private void StopOutput()
    {
        var output = _output; _output = null;
        try { output?.Dispose(); }
        finally { _device?.Dispose(); _device = null; }
    }

    public void Dispose() { _follower.Dispose(); StopOutput(); }
}
