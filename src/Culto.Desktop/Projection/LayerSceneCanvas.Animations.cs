using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class LayerSceneCanvas
{
    private sealed class AnimatedLayer(SceneLayer layer, Rectangle visual)
    {
        public SceneLayer Layer = layer;
        public Rectangle Visual = visual;
        public ScaleTransform Scale = new(1, 1);
        public TranslateTransform Offset = new();
        public SceneAnimationSample Current = new(1, 0, 0, 1, true);
        public SceneAnimationSample ExitStart;
    }
    private readonly List<AnimatedLayer> _animatedLayers = [];
    private readonly Stopwatch _animationClock = new();
    private bool _animationPending, _animationRunning, _animationExiting, _animationSubscribed, _animationDisposed;
    public bool AnimateOnLoad { get; set; } = true;
    public bool HasAnimations => _animatedLayers.Any(node => node.Layer.Animation.Entrance != "None"
        || node.Layer.Animation.Exit != "None" || node.Layer.Animation.DelayMilliseconds > 0);
    public int ExitDurationMilliseconds => _animatedLayers.Where(node => node.Layer.Animation.Exit != "None")
        .Select(node => node.Layer.Animation.DurationMilliseconds).DefaultIfEmpty().Max();

    private void InitializeLayerAnimations()
    {
        Loaded += (_, _) =>
        {
            if (AnimateOnLoad) StartAnimations();
            else if (_animationRunning) { _animationClock.Start(); SubscribeAnimations(); }
        };
        Unloaded += (_, _) => UnsubscribeAnimations();
    }
    private void RegisterAnimatedLayer(SceneLayer layer, Rectangle visual)
    {
        var node = new AnimatedLayer(layer, visual);
        var transforms = new TransformGroup();
        transforms.Children.Add(node.Scale); transforms.Children.Add(new RotateTransform(layer.Rotation)); transforms.Children.Add(node.Offset);
        visual.RenderTransform = transforms;
        _animatedLayers.Add(node);
        ApplyAnimation(node, SceneAnimationTimeline.SampleEntry(layer.Animation, 0));
    }
    private static void ApplyAnimation(AnimatedLayer node, SceneAnimationSample sample)
    {
        node.Current = sample;
        node.Visual.Opacity = node.Layer.Opacity * sample.Opacity;
        node.Scale.ScaleX = node.Scale.ScaleY = sample.Scale;
        node.Offset.X = node.Layer.Width * sample.OffsetX;
        node.Offset.Y = node.Layer.Height * sample.OffsetY;
    }
    public void StartAnimations()
    {
        if (_animationDisposed) return;
        UnsubscribeAnimations();
        _animationExiting = false; _animationPending = true; _animationRunning = HasAnimations;
        _animationClock.Reset();
        foreach (var node in _animatedLayers) ApplyAnimation(node, SceneAnimationTimeline.SampleEntry(node.Layer.Animation, 0));
        if (_animationRunning && IsLoaded) SubscribeAnimations();
    }
    public void BeginExit()
    {
        if (_animationDisposed) return;
        UnsubscribeAnimations();
        foreach (var node in _animatedLayers) node.ExitStart = node.Current;
        _animationPending = false; _animationExiting = true;
        _animationRunning = ExitDurationMilliseconds > 0;
        _animationClock.Restart();
        if (_animationRunning && IsLoaded) SubscribeAnimations();
    }
    private void SubscribeAnimations()
    {
        if (_animationSubscribed) return;
        CompositionTarget.Rendering += AdvanceAnimations;
        _animationSubscribed = true;
    }
    private void UnsubscribeAnimations()
    {
        if (_animationSubscribed) CompositionTarget.Rendering -= AdvanceAnimations;
        _animationSubscribed = false; _animationClock.Stop();
    }
    private void AdvanceAnimations(object? sender, EventArgs e)
    {
        if (_animationDisposed || !_animationRunning) { UnsubscribeAnimations(); return; }
        if (_animationPending)
        {
            if (!IsReady) return;
            _animationPending = false; _animationClock.Restart();
        }
        var complete = true;
        foreach (var node in _animatedLayers)
        {
            var sample = _animationExiting
                ? SceneAnimationTimeline.SampleExit(node.Layer.Animation, _animationClock.Elapsed.TotalMilliseconds)
                : SceneAnimationTimeline.SampleEntry(node.Layer.Animation, _animationClock.Elapsed.TotalMilliseconds);
            if (_animationExiting)
            {
                // Exit begins at the actual visible state, including partial
                // entrances and layers already hidden by automatic departure.
                var progress = 1 - sample.Opacity;
                var start = node.ExitStart;
                var target = SceneAnimationTimeline.SampleExit(node.Layer.Animation, node.Layer.Animation.DurationMilliseconds);
                sample = new SceneAnimationSample(start.Opacity * sample.Opacity,
                    start.OffsetX + (target.OffsetX - start.OffsetX) * progress,
                    start.OffsetY + (target.OffsetY - start.OffsetY) * progress,
                    start.Scale + (target.Scale - start.Scale) * progress, sample.Complete);
            }
            ApplyAnimation(node, sample); complete &= sample.Complete;
        }
        if (complete) { _animationRunning = false; UnsubscribeAnimations(); }
    }
    private void ResetLayerAnimations()
    {
        UnsubscribeAnimations(); _animationRunning = _animationPending = false;
        _animatedLayers.Clear();
    }
    private void DisposeLayerAnimations()
    {
        ResetLayerAnimations(); _animationDisposed = true;
    }
}
