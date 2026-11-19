namespace Culto.Desktop.Projection;

internal sealed partial class ProjectionMixer
{
    private bool _mediaFill;
    private double _mediaZoom = 1;
    public bool MediaFill
    {
        get => _mediaFill;
        set { _mediaFill = value; foreach (var surface in Surfaces()) surface.MediaFill = value; }
    }
    public double MediaZoom
    {
        get => _mediaZoom;
        set { _mediaZoom = value; foreach (var surface in Surfaces()) surface.MediaZoom = value; }
    }
}
