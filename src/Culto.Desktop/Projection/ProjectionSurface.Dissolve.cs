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
    private readonly Grid _composition = new() { ClipToBounds = true };
    private readonly Image _dissolve = new() { Stretch = Stretch.Fill, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly DispatcherTimer _frameTimeout = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _waitingForFrame, _cueFailed;
    private int _dissolveGeneration;
    private int _dissolveMilliseconds = 400;
    private string _transitionKind = "Dissolve";
    private TransitionOptions _transitions = new();
    private TransitionOptions _presentedTransitions = new();
    public TransitionOptions Transitions
    {
        get => _mixer?.Transitions ?? _transitions with { };
        set { _transitions = (value ?? new()).Normalize(); if (_mixer is not null) _mixer.Transitions = _transitions; }
    }
    public bool IsTransitioning => _mixer?.IsTransitioning ?? (_pendingPresentation is not null || _dissolve.Visibility == Visibility.Visible);

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
        if (_mixer is not null) { _mixer.TransitionTo(cue); return; }
        var kind = cue.Kind == CueKind.Bible && _bibleStyle.Transition != "System" ? _bibleStyle.Transition : _transitions.Kind;
        Present(CreatePresentation(cue, false, false, kind != "Cut") with { AnimateAudio = true });
    }

    public void TransitionToBackdrop(Cue? live, bool blackout, bool hideText)
    {
        if (_mixer is not null) _mixer.TransitionToBackdrop(live, blackout, hideText);
        else Present(CreatePresentation(live, blackout, hideText, _transitions.Kind != "Cut", pauseAfter: true) with { AnimateAudio = true });
    }

    private void TransitionToCore(Presentation presentation)
    {
        if (_disposed) return;
        if (_preview || ActualWidth < 1 || ActualHeight < 1)
        {
            ShowCueCore(presentation.Cue, presentation.Blackout, presentation.HideText, presentation.PauseAfter);
            return;
        }
        UpdateLayout();
        // Retain the composed outgoing frame, including any in-flight dissolve.
        // Only the new video decodes; there is never duplicate output audio.
        var previous = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
        previous.Render(_composition); previous.Freeze();
        ShowCueCore(presentation.Cue, presentation.Blackout, presentation.HideText, presentation.PauseAfter);
        if (_cueFailed) return;
        var bibleOverride = presentation.Cue?.Kind == CueKind.Bible && !presentation.PauseAfter
            && presentation.BibleStyle.Transition != "System";
        _dissolveMilliseconds = bibleOverride ? presentation.BibleStyle.TransitionMilliseconds : presentation.Transitions.DurationMilliseconds;
        _transitionKind = bibleOverride ? presentation.BibleStyle.Transition : presentation.Transitions.Kind;
        _dissolve.Source = previous;
        _dissolve.Opacity = 1;
        _dissolve.Visibility = Visibility.Visible;
        // Suppressed and paused video retains its decoded frame; it cannot deliver a new one.
        if (presentation.Cue?.Kind == CueKind.Video && !_suppressed && !_pauseRequested)
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
        if (_transitionKind == "Move") { BeginNativeMove(); return; }
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(_dissolveMilliseconds)) { FillBehavior = FillBehavior.Stop };
        var generation = _dissolveGeneration;
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        if (_transitionKind == "Slide")
        {
            var outgoing = new TranslateTransform();
            var incoming = new TranslateTransform();
            _dissolve.RenderTransform = outgoing; _root.RenderTransform = incoming;
            outgoing.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, -ActualWidth, fade.Duration) { EasingFunction = ease });
            incoming.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(ActualWidth, 0, fade.Duration) { EasingFunction = ease });
        }
        else if (_transitionKind == "Zoom")
        {
            var outgoing = new ScaleTransform(1, 1);
            var incoming = new ScaleTransform(.88, .88);
            _dissolve.RenderTransformOrigin = _root.RenderTransformOrigin = new Point(.5, .5);
            _dissolve.RenderTransform = outgoing; _root.RenderTransform = incoming;
            foreach (var axis in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
            {
                outgoing.BeginAnimation(axis, new DoubleAnimation(1, 1.12, fade.Duration) { EasingFunction = ease });
                incoming.BeginAnimation(axis, new DoubleAnimation(.88, 1, fade.Duration) { EasingFunction = ease });
            }
        }
        fade.Completed += (_, _) => { if (generation == _dissolveGeneration) CancelDissolve(); };
        _dissolve.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }

    private void CancelDissolve()
    {
        _moveCancellation?.Cancel();
        _latestMoveFrame = null;
        _dissolveGeneration++;
        _waitingForFrame = false;
        _frameTimeout.Stop();
        _dissolve.BeginAnimation(OpacityProperty, null);
        _dissolve.Visibility = Visibility.Collapsed;
        _dissolve.Source = null;
        _dissolve.RenderTransform = _root.RenderTransform = Transform.Identity;
        _dissolve.RenderTransformOrigin = _root.RenderTransformOrigin = new Point();
    }
}
