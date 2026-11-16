using System;
using System.Runtime.InteropServices;
using CefSharp;
using CefSharp.Handler;
using CefSharp.Structs;
using NAudio.Wave;

namespace Culto.Desktop.Projection;

// CEF redirects its audio to this handler. Gain is applied to our own samples,
// never to the Windows device, another browser or another application's session.
internal sealed class BrowserAudioOutput : AudioHandler
{
    private readonly object _gate = new();
    private BufferedWaveProvider? _buffer;
    private DefaultAudioWaveOutput? _output;
    private int _channels;
    private volatile float _gain;
    private bool _closed;
    public long PacketsReceived { get; private set; }
    public string? LastError { get; private set; }
    public void SetGain(double gain) => _gain = (float)Math.Clamp(gain, 0, 1);
    protected override bool GetAudioParameters(IWebBrowser chromiumWebBrowser, IBrowser browser, ref AudioParameters parameters) => true;
    protected override void OnAudioStreamStarted(IWebBrowser chromiumWebBrowser, IBrowser browser, AudioParameters parameters, int channels)
    {
        lock (_gate)
        {
            if (_closed) return;
            try
            {
                Stop(); _channels = channels;
                _buffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(parameters.SampleRate, channels))
                { BufferDuration = TimeSpan.FromMilliseconds(500), DiscardOnBufferOverflow = true, ReadFully = true };
                _output = new DefaultAudioWaveOutput(_buffer, 80);
            }
            catch (Exception error) { LastError = error.Message; Stop(); }
        }
    }
    protected override void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts)
    {
        lock (_gate)
        {
            if (_closed || _buffer is null || noOfFrames is <= 0 or > 65536 || _channels is <= 0 or > 32) return;
            try
            {
                var interleaved = new float[noOfFrames * _channels];
                var plane = new float[noOfFrames];
                var gain = _gain;
                for (int channel = 0; channel < _channels; channel++)
                {
                    Marshal.Copy(Marshal.ReadIntPtr(data, channel * IntPtr.Size), plane, 0, noOfFrames);
                    for (int frame = 0; frame < noOfFrames; frame++) interleaved[frame * _channels + channel] = plane[frame] * gain;
                }
                var bytes = new byte[interleaved.Length * sizeof(float)]; Buffer.BlockCopy(interleaved, 0, bytes, 0, bytes.Length);
                _buffer.AddSamples(bytes, 0, bytes.Length); PacketsReceived++;
            }
            catch (Exception error) { LastError = error.Message; }
        }
    }
    protected override void OnAudioStreamStopped(IWebBrowser chromiumWebBrowser, IBrowser browser) { lock (_gate) Stop(); }
    protected override void OnAudioStreamError(IWebBrowser chromiumWebBrowser, IBrowser browser, string errorMessage) => LastError = errorMessage;
    private void Stop() { _output?.Dispose(); _output = null; _buffer = null; }
    public void Close() { lock (_gate) { _closed = true; _gain = 0; Stop(); } }
}
