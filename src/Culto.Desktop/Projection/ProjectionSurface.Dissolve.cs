using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private readonly Grid _composition = new();
    private readonly Image _dissolve = new() { Stretch = Stretch.Fill, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly DispatcherTimer _frameTimeout = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _waitingForFrame, _cueFailed;
    private int _dissolveGeneration;
    public bool IsTransitioning => _dissolve.Visibility == Visibility.Visible;

    private void InitializeDissolve()
    {
        _composition.Children.Add(_root);
        _composition.Children.Add(_dissolve);
        Content = _composition;
        _compositedVideo.FramePresented += () => { if (_waitingForFrame) BeginDissolve(); };
        _frameTimeout.Tick += (_, _) => ReportError("El video no entregó imagen. Comprueba el archivo y vuelve a proyectarlo.");
    }

    public void TransitionTo(Cue cue)
    {
        if (_disposed) return;
        if (_preview || ActualWidth < 1 || ActualHeight < 1) { ShowCue(cue); return; }
        UpdateLayout();
        // Retain the composed outgoing frame, including any in-flight dissolve.
        // Only the new video decodes; there is never duplicate output audio.
        var previous = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
        previous.Render(_composition); previous.Freeze();
        ShowCue(cue);
        if (_cueFailed) return;
        _dissolve.Source = previous;
        _dissolve.Opacity = 1;
        _dissolve.Visibility = Visibility.Visible;
        if (cue.Kind == CueKind.Video)
        {
            _waitingForFrame = true;
            _frameTimeout.Start();
        }
        else BeginDissolve();
    }

    private void BeginDissolve()
    {
        _waitingForFrame = false;
        _frameTimeout.Stop();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400)) { FillBehavior = FillBehavior.Stop };
        var generation = _dissolveGeneration;
        fade.Completed += (_, _) => { if (generation == _dissolveGeneration) CancelDissolve(); };
        _dissolve.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }

    private void CancelDissolve()
    {
        _dissolveGeneration++;
        _waitingForFrame = false;
        _frameTimeout.Stop();
        _dissolve.BeginAnimation(OpacityProperty, null);
        _dissolve.Visibility = Visibility.Collapsed;
        _dissolve.Source = null;
    }
}
