using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Culto.Desktop.Projection;

// Optional, per-decoder PCM output. No device/session volume changes and no
// processing of normal dry playback. The pull thread alone owns the native DSP.
internal sealed class CathedralAudioOutput : IDisposable, IWaveProvider
{
    private const int Rate = 48000, Channels = 2;
    private readonly object _gate = new();
    private readonly float[] _queue = new float[Rate * Channels];
    private readonly float[] _history = new float[Rate * Channels / 5];
    private int _head, _length, _epoch;
    private int _historyHead, _historyLength;
    private float[] _dry = [], _wet = [];
    private WasapiOut? _output;
    private CathedralReverbProcessor? _reverb;
    private bool _closed, _paused, _ended, _hasTail, _tailComplete;
    private bool _resetEffect;
    private long _fadeFrames, _fadePosition, _tailFrames, _tailPosition;
    private long _completeAfter;
    private int _tailMilliseconds;
    private double _gain, _master = 1, _sendGain, _intensity;
    private double _lastAudibleSend = 1;
    private string? _lastError;
    private Func<long>? _clock;
    public float InputPeak { get; private set; }

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, Channels);
    public bool TailComplete { get { lock (_gate) return _closed || _lastError is not null || _tailComplete && Environment.TickCount64 >= _completeAfter; } }
    public bool HasTail { get { lock (_gate) return _hasTail; } }
    public bool HasError { get { lock (_gate) return _lastError is not null; } }
    public string? LastError { get { lock (_gate) return _lastError; } }

    public void Attach(MediaPlayer player, Func<long>? clock = null)
    {
        _clock = clock;
        StartOutput();
        player.SetAudioFormat("S16N", Rate, Channels);
        player.SetAudioCallbacks(Play, Pause, Resume, Flush, Drain);
        // Keep VLC from additionally applying its software volume to the PCM.
        // The enclosing surface supplies the complete envelope through SetGain.
        player.SetVolumeCallback((_, _, _) => { });
    }

    private void StartOutput()
    {
        lock (_gate) { ObjectDisposedException.ThrowIf(_closed, this); if (_output is not null) return; }
        var output = new WasapiOut(AudioClientShareMode.Shared, 60);
        try
        {
            output.Init(this);
            output.PlaybackStopped += (_, e) => { if (e.Exception is not null) Error(e.Exception); };
            lock (_gate) _output = output;
            output.Play();
        }
        catch (Exception error) { output.Dispose(); Error(error); throw; }
    }

    public void SetGain(double amplitudeGain)
    {
        lock (_gate) { _gain = SafeGain(amplitudeGain); if (_gain > 0 && _master > 0) _lastAudibleSend = Math.Clamp(_gain / _master, 0, 1); }
    }
    public void SetMasterGain(double amplitudeGain) { lock (_gate) _master = SafeGain(amplitudeGain); }
    private static double SafeGain(double gain) => double.IsFinite(gain) ? Math.Clamp(gain, 0, 1) : 0;

    public void BeginFadeOut(int fadeMilliseconds, int tailMilliseconds, int intensityPercent)
    {
        lock (_gate)
        {
            if (_closed || _hasTail) return;
            _hasTail = true; _tailComplete = false;
            _fadeFrames = Math.Clamp(fadeMilliseconds, 0, 60000) * (long)Rate / 1000;
            _tailMilliseconds = Math.Clamp(tailMilliseconds, 100, 20000);
            _tailFrames = _tailMilliseconds * (long)Rate / 1000;
            _fadePosition = _tailPosition = 0;
            _sendGain = _master > 0 ? _gain > 0 ? Math.Clamp(_gain / _master, 0, 1) : _lastAudibleSend : 0;
            _intensity = Math.Clamp(intensityPercent, 0, 100) / 100d;
            _resetEffect = true;
        }
    }

    public void EndInput() { lock (_gate) { _ended = true; _paused = false; } }
    public void ResetPlayback() => Resume(IntPtr.Zero, 0);
    public void CancelTail()
    {
        lock (_gate) { _hasTail = _tailComplete = false; _resetEffect = true; }
    }

    private void Play(IntPtr data, IntPtr samples, uint count, long pts)
    {
        try
        {
            if (count is 0 or > 192000) return;
            int epoch; lock (_gate) { if (_closed || _ended) return; epoch = _epoch; }
            // VLC supplies a presentation clock, not callback-arrival time. Queue
            // only enough ahead to cover WASAPI's configured device latency.
            var waitUntil = Environment.TickCount64 + 1000;
            while (_clock is not null && pts > _clock() + 60_000)
            {
                lock (_gate) { if (_closed || _ended || epoch != _epoch) return; }
                if (Environment.TickCount64 >= waitUntil) throw new InvalidOperationException("El reloj de audio no avanzó a tiempo.");
                Thread.Sleep((int)Math.Clamp((pts - _clock() - 60_000) / 1000, 1, 5));
            }
            var pcm = new float[checked((int)count * Channels)];
            var source = new short[pcm.Length];
            Marshal.Copy(samples, source, 0, source.Length);
            for (var i = 0; i < pcm.Length; i++) pcm[i] = source[i] / 32768f;
            foreach (var value in pcm) if (float.IsFinite(value)) InputPeak = Math.Max(InputPeak, Math.Abs(value));
            lock (_gate)
            {
                if (_closed || _ended || epoch != _epoch) return;
                Enqueue(pcm);
            }
        }
        catch (Exception error) { Error(error); }
    }

    private void Enqueue(float[] samples)
    {
        // Keep a bounded mailbox even if a damaged timestamp floods callbacks.
        var start = Math.Max(0, samples.Length - _queue.Length);
        for (var i = start; i < samples.Length; i++)
        {
            if (_length == _queue.Length) { _head = (_head + 1) % _queue.Length; _length--; }
            _queue[(_head + _length) % _queue.Length] = float.IsFinite(samples[i]) ? samples[i] : 0;
            _length++;
        }
    }
    private void Pause(IntPtr data, long pts)
    {
        try
        {
            lock (_gate)
            {
                if (_ended) return;
                _paused = true; _epoch++; _head = _length = 0;
                // Retained logo/blackout decoders pause their dry stream while
                // the independently controlled room continues its finite decay.
                if (!_hasTail) { _historyHead = _historyLength = 0; _resetEffect = true; }
            }
        }
        catch (Exception error) { Error(error); }
    }
    private void Resume(IntPtr data, long pts)
    {
        try
        {
            lock (_gate)
            {
                _epoch++; _paused = _ended = _hasTail = _tailComplete = false;
                _head = _length = _historyHead = _historyLength = 0; _resetEffect = true;
            }
        }
        catch (Exception error) { Error(error); }
    }
    private void Drain(IntPtr data) { try { EndInput(); } catch (Exception error) { Error(error); } }
    private void Flush(IntPtr data, long pts)
    {
        try
        {
            lock (_gate)
            {
                if (_ended) return; // Detached tails own their remaining PCM/DSP.
                _epoch++; _head = _length = 0;
                _historyHead = _historyLength = 0;
                _resetEffect = true;
                _hasTail = _tailComplete = false;
            }
        }
        catch (Exception error) { Error(error); }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        try
        {
            lock (_gate)
            {
                if (_closed || _lastError is not null) return 0;
                if (_paused && !_hasTail) return count;
                var samples = count / (Channels * sizeof(float)) * Channels;
                if (_dry.Length < samples) { _dry = new float[samples]; _wet = new float[samples]; }
                Array.Clear(_dry, 0, samples); Array.Clear(_wet, 0, samples);
                var feeding = !_paused && (!_hasTail || _fadePosition < _fadeFrames);
                var read = feeding ? Math.Min(samples, _length) : 0;
                for (var i = 0; i < read; i++) { _dry[i] = _queue[_head]; _head = (_head + 1) % _queue.Length; }
                _length -= read;
                if (_resetEffect) { _reverb?.Dispose(); _reverb = null; _resetEffect = false; }
                if (_hasTail && !_tailComplete)
                {
                    if (_reverb is null)
                    {
                        _reverb = new CathedralReverbProcessor(Rate, Channels, _tailMilliseconds);
                        // Seed the room with recent outgoing sound even for a cut.
                        // This happens only at transition onset, never on dry playback.
                        if (_historyLength > 0)
                        {
                            var seed = new float[_historyLength];
                            for (var i = 0; i < seed.Length; i++) seed[i] = (float)(_history[(_historyHead + i) % _history.Length] * _sendGain);
                            _reverb.Process(seed, 0, seed.Length);
                        }
                    }
                    for (var i = 0; i < samples; i++) _wet[i] = (float)(_dry[i] * _sendGain);
                    _reverb.Process(_wet, 0, samples);
                    var tailNow = !feeding || _ended && _length == 0;
                    for (var i = 0; i < samples; i++)
                    {
                        var tailFraction = tailNow ? (_tailPosition + i / Channels) / (double)_tailFrames : 0;
                        // A final short ramp avoids a hard cutoff at the selected duration.
                        var taper = Math.Clamp((1 - tailFraction) * 10, 0, 1);
                        // The processor returns direct plus reflected audio. Remove the
                        // direct component before adding this independent wet bus.
                        var wet = _wet[i] - _dry[i] * _sendGain;
                        _dry[i] = (float)Math.Clamp(_dry[i] * _gain + wet * _master * _intensity * taper, -1, 1);
                    }
                    if (tailNow) _tailPosition += samples / Channels;
                    else _fadePosition += samples / Channels;
                    if (_tailPosition >= _tailFrames)
                    { _tailComplete = true; _completeAfter = Environment.TickCount64 + 120; _reverb.Dispose(); _reverb = null; _head = _length = 0; }
                }
                else for (var i = 0; i < samples; i++)
                {
                    if (!_hasTail && i < read)
                    {
                        if (_historyLength == _history.Length) { _historyHead = (_historyHead + 1) % _history.Length; _historyLength--; }
                        _history[(_historyHead + _historyLength++) % _history.Length] = _dry[i];
                    }
                    _dry[i] = (float)Math.Clamp(_dry[i] * _gain, -1, 1);
                }
                Buffer.BlockCopy(_dry, 0, buffer, offset, samples * sizeof(float));
                if (_ended && !_hasTail && !_tailComplete && _length == 0)
                { _tailComplete = true; _completeAfter = Environment.TickCount64 + 120; }
                return count;
            }
        }
        catch (Exception error) { Error(error); return 0; }
    }

    private void Error(Exception error) { lock (_gate) { _lastError = error.Message; _tailComplete = true; _epoch++; } }

    public static async Task<CathedralAudioOutput> PlayPreviewAsync(int tailMilliseconds, int intensityPercent, CancellationToken token)
    {
        var output = new CathedralAudioOutput();
        try
        {
            output.SetGain(1); output.SetMasterGain(1); output.StartOutput();
            var block = new float[Rate / 100 * Channels];
            for (var tick = 0; tick < 70; tick++)
            {
                token.ThrowIfCancellationRequested();
                if (tick == 40) output.BeginFadeOut(300, tailMilliseconds, intensityPercent);
                var envelope = Math.Min(1, tick / 5d) * (tick < 40 ? 1 : (70 - tick) / 30d);
                for (var frame = 0; frame < block.Length / Channels; frame++)
                {
                    var t = (tick * (Rate / 100) + frame) / (double)Rate;
                    var value = (float)(envelope * .12 * (Math.Sin(2 * Math.PI * 261.63 * t) + Math.Sin(2 * Math.PI * 329.63 * t) + Math.Sin(2 * Math.PI * 392 * t)));
                    block[frame * Channels] = block[frame * Channels + 1] = value;
                }
                lock (output._gate) output.Enqueue(block);
                await Task.Delay(10, token).ConfigureAwait(false);
            }
            output.EndInput();
            return output;
        }
        catch { output.Dispose(); throw; }
    }

    public void Dispose()
    {
        WasapiOut? output;
        lock (_gate) { if (_closed) return; _closed = true; _epoch++; output = _output; _output = null; }
        try { output?.Stop(); }
        finally
        {
            output?.Dispose();
            lock (_gate) { _reverb?.Dispose(); _reverb = null; }
        }
    }
}
