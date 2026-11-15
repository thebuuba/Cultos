using System;
using System.Collections.Generic;

namespace Culto.Desktop.Projection;

internal sealed partial class ProjectionMixer
{
    private sealed record AudioTail(CathedralAudioOutput Output, bool Owned);
    private readonly List<AudioTail> _cathedralTails = [];

    private void RememberTail(CathedralAudioOutput? output, bool owned)
    {
        if (output is not { HasTail: true }) return;
        if (_disposed) { if (owned) output.Dispose(); return; }
        var index = _cathedralTails.FindIndex(t => ReferenceEquals(t.Output, output));
        if (index >= 0) { if (owned) _cathedralTails[index] = new AudioTail(output, true); }
        else
        {
            while (_cathedralTails.Count >= 2)
            {
                var oldest = _cathedralTails[0]; _cathedralTails.RemoveAt(0);
                if (oldest.Owned) oldest.Output.Dispose(); else oldest.Output.CancelTail();
            }
            _cathedralTails.Add(new AudioTail(output, owned));
        }
        _timer.Start();
    }

    private void UpdateCathedralTails()
    {
        for (var i = _cathedralTails.Count - 1; i >= 0; i--)
        {
            var tail = _cathedralTails[i];
            if (tail.Output.TailComplete || !tail.Output.HasTail)
            {
                _cathedralTails.RemoveAt(i); if (tail.Owned) tail.Output.Dispose();
                continue;
            }
            tail.Output.SetMasterGain(_pauseImmediately ? 0 : Math.Pow(_volume / 100d, 3));
        }
    }

    private void StopCathedralTails()
    {
        foreach (var tail in _cathedralTails) if (tail.Owned) tail.Output.Dispose();
        _cathedralTails.Clear();
    }
}
