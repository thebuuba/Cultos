using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Culto.Core;
using LibVLCSharp.Shared;

namespace Culto.Desktop.Projection;

// Preview and HDMI draw one compositor. An optional live media child can stay
// behind foreground cues; replacing it overlaps only the two media sources.
internal sealed partial class ProjectionMixer : Grid, IDisposable
{
    private sealed record Request(Cue? Cue, bool Blackout, bool Hidden, bool PauseAfter,
        TransitionOptions Options, BibleVisualStyle Style, string? Background, bool ReusedLiveMedia = false, bool PreserveAudio = false, bool AudioOnly = false);
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly Stopwatch _audioClock = new();
    private ProjectionSurface? _audioOutgoing;
    private TransitionOptions? _audioBlend;
    private readonly List<Task> _retired = [];
    private readonly Func<LibVLC> _engineFactory;
    private readonly LayerSourcePool _layerPool;
    private Task<LibVLC>? _initialization;
    private LibVLC? _vlc;
    private ProjectionSurface _current;
    private ProjectionSurface? _incoming, _outgoing, _retained, _background;
    private Cue? _currentCue, _outgoingCue, _retainedCue, _backgroundCue;
    private Request? _loading, _blend, _queued, _visible;
    private int _volume = 80;
    private bool _loop, _disposed, _desiredPause, _pauseImmediately, _blackout, _hidden;
    private TransitionOptions _options = new(), _appliedOptions = new();
    private BibleVisualStyle _style = new();
    public event Action<string>? PlaybackError;

