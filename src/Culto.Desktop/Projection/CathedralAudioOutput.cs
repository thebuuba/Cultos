using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using NAudio.Wave;

namespace Culto.Desktop.Projection;

// Per-decoder PCM output. Gain ramps run at sample rate without changing
// device/session volume. Reverb is enabled only for Cathedral transitions.
internal sealed class CathedralAudioOutput : IDisposable, IWaveProvider
{
    private const int Rate = 48000, Channels = 2;
    private readonly object _gate = new();
    private readonly float[] _queue = new float[Rate * Channels];
    private readonly float[] _history = new float[Rate * Channels / 5];
    private int _head, _length, _epoch;
    private int _historyHead, _historyLength;
    private float[] _dry = [], _wet = [];
    private DefaultAudioWaveOutput? _output;
    private CathedralReverbProcessor? _reverb;
    private bool _closed, _paused, _ended, _hasTail, _tailComplete;
    private bool _resetEffect;
    private long _fadeFrames, _fadePosition, _tailFrames, _tailPosition;
    private long _completeAfter;
    private int _tailMilliseconds;
    private double _gain, _master = 1, _sendGain, _intensity;
    private const int DeClickFrames = Rate / 200; // 5 ms, independent of scene transitions.
    private double _renderGain, _renderTarget = -1, _renderStep;
    private int _renderRampFrames;
    private double _renderMaster = 1, _masterTarget = 1, _masterStep;
    private int _masterRampFrames;
    private bool _starved = true;
    private int _gapFrames;
    private float _gapLeft, _gapRight;
    private float _lastLeft, _lastRight, _lastDryLeft, _lastDryRight;
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
        var output = new DefaultAudioWaveOutput(this, 60);
        lock (_gate) _output = output;
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
                if (!_paused) BeginGapRelease();
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
                if (_hasTail) BeginGapRelease(includeWet: true);
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
                if (!_paused || _hasTail) BeginGapRelease(includeWet: _hasTail);
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
                var samples = count / (Channels * sizeof(float)) * Channels;
                if (_dry.Length < samples) { _dry = new float[samples]; _wet = new float[samples]; }
                Array.Clear(_dry, 0, samples); Array.Clear(_wet, 0, samples);
                var feeding = !_paused && (!_hasTail || _fadePosition < _fadeFrames);
                var read = feeding ? Math.Min(samples, _length) : 0;
                for (var i = 0; i < read; i++) { _dry[i] = _queue[_head]; _head = (_head + 1) % _queue.Length; }
                _length -= read;
                if (_resetEffect) { _reverb?.Dispose(); _reverb = null; _resetEffect = false; }
                var tail = _hasTail && !_tailComplete;
                var tailNow = tail && (!feeding || _ended && _length == 0);
                if (tail)
                {
                    if (_reverb is null)
                    {
                        _reverb = new CathedralReverbProcessor(Rate, Channels, _tailMilliseconds);
                        if (_historyLength > 0)
                        {
                            var seed = new float[_historyLength];
                            for (var i = 0; i < seed.Length; i++) seed[i] = (float)(_history[(_historyHead + i) % _history.Length] * _sendGain);
                            _reverb.Process(seed, 0, seed.Length);
                        }
                    }
                    for (var i = 0; i < samples; i++) _wet[i] = (float)(_dry[i] * _sendGain);
                    _reverb.Process(_wet, 0, samples);
                }
                else
                {
                    for (var i = 0; i < read; i++)
                    {
                        if (_historyLength == _history.Length) { _historyHead = (_historyHead + 1) % _history.Length; _historyLength--; }
                        _history[(_historyHead + _historyLength++) % _history.Length] = _dry[i];
                    }
                }
                // Every dry signal uses the same release/attack, including a
                // paused source or an underrun while Cathedral is still ringing.
                for (var i = 0; i < samples; i += Channels)
                {
                    var available = i < read;
                    var rawLeft = _dry[i]; var rawRight = _dry[i + 1];
                    if (!available && !_starved) BeginGapRelease();
                    if (available) _starved = false;
                    AdvanceRenderGains(available);
                    var left = available ? (float)(rawLeft * _renderGain) : 0;
                    var right = available ? (float)(rawRight * _renderGain) : 0;
                    if (_gapFrames > 0)
                    {
                        var release = --_gapFrames / (float)DeClickFrames;
                        left += _gapLeft * release; right += _gapRight * release;
                    }
                    _lastDryLeft = left; _lastDryRight = right;
                    if (tail)
                    {
                        var tailFraction = tailNow ? (_tailPosition + i / Channels) / (double)_tailFrames : 0;
                        var wetGain = _renderMaster * _intensity * Math.Clamp((1 - tailFraction) * 10, 0, 1);
                        // The reverb processor returns direct plus reflected audio.
                        left += (float)((_wet[i] - rawLeft * _sendGain) * wetGain);
                        right += (float)((_wet[i + 1] - rawRight * _sendGain) * wetGain);
                    }
                    _dry[i] = _lastLeft = Math.Clamp(left, -1, 1);
                    _dry[i + 1] = _lastRight = Math.Clamp(right, -1, 1);
                }
                if (tail)
                {
                    if (tailNow) _tailPosition += samples / Channels;
                    else _fadePosition += samples / Channels;
                    if (_tailPosition >= _tailFrames)
                    { _tailComplete = true; _completeAfter = Environment.TickCount64 + 120; _reverb!.Dispose(); _reverb = null; _head = _length = 0; }
                }
                Buffer.BlockCopy(_dry, 0, buffer, offset, samples * sizeof(float));
                if (_ended && !_hasTail && !_tailComplete && _length == 0)
                { _tailComplete = true; _completeAfter = Environment.TickCount64 + 120; }
                return count;
            }
        }
        catch (Exception error) { Error(error); return 0; }
    }

    private void AdvanceRenderGains(bool available)
    {
        // Dry playback and Cathedral tails use the same sample-rate ramps.
        if (available || _gain < _renderGain)
        {
            if (_renderTarget != _gain)
            {
                _renderTarget = _gain; _renderRampFrames = DeClickFrames;
                _renderStep = (_gain - _renderGain) / DeClickFrames;
            }
            if (_renderRampFrames > 0)
            {
                _renderGain += _renderStep;
                if (--_renderRampFrames == 0) _renderGain = _renderTarget;
            }
        }
        if (_masterTarget != _master)
        {
            _masterTarget = _master; _masterRampFrames = DeClickFrames;
            _masterStep = (_master - _renderMaster) / DeClickFrames;
        }
        if (_masterRampFrames > 0)
        {
            _renderMaster += _masterStep;
            if (--_masterRampFrames == 0) _renderMaster = _masterTarget;
        }
    }

    private void BeginGapRelease(bool includeWet = false)
    {
        _gapLeft = includeWet ? _lastLeft : _lastDryLeft;
        _gapRight = includeWet ? _lastRight : _lastDryRight;
        _gapFrames = DeClickFrames;
        _starved = true;
        _renderGain = 0; _renderTarget = -1; _renderRampFrames = 0;
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
        DefaultAudioWaveOutput? output;
        lock (_gate) { if (_closed) return; _closed = true; _epoch++; output = _output; _output = null; }
        try { output?.Dispose(); }
        finally
        {
            lock (_gate) { _reverb?.Dispose(); _reverb = null; }
        }
    }
}
