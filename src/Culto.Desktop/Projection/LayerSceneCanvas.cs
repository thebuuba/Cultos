using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed record LiveSceneLayer(string SourceId, ProjectionSurface Surface, Cue Cue, SceneLayer Layer, int Width, int Height);

public sealed partial class LayerSceneCanvas : Viewbox, IDisposable
{
    private readonly LayerSourcePool _pool;
    private readonly Canvas _canvas = new() { ClipToBounds = true };
    private readonly HashSet<string> _sources = new(StringComparer.Ordinal);
    private readonly List<LiveSceneLayer> _live = new();
    private int _volume;
    private double _gain = 1;
    private bool _paused, _loop;
    public LayerSceneCanvas(LayerSourcePool pool)
    {
        _pool = pool;
        InitializeLayerAnimations();
        Stretch = Stretch.Uniform;
        Child = _canvas;
    }
    public SceneComposition Scene { get; private set; } = new();
    public SceneComposition Layers => Scene;
    public bool IsReady => _live.All(x => x.Surface.HasPlaybackError || (x.Cue.Kind switch
    {
        CueKind.Video or CueKind.WindowCapture or CueKind.BrowserSource => x.Surface.HasVideoFrame,
        CueKind.Audio => x.Surface.IsPlaying || x.Surface.HasEnded,
        _ => true
    }));
    public bool IsPlaying => _live.Any(x => x.Surface.IsPlaying);
    public bool HasEnded => _live.Where(x => x.Cue.Kind is CueKind.Video or CueKind.Audio or CueKind.WindowCapture or CueKind.BrowserSource)
        .All(x => x.Surface.HasEnded || x.Surface.HasPlaybackError);
    public ProjectionSurface? PrimarySurface => _live.FirstOrDefault(x => x.Cue.Kind is CueKind.Video or CueKind.Audio)?.Surface
        ?? _live.FirstOrDefault(x => x.Cue.Kind is CueKind.BrowserSource or CueKind.WindowCapture)?.Surface;
    public IReadOnlyList<LiveSceneLayer> GetLiveLayers() => _live;
    public void SetScene(SceneComposition scene)
    {
        Scene = scene.Normalize();
        ResetLayerAnimations();
        var next = new HashSet<string>(StringComparer.Ordinal);
        _canvas.Children.Clear();
        _live.Clear();
        _canvas.Width = Scene.Width;
        _canvas.Height = Scene.Height;
        _canvas.Background = (Brush)new BrushConverter().ConvertFromString(Scene.Background)!;
        foreach (var layer in Scene.Layers.Where(x => x.Visible))
        {
            var entry = _pool.Acquire(this, layer.SourceId, layer.Source);
            next.Add(layer.SourceId);
            _live.Add(new LiveSceneLayer(layer.SourceId, entry.Surface, entry.Cue, layer, entry.Width, entry.Height));
            var visual = new Rectangle
            {
                Width = layer.Width, Height = layer.Height, Opacity = layer.Opacity,
                Fill = new VisualBrush(entry.Surface) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                RenderTransformOrigin = new Point(.5, .5)
            };
            RegisterAnimatedLayer(layer, visual);
            Canvas.SetLeft(visual, layer.X);
            Canvas.SetTop(visual, layer.Y);
            _canvas.Children.Add(visual);
        }
        foreach (var id in _sources.Except(next).ToArray()) _pool.Release(this, id);
        _sources.Clear();
        _sources.UnionWith(next);
        ApplyPlayback(_volume, _paused, _loop, _gain);
        if (AnimateOnLoad) StartAnimations();
    }
    public void ApplyPlayback(int volume, bool paused, bool loop, double envelope = 1)
    {
        _volume = volume; _paused = paused; _loop = loop; _gain = envelope;
        foreach (var source in _live.GroupBy(x => x.SourceId))
        {
            var gain = source.Max(x => x.Layer.Muted ? 0 : x.Layer.Volume);
            _pool.ApplyPlayback(this, source.Key, (int)Math.Round(volume * gain / 100d), paused, loop || source.Any(item => item.Layer.Loop), envelope);
        }
    }
    public void Dispose()
    {
        DisposeLayerAnimations();
        _canvas.Children.Clear();
        _live.Clear();
        foreach (var id in _sources) _pool.Release(this, id);
        _sources.Clear();
    }
}