    public ProjectionMixer(Func<LibVLC>? engineFactory = null)
    {
        _engineFactory = engineFactory ?? ProjectionSurface.CreatePlaybackEngine;
        Background = Brushes.Black; ClipToBounds = true;
        _layerPool = new LayerSourcePool(() => _vlc ?? throw new InvalidOperationException("El reproductor todavía se está preparando."));
        _layerPool.PlaybackError += message => PlaybackError?.Invoke(message);
        Panel.SetZIndex(_layerPool.Depot, -1000); Children.Add(_layerPool.Depot);
        _current = CreateSurface(); Children.Add(_current);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick(), Dispatcher);
        _timer.Stop();
    }

    public ProjectionSurface ActiveSurface => _visible is { } visible && ShowsMedia(visible) && _current.HasLoadedMedia ? _current
        : BackgroundSurface ?? (_retained is not null && (_blackout || _hidden) ? _retained : _current);
    private ProjectionSurface? BackgroundSurface => _background is { HasEnded: false } ? _background : (KeepOutgoingMedia ? _outgoing : null);
    public bool HasBackgroundMedia => BackgroundSurface is not null;
    public Cue? RetainedMediaCue => (_background is { HasEnded: false } ? _backgroundCue
        : KeepOutgoingMedia ? _outgoingCue : null)
        is { } cue ? cue.Copy() : null;
    public Cue? AudioCue => BackgroundSurface?.AudioCue ?? (_current.HasLoadedMedia && !_current.HasEnded ? _current.AudioCue : null);
    public Cue? DisplayedCue => _currentCue;
    public TransitionOptions PresentedTransitions => _appliedOptions with { };
    public bool IsTransitioning => _loading is not null || _blend is not null || _queued is not null || _audioBlend is not null || _current.IsTransitioning;
    public bool IsPreparingPlaybackEngine => !_disposed && _initialization is { IsCompleted: false };
    public bool IsPlaying => !_disposed && ActiveSurface.IsPlaying;
    public bool IsPaused => _disposed || (HasBackgroundMedia ? ActiveSurface.IsPaused : _queued is { } queued
        ? queued.PauseAfter || queued.Blackout || queued.Hidden
        : _desiredPause || _blackout || _hidden || (!IsTransitioning && _current.IsPaused));
    public bool DisplayedBlackout => _visible?.Blackout == true;
    public bool DisplayedHidden => _visible?.Hidden == true;
    public double Position => ActiveSurface.Position;
    public double Duration => ActiveSurface.Duration;
    public int Volume { get => _volume; set { _volume = Math.Clamp(value, 0, 100); ApplyVolumes(); } }
    public bool Loop
    {
        get => _loop;
        set { _loop = value; foreach (var surface in Surfaces()) surface.Loop = value; }
    }
    public string? BackgroundImagePath { get; set; }
    public TransitionOptions Transitions { get => _options with { }; set => _options = (value ?? new()).Normalize(); }
    public BibleVisualStyle BibleStyle { get => _style with { }; set => _style = (value ?? new()).Normalize(); }

    public void ReloadBrowserSource(Guid id) { foreach (var surface in Surfaces()) surface.ReloadBrowserSource(id); }

    public void RefreshWindowCapture() { foreach(var surface in Surfaces())surface.RefreshWindowCapture(); }

    private IEnumerable<ProjectionSurface> Surfaces() => new[] { _current, _incoming, _outgoing, _retained, _background, _audioOutgoing }
        .OfType<ProjectionSurface>().Distinct();
    private ProjectionSurface CreateSurface()
    {
        var surface = new ProjectionSurface(sharedVlc: () => _vlc ?? throw new InvalidOperationException("El reproductor todavía se está preparando."), layers: _layerPool)
        {
            Volume = 0, Loop = _loop,
            Transitions = new TransitionOptions { Kind = "Cut", AudioFadeOutMilliseconds = 0, AudioFadeInMilliseconds = 0 }
        };
        surface.PlaybackError += message => SurfaceFailed(surface, message);
        return surface;
    }
    private Request Capture(Cue? cue, bool blackout, bool hidden, bool pauseAfter)
    {
        // A live window has no operator pause transport. Revealing its retained
        // capture must resume frames/audio automatically; files keep paused restore.
        if (cue?.Kind is CueKind.WindowCapture or CueKind.BrowserSource && !blackout && !hidden) pauseAfter = false;
        var options = _options.Normalize();
        if (cue?.Kind == CueKind.Bible && !pauseAfter && _style.Transition != "System")
            options = options with { Kind = _style.Transition, DurationMilliseconds = _style.TransitionMilliseconds };
        return new(cue is null ? null : cue.Copy(), blackout, hidden, pauseAfter, options, _style.Normalize(), BackgroundImagePath);
    }
    private static bool SameMedia(Cue? first, Cue? second) => first is not null && second is not null
        && first.Id == second.Id && first.Kind == second.Kind && first.FilePath == second.FilePath && first.Capture == second.Capture && first.Browser == second.Browser
        && (first.Kind != CueKind.Scene || System.Text.Json.JsonSerializer.Serialize(first.Scene) == System.Text.Json.JsonSerializer.Serialize(second.Scene));
    private static bool IsMedia(Cue? cue) => cue?.Kind is CueKind.Video or CueKind.Audio or CueKind.WindowCapture or CueKind.BrowserSource
        || cue?.Scene?.Layers.Any(layer => layer.Visible && IsMedia(layer.Source)) == true;
    private static bool ShowsMedia(Request request) => !request.Blackout && !request.Hidden && IsMedia(request.Cue);
    private bool KeepOutgoingMedia => _blend is { } blend && (blend.PreserveAudio && _outgoingCue?.Kind is CueKind.Video or CueKind.Audio or CueKind.Scene) && !ShowsMedia(blend)
        && _outgoing is { HasLoadedMedia: true, HasEnded: false } && IsMedia(_outgoingCue);
    private static bool SameSource(Cue? first, Cue? second)
    {
        if (!IsMedia(first) || first!.Kind != second?.Kind) return false;
        if (first.Kind == CueKind.BrowserSource) return first.Browser is not null && first.Browser == second!.Browser;
        if (first.Kind == CueKind.WindowCapture) return first.Capture is { } source && second!.Capture is { } candidate
            // Browser titles change during playback; the exact window identity
            // and every effective capture/audio option still have to match.
            && source == candidate with { WindowTitle = source.WindowTitle };
        if (string.IsNullOrWhiteSpace(first.FilePath) || string.IsNullOrWhiteSpace(second!.FilePath)) return false;
        try { return StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(first.FilePath), Path.GetFullPath(second.FilePath)); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
    private bool ReusesSource(Cue? first, Cue? second) => first?.Kind != CueKind.Scene && SameMedia(first, second);

    public void TransitionVisual(Cue? cue, bool blackout, bool hidden) =>
        Enqueue(Capture(cue, blackout, hidden, false) with { PreserveAudio = true });
    public void PlayAudioKeepingVisual(Cue cue)
    {
        if (cue.Kind is not (CueKind.Video or CueKind.Audio)) return;
        if (_current.HasLoadedMedia && !_current.HasEnded && !DisplayedBlackout && !DisplayedHidden)
        {
            PlaybackError?.Invoke("Muestre primero una imagen, Biblia, logo o negro para conservarla al cambiar el audio.");
            return;
        }
        Enqueue(Capture(cue, false, false, false) with { PreserveAudio = true, AudioOnly = true });
    }
    public void TransitionTo(Cue cue) => Enqueue(Capture(cue, false, false, false));
    public void TransitionToBackdrop(Cue? cue, bool blackout, bool hidden) => Enqueue(Capture(cue, blackout, hidden, true));

    public void PreparePlaybackEngine()
    {
        if (_disposed || _vlc is not null) return;
        // An unsuccessful speculative warmup must not consume the first real command.
        if (_initialization is { IsFaulted: true })
        {
            _ = _initialization.Exception;
            _initialization = null;
        }
        _initialization ??= Task.Run(_engineFactory);
    }

    private void Enqueue(Request request)
    {
        if (_disposed) return;
        if (_loading is { } loading && request.Cue?.Kind is CueKind.Video or CueKind.Audio
            && SameMedia(loading.Cue, request.Cue) && loading.Cue?.Title == request.Cue.Title
            && loading.Blackout == request.Blackout && loading.Hidden == request.Hidden
            && loading.AudioOnly == request.AudioOnly && loading.PreserveAudio == request.PreserveAudio
            && loading.Options == request.Options && loading.Style == request.Style && loading.Background == request.Background)
        {
            // Repeated projection of a loading file must not discard its decoder
            // and restart the wait for its first frame.
            SetPaused(request.PauseAfter);
            return;
        }
        if (_blend is not null)
        {
            if (!request.AudioOnly && request.Options.Kind == "Cut")
            {
                // A cut, especially blackout, must not wait behind a long audio
                // envelope. Discard older queued commands before finishing it.
                _queued = null;
                FinishBlend();
            }
            else
            {
                // Finish the brief visible blend; keep only the newest queued request.
                // No third decoder and no frozen snapshot when operators click rapidly.
                _queued = request;
                return;
            }
        }
        CancelLoading();
        FinishAudioBlend();
        Load(request);
    }

    private async void Load(Request request, bool retryEngine = true)
    {
        // EOF leaves a decoder loaded, but it is no longer live audio to preserve
        // or resume when a later foreground cue applies its playback intent.
        if (_background?.HasEnded == true) ReleaseBackground();
        _desiredPause = request.PauseAfter; _pauseImmediately = false;
        if (!request.AudioOnly) { _blackout = request.Blackout; _hidden = request.Hidden; }
        _appliedOptions = request.Options with { };
        // A capture can run for an entire service before the first local file.
        // Warm the engine now, without opening a decoder or changing its audio.
        if (_vlc is null && request.Cue?.Kind == CueKind.WindowCapture && !request.Blackout && !request.Hidden)
            PreparePlaybackEngine();
        if (_vlc is null && !request.Blackout && !request.Hidden && (request.Cue?.Kind is CueKind.Video or CueKind.Audio
            || request.Cue?.Scene?.Layers.Any(layer => layer.Source.Kind is CueKind.Video or CueKind.Audio) == true))
        {
            // libvlc_new may scan/load hundreds of native plugins on a cold install.
            // Keep that disk/native initialization off the WPF dispatcher.
            _loading = request;
            PreparePlaybackEngine();
            var initialization = _initialization!;
            try
            {
                var engine = await initialization;
                if (_disposed) return; // Dispose schedules cleanup of the pending engine.
                _vlc = engine;
                if (!ReferenceEquals(_loading, request)) return;
                _loading = null;
                request = request with { PauseAfter = _desiredPause };
            }
            catch (Exception error)
            {
                if (ReferenceEquals(_initialization, initialization)) _initialization = null;
                if (_disposed || !ReferenceEquals(_loading, request)) return;
                // Keep the first command if native initialization fails transiently,
                // including a warmup that fails after the operator has requested play.
                // Retry the engine once; invalid files never enter this path.
                if (retryEngine)
                {
                    Load(request with { PauseAfter = _desiredPause }, retryEngine: false);
                    return;
                }
                _loading = null; _desiredPause = ActiveSurface.IsPaused;
                _blackout = DisplayedBlackout; _hidden = DisplayedHidden;
                PlaybackError?.Invoke("No se pudo preparar el reproductor. " + error.Message + " La salida anterior se conserva.");
                return;
            }
        }
        if (!request.AudioOnly && !request.Blackout && !request.Hidden && !DisplayedBlackout && !DisplayedHidden
            && ReusesSource(_currentCue, request.Cue) && _current.HasLoadedMedia)
        {
            // Selecting the current clip again must not reload it or jump to zero.
            Configure(_current, request);
            _currentCue = request.Cue; _visible = request;
            _current.SetPaused(request.PauseAfter);
            ApplyVolumes();
            return;
        }
        var incoming = !request.AudioOnly && ShowsMedia(request) && (ReusesSource(_backgroundCue, request.Cue) || SameSource(_backgroundCue, request.Cue)) ? _background : null;
        if (incoming is not null)
        {
            _background = null; _backgroundCue = null;
            request = request with { ReusedLiveMedia = true, PauseAfter = false };
            _desiredPause = false;
        }
        else incoming = !request.Blackout && !request.Hidden && ReusesSource(_retainedCue, request.Cue) ? _retained : null;
        if (incoming is not null) { _retained = null; _retainedCue = null; }
        else incoming = CreateSurface();
        _loading = request; _incoming = incoming;
        Configure(incoming, request);
        if (!request.ReusedLiveMedia) incoming.Volume = 0;
        // Incoming is under the opaque, still-moving outgoing surface.
        Children.Insert(0, incoming);
        if (request.AudioOnly) incoming.Opacity = 0;
        else ResetVisual(incoming);
        _clock.Restart(); _timer.Start();
        if (!request.ReusedLiveMedia) incoming.ShowCue(request.Cue, request.Blackout, request.Hidden);
        if (!ReferenceEquals(_incoming, incoming)) return; // Synchronous load error.
        if (!request.PauseAfter) incoming.SetPaused(false);
        Tick();
    }

    private static void Configure(ProjectionSurface surface, Request request)
    {
        surface.BibleStyle = request.Style;
        surface.BackgroundImagePath = request.Background;
        // The mixer owns timing; child surfaces must not run sequential envelopes.
        surface.Transitions = request.Options with { Kind = "Cut", AudioFadeOutMilliseconds = 0, AudioFadeInMilliseconds = 0 };
    }
    private bool IncomingReady(Request request, ProjectionSurface incoming) => request.Blackout || request.Hidden
        || request.Cue?.Kind is not (CueKind.Video or CueKind.Audio or CueKind.WindowCapture or CueKind.BrowserSource or CueKind.Scene)
        || (request.Cue.Kind is CueKind.Video or CueKind.WindowCapture or CueKind.BrowserSource or CueKind.Scene ? incoming.HasVideoFrame : incoming.IsPlaying
            || request.PauseAfter && incoming.HasLoadedMedia);

    private void Tick()
    {
        if (_disposed) return;
        if (_loading is { } loading && _incoming is { } incoming)
        {
            if (!IncomingReady(loading, incoming))
            {
                ApplyVolumes();
                if (_clock.Elapsed.TotalSeconds >= 15) SurfaceFailed(incoming, "El nuevo medio no entregó imagen o audio.");
                return;
            }
            if (_desiredPause) incoming.SetPaused(true);
            if (loading.AudioOnly)
            {
                // A hidden decoder becomes audio only after it is ready. The
                // foreground remains untouched throughout loading and crossfade.
                Children.Remove(incoming);
                _audioOutgoing = _background;
                _background = incoming; _backgroundCue = loading.Cue;
                _incoming = null; _loading = null;
                _audioBlend = loading.Options; _audioClock.Restart();
            }
            else
            {
                _outgoing = _current; _outgoingCue = _currentCue;
                _current = incoming; _currentCue = loading.Cue;
                _visible = loading;
                _incoming = null; _loading = null; _blend = loading;
                _clock.Restart();
                if (loading.Options.Kind == "Move") BeginLiveMove(loading);
            }
        }
        if (_audioBlend is { } audioBlend && _audioClock.Elapsed.TotalMilliseconds >= Math.Max(audioBlend.AudioFadeOutMilliseconds, audioBlend.AudioFadeInMilliseconds)) FinishAudioBlend();
        if (_blend is { } blend && _outgoing is not null)
        {
            var sample = Sample(blend);
            if (_liveMove is { } liveMove) _outgoing.Opacity = liveMove.Complete ? 0 : 1;
            else
            {
                _outgoing.Opacity = 1 - sample.VisualProgress;
                ApplyTransforms(blend.Options.Kind, sample.VisualProgress);
            }
            ApplyVolumes();
            if (sample.Complete && (_liveMove is null || _liveMove.Complete)) FinishBlend();
        }
        ApplyVolumes();
        if (_loading is null && _blend is null && _audioBlend is null) _timer.Stop();
    }

    private CrossfadeSample Sample(Request request) => CrossfadeTimeline.Sample(_liveMove is { Failed: false } live ? live.Started ? live.ElapsedMilliseconds : 0 : _clock.Elapsed.TotalMilliseconds,
        request.Options.Kind == "Cut" ? 0 : request.Options.DurationMilliseconds,
        !KeepOutgoingMedia && (_outgoing?.HasLoadedMedia == true || _background is not null && (!request.PreserveAudio || ShowsMedia(request)))
            ? request.Options.AudioFadeOutMilliseconds : 0,
        _current.HasLoadedMedia && !_desiredPause && !request.ReusedLiveMedia ? request.Options.AudioFadeInMilliseconds : 0);

    private void ApplyTransforms(string kind, double progress)
    {
        if (_outgoing is null) return;
        if (kind == "Slide")
        {
            var eased = progress * progress * (3 - 2 * progress);
            _outgoing.RenderTransform = new TranslateTransform(-ActualWidth * eased, 0);
            _current.RenderTransform = new TranslateTransform(ActualWidth * (1 - eased), 0);
        }
        else if (kind == "Zoom")
        {
            var eased = progress * progress * (3 - 2 * progress);
            _outgoing.RenderTransformOrigin = _current.RenderTransformOrigin = new Point(.5, .5);
            _outgoing.RenderTransform = new ScaleTransform(1 + .12 * eased, 1 + .12 * eased);
            _current.RenderTransform = new ScaleTransform(.88 + .12 * eased, .88 + .12 * eased);
        }
    }
    private void ApplyVolumes()
    {
        if (_disposed) return;
        if (_blend is { } blend && _outgoing is not null)
        {
            var sample = Sample(blend);
            _outgoing.SetMixVolume(_volume, _pauseImmediately ? 0 : KeepOutgoingMedia ? 1 : sample.OutgoingGain);
            _current.SetMixVolume(_volume, _desiredPause ? 0 : blend.ReusedLiveMedia ? 1 : sample.IncomingGain);
        }
        else _current.Volume = _pauseImmediately || (_loading is null && _desiredPause) ? 0 : _volume;
        if (_incoming is not null) _incoming.Volume = _loading?.ReusedLiveMedia == true ? _volume : 0;
        if (_retained is not null) _retained.Volume = 0;
        if (_audioOutgoing is not null && _audioBlend is { } fade)
            _audioOutgoing.SetMixVolume(_volume, _pauseImmediately ? 0 :
                CrossfadeTimeline.Sample(_audioClock.Elapsed.TotalMilliseconds, 0, fade.AudioFadeOutMilliseconds, fade.AudioFadeInMilliseconds).OutgoingGain);
        if (_background is not null)
        {
            double gain = 1;
            if (_blend is { } replacement && (ShowsMedia(replacement) || !replacement.PreserveAudio)) gain = Math.Min(gain, Sample(replacement).OutgoingGain);
            if (_audioBlend is { } audio) gain = Math.Min(gain, CrossfadeTimeline.Sample(_audioClock.Elapsed.TotalMilliseconds, 0,
                audio.AudioFadeOutMilliseconds, audio.AudioFadeInMilliseconds).IncomingGain);
            _background.SetMixVolume(_volume, _pauseImmediately ? 0 : gain);
        }
        // A source shared by two scenes is one decoder, not two signals to fade.
        // Preserve its gain when only its position/size changes between scenes.
        if (!_pauseImmediately && (_outgoing?.SceneCanvas ?? _background?.SceneCanvas) is { } from && _current.SceneCanvas is { } to)
        {
            foreach (var source in from.GetLiveLayers().Where(l => !l.Layer.Muted))
            {
                var target = to.GetLiveLayers().FirstOrDefault(l => l.SourceId == source.SourceId && !l.Layer.Muted);
                if (target is not null && target.Layer.Volume == source.Layer.Volume)
                    source.Surface.Volume = (int)Math.Round(_volume * source.Layer.Volume / 100d);
            }
        }
    }
    private void FinishAudioBlend()
    {
        if (_audioOutgoing is { } previous) { _audioOutgoing = null; Retire(previous); }
        _audioBlend = null;
    }
    private static void ResetVisual(ProjectionSurface surface)
    { surface.Opacity = 1; surface.RenderTransform = Transform.Identity; surface.RenderTransformOrigin = new Point(); }

    private void FinishBlend()
    {
        EndLiveMove();
        var request = _blend; var outgoing = _outgoing;
        var keepOutgoing = KeepOutgoingMedia;
        _blend = null; _outgoing = null; _timer.Stop();
        ResetVisual(_current);
        if (outgoing is not null)
        {
            Children.Remove(outgoing); ResetVisual(outgoing);
            if (keepOutgoing)
            {
                ReleaseBackground();
                _background = outgoing; _backgroundCue = _outgoingCue;
            }
            else if ((request is { Blackout: true } or { Hidden: true }) && SameMedia(_outgoingCue, request.Cue)
                && outgoing.HasLoadedMedia && !outgoing.HasEnded)
            {
                ReleaseRetained();
                outgoing.Volume = 0; outgoing.SetPaused(true);
                _retained = outgoing; _retainedCue = _outgoingCue;
            }
            else Retire(outgoing);
        }
        if (request is { Blackout: false, Hidden: false }) ReleaseRetained();
        if (request is not null && (ShowsMedia(request) || !request.PreserveAudio)) ReleaseBackground();
        _outgoingCue = null;
        ApplyVolumes();
        if (_audioBlend is not null) _timer.Start();
        if (_queued is { } queued)
        {
            _queued = null;
            Load(queued);
        }
    }

    private void CancelLoading()
    {
        var loading = _loading;
        _loading = null;
        if (_incoming is { } pending)
        {
            _incoming = null; Children.Remove(pending);
            if (loading?.ReusedLiveMedia == true)
            { _background = pending; _backgroundCue = loading.Cue; }
            else Retire(pending);
        }
        if (_blend is null) _timer.Stop();
    }
    private void ReleaseRetained()
    {
        if (_retained is { } retained) { _retained = null; _retainedCue = null; Retire(retained); }
    }
    private void ReleaseBackground()
    {
        if (_background is { } background) { _background = null; _backgroundCue = null; Retire(background); }
    }
    private void Retire(ProjectionSurface surface)
    {
        surface.Volume = 0;
        // Observe native cleanup failures instead of leaving unobserved tasks.
        foreach (var task in _retired.Where(task => task.IsCompleted))
            if (task.Exception is { } error) Debug.WriteLine(error);
        _retired.RemoveAll(task => task.IsCompleted);
        _retired.Add(surface.RetireAsync());
    }
    private void SurfaceFailed(ProjectionSurface surface, string message)
    {
        if (_disposed) return;
        if (ReferenceEquals(surface, _incoming))
        {
            CancelLoading();
            _desiredPause = ActiveSurface.IsPaused; _pauseImmediately = _desiredPause;
            _blackout = DisplayedBlackout; _hidden = DisplayedHidden;
            ApplyVolumes();
            PlaybackError?.Invoke(message + " La salida anterior se conserva.");
        }
        else if (ReferenceEquals(surface, _current) || ReferenceEquals(surface, _background)
            || ReferenceEquals(surface, _outgoing)) PlaybackError?.Invoke(message);
    }

    public void ShowCue(Cue? cue, bool blackout, bool hidden)
    {
        if (_disposed) return;
        if (_background is not null)
        {
            var cut = Capture(cue, blackout, hidden, blackout || hidden);
            Enqueue(cut with { Options = cut.Options with { Kind = "Cut", DurationMilliseconds = 0 } });
            return;
        }
        if (_vlc is null && !blackout && !hidden && (cue?.Kind is CueKind.Video or CueKind.Audio
            || cue?.Scene?.Layers.Any(layer => layer.Source.Kind is CueKind.Video or CueKind.Audio) == true))
        { Enqueue(Capture(cue, blackout, hidden, false)); return; }
        _queued = null; CancelLoading();
        if (_blend is not null) FinishBlend();
        var request = Capture(cue, blackout, hidden, blackout || hidden);
        var resumeCapture = false;
        _desiredPause = request.PauseAfter; _pauseImmediately = request.PauseAfter;
        _blackout = blackout; _hidden = hidden;
        if ((blackout || hidden) && SameMedia(_currentCue, cue) && _current.HasLoadedMedia)
        {
            ReleaseRetained();
            _retained = _current; _retainedCue = _currentCue;
            _retained.Volume = 0; _retained.SetPaused(true); Children.Remove(_retained);
            _current = CreateSurface(); Children.Add(_current);
        }
        else if (!blackout && !hidden && SameMedia(_retainedCue, cue))
        {
            Children.Remove(_current); Retire(_current);
            _current = _retained!; _retained = null; _retainedCue = null;
            resumeCapture = cue?.Kind is CueKind.WindowCapture or CueKind.BrowserSource;
            _desiredPause = !resumeCapture; Children.Add(_current);
        }
        else if (!blackout && !hidden) ReleaseRetained();
        Configure(_current, request); _appliedOptions = request.Options;
        _visible = request;
        _currentCue = request.Cue; _current.Volume = _desiredPause ? 0 : _volume;
        _current.ShowCue(cue, blackout, hidden);
        if (resumeCapture) _current.SetPaused(false);
    }

    public void SetPaused(bool paused)
    {
        if (_disposed) return;
        if (_queued is { } queued) { _queued = queued with { PauseAfter = paused || queued.Blackout || queued.Hidden }; return; }
        _desiredPause = paused || !HasBackgroundMedia && (_blackout || _hidden);
        _pauseImmediately = _desiredPause;
        if (HasBackgroundMedia) ActiveSurface.SetPaused(_desiredPause);
        else if (_loading is null) _current.SetPaused(_desiredPause);
        else if (paused) _current.SetPaused(true);
        _audioOutgoing?.SetPaused(_desiredPause);
        if (_outgoing is not null && _pauseImmediately) _outgoing.SetPaused(true);
        ApplyVolumes();
    }
    public void Restart()
    {
        if (_queued is { } queued)
        {
            if (!queued.Blackout && !queued.Hidden) _queued = queued with { PauseAfter = false };
            return;
        }
        if (_disposed || !HasBackgroundMedia && (_blackout || _hidden)) return;
        _desiredPause = _pauseImmediately = false;
        if (_loading is null) ActiveSurface.Restart(); // A loading cue already starts at its beginning.
        ApplyVolumes();
    }
    public void Seek(double position)
    {
        if (!_disposed && _queued is null && _loading is null && (HasBackgroundMedia || !_blackout && !_hidden)) ActiveSurface.Seek(position);
    }

    public void Dispose()
    {
        if (_disposed) return;
        EndLiveMove(); _liveMoveClient?.Dispose(); _liveMoveClient = null;
        _timer.Stop(); _disposed = true;
        var surfaces = Surfaces().ToArray(); Children.Clear();
        foreach (var surface in surfaces) _retired.Add(surface.RetireAsync());
        try { Task.WhenAll(_retired).GetAwaiter().GetResult(); }
        catch (Exception error) { Debug.WriteLine(error); }
        finally
        {
            _layerPool.Dispose();
            if (_vlc is not null) { _vlc.Dispose(); _vlc = null; }
            else if (_initialization is { } initialization)
                _ = initialization.ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully) task.Result.Dispose();
                    else if (task.Exception is { } error) Debug.WriteLine(error);
                }, TaskScheduler.Default);
        }
        _incoming = _outgoing = _retained = _background = null; _loading = _blend = _queued = null;
    }
}
