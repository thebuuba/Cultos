using System;

namespace Culto.Desktop.Projection;

// Parallel damped combs and two all-pass diffusers. One audio thread owns
// this bounded room; feed silence to receive its decay, without native plugins.
internal sealed class CathedralReverbProcessor : IDisposable
{
    private readonly Comb[][] _combs;
    private readonly AllPass[][] _diffusers;
    private readonly int _channels;
    private bool _disposed;

    public CathedralReverbProcessor(int sampleRate, int channels, int tailMilliseconds)
    {
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(channels));
        _channels = channels;
        _combs = new Comb[channels][];
        _diffusers = new AllPass[channels][];
        var decay = Math.Clamp(tailMilliseconds / 1000d, .1, 20);
        double[] delays = [.0297, .0371, .0411, .0437, .0307, .0331, .0393, .0451];
        for (var channel = 0; channel < channels; channel++)
        {
            _combs[channel] = new Comb[delays.Length];
            var spread = channel * Math.Max(1, (int)Math.Round(23d * sampleRate / 44100));
            for (var i = 0; i < delays.Length; i++)
            {
                var length = Math.Max(1, (int)Math.Round(delays[i] * sampleRate) + spread);
                var feedback = (float)Math.Pow(.001, length / (sampleRate * decay));
                _combs[channel][i] = new Comb(length, feedback);
            }
            _diffusers[channel] = [new AllPass(Math.Max(1, (int)(sampleRate * .005) + spread)),
                new AllPass(Math.Max(1, (int)(sampleRate * .0017) + spread))];
        }
    }

    // Return dry + wet. The caller subtracts dry to mix the independent tail.
    public void Process(float[] samples, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(samples);
        if (offset < 0 || count < 0 || offset > samples.Length - count || count % _channels != 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        for (var frame = offset; frame < offset + count; frame += _channels)
        {
            var left = Finite(samples[frame]);
            var right = _channels == 2 ? Finite(samples[frame + 1]) : left;
            var input = (left + right) * .075f;
            for (var channel = 0; channel < _channels; channel++)
            {
                var wet = 0f;
                foreach (var comb in _combs[channel]) wet += comb.Process(input);
                wet /= _combs[channel].Length;
                foreach (var diffuser in _diffusers[channel]) wet = diffuser.Process(wet);
                samples[frame + channel] = (channel == 0 ? left : right) + Math.Clamp(wet, -1f, 1f);
            }
        }
    }

    private static float Finite(float value) => float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0;
    private static float WithoutDenormal(float value) => Math.Abs(value) < 1e-20f ? 0 : value;

    private sealed class Comb(int length, float feedback)
    {
        private readonly float[] _delay = new float[length];
        private int _position;
        private float _filtered;
        public float Process(float input)
        {
            var output = _delay[_position];
            _filtered = WithoutDenormal(output * .75f + _filtered * .25f);
            _delay[_position] = WithoutDenormal(input + _filtered * feedback);
            if (++_position == _delay.Length) _position = 0;
            return output;
        }
    }

    private sealed class AllPass(int length)
    {
        private readonly float[] _delay = new float[length];
        private int _position;
        public float Process(float input)
        {
            var delayed = _delay[_position];
            var output = delayed - input * .5f;
            _delay[_position] = WithoutDenormal(input + output * .5f);
            if (++_position == _delay.Length) _position = 0;
            return output;
        }
    }

    public void Dispose() => _disposed = true;
}
