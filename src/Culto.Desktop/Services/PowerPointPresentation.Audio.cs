using Culto.Core;

namespace Culto.Desktop.Services;

public sealed partial class PowerPointPresentation
{
    private readonly List<(object Format, double OriginalVolume)> _mediaVolumes = [];
    private PresentationAudioEnvelope? _audioEnvelope;
    private double _audioMasterVolume = 1;
    public double AudioGain => _audioEnvelope?.CurrentGain ?? 1;

    // MediaFormat belongs to our untitled read-only copy. PowerPoint's Player has
    // no Volume property, and a process-wide audio session would affect user decks.
    private void CaptureAudioChannels()
    {
        if (_presentation is null) return;
        dynamic slides = _presentation.Slides;
        try
        {
            for (var slideIndex = 1; slideIndex <= (int)slides.Count; slideIndex++)
            {
                dynamic slide = slides[slideIndex];
                dynamic shapes = slide.Shapes;
                try
                {
                    for (var index = 1; index <= (int)shapes.Count; index++)
                    {
                        dynamic shape = shapes[index];
                        try
                        {
                            if ((int)shape.Type != 16) continue;
                            dynamic format = shape.MediaFormat;
                            try { _mediaVolumes.Add(((object)format, (double)format.Volume)); }
                            catch { Release(format); throw; }
                        }
                        finally { Release(shape); }
                    }
                }
                finally { Release(shapes); Release(slide); }
            }
        }
        finally { Release(slides); }
        _audioEnvelope = new PresentationAudioEnvelope(_mediaVolumes.Select(media =>
            new PresentationAudioChannel(media.OriginalVolume, volume =>
                ((dynamic)media.Format).Volume = (float)(volume * _audioMasterVolume))));
    }

    // Audio may continue across slides. The authored channels of this owned copy
    // are the boundary; enumerating only the current slide misses those sounds.
    public bool HasAudioToFade => _show is not null && IsRunning && !_isPaused
        && _audioMasterVolume > 0 && AudioGain > 0 && _mediaVolumes.Any(media => media.OriginalVolume > 0);
    public void SetAudioMasterVolume(int volume)
    {
        _audioMasterVolume = Math.Clamp(volume, 0, 100) / 100d;
        foreach (var media in _mediaVolumes)
            ((dynamic)media.Format).Volume = (float)(media.OriginalVolume * AudioGain * _audioMasterVolume);
    }
    public void SetAudioGain(double gain) => _audioEnvelope?.SetGain(gain);
    public long BeginAudioFade(double target, int milliseconds) => _audioEnvelope?.BeginFade(target, milliseconds) ?? 0;
    public bool AdvanceAudioFade(long token, int milliseconds) => _audioEnvelope?.Advance(token, milliseconds) ?? true;
    public void CancelAudioFade() => _audioEnvelope?.Cancel();

    private void DisposeAudioChannels()
    {
        // Called after View.Exit and before closing the owned presentation.
        _audioMasterVolume = 1;
        try { _audioEnvelope?.Dispose(); }
        finally
        {
            _audioEnvelope = null;
            foreach (var media in _mediaVolumes) Release(media.Format);
            _mediaVolumes.Clear();
        }
    }
}
