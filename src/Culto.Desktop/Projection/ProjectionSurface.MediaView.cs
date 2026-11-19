using System;
using System.Windows;
using System.Windows.Media;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private bool _mediaFill;
    private double _mediaZoom = 1;

    public bool MediaFill
    {
        get => _mixer?.MediaFill ?? _mediaFill;
        set { _mediaFill = value; if (_mixer is not null) _mixer.MediaFill = value; ApplyMediaView(); }
    }

    public double MediaZoom
    {
        get => _mixer?.MediaZoom ?? _mediaZoom;
        set
        {
            _mediaZoom = double.IsFinite(value) ? Math.Clamp(value, 1, 3) : 1;
            if (_mixer is not null) _mixer.MediaZoom = _mediaZoom;
            ApplyMediaView();
        }
    }

    private void ApplyMediaView()
    {
        // Bible backgrounds retain their own style controls.
        var image = !_suppressed && _shownCue is { Kind: CueKind.Image };
        if (image) _image.Stretch = _mediaFill ? Stretch.UniformToFill : Stretch.Uniform;
        _image.RenderTransformOrigin = _compositedVideo.RenderTransformOrigin = new Point(.5, .5);
        _image.RenderTransform = image ? new ScaleTransform(_mediaZoom, _mediaZoom) : Transform.Identity;
        _compositedVideo.Stretch = _mediaFill ? Stretch.UniformToFill : Stretch.Uniform;
        _compositedVideo.RenderTransform = new ScaleTransform(_mediaZoom, _mediaZoom);
    }
}
