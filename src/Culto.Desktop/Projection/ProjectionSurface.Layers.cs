using System;
using System.Collections.Generic;
using System.Windows;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private LayerSourcePool? _layerPool;
    private bool _ownsLayerPool;
    private readonly bool _transparentBackground;
    private LayerSceneCanvas? _sceneCanvas;
    internal LayerSceneCanvas? SceneCanvas => _sceneCanvas;
    internal IReadOnlyList<LiveSceneLayer> GetLiveLayers() => _sceneCanvas?.GetLiveLayers() ?? Array.Empty<LiveSceneLayer>();

    private void ShowScene(Cue cue, bool paused)
    {
        if (_layerPool is null)
        {
            _layerPool = new LayerSourcePool(_sharedVlc);
            _layerPool.PlaybackError += message => PlaybackError?.Invoke(message);
            _ownsLayerPool = true;
            _composition.Children.Insert(0, _layerPool.Depot);
        }
        if (_sceneCanvas is null)
        {
            _sceneCanvas = new LayerSceneCanvas(_layerPool);
            _root.Children.Add(_sceneCanvas);
        }
        _pauseRequested = paused || _suppressed;
        _sceneCanvas.SetScene(cue.Scene ?? new SceneComposition());
        _sceneCanvas.Visibility = _suppressed ? Visibility.Collapsed : Visibility.Visible;
        ApplyScenePlayback();
    }
    private double _mixGain = 1;
    internal void SetMixVolume(int volume, double gain)
    {
        _mixGain = Math.Clamp(gain, 0, 1);
        _volume = Math.Clamp(volume, 0, 100);
        ApplyAudioVolume(); ApplyBrowserAudioMute();
    }
    private void ApplyScenePlayback() => _sceneCanvas?.ApplyPlayback(_preview ? 0 : _volume, _pauseRequested || _suppressed, _loop, _mixGain);
    private void StopScene()
    {
        if (_sceneCanvas is null) return;
        _root.Children.Remove(_sceneCanvas);
        _sceneCanvas.Dispose();
        _sceneCanvas = null;
    }
    private void DisposeLayers()
    {
        StopScene();
        if (!_ownsLayerPool) return;
        _layerPool?.Dispose();
        _layerPool = null;
    }
}
