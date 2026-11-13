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
internal sealed class ProjectionMixer : Grid, IDisposable
{
    private sealed record Request(Cue? Cue, bool Blackout, bool Hidden, bool PauseAfter,
        TransitionOptions Options, BibleVisualStyle Style, string? Background, bool ReusedLiveMedia = false);
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly Stopwatch _backgroundClock = new();
    private readonly List<Task> _retired = [];
    private readonly Func<LibVLC> _engineFactory;
    private Task<LibVLC>? _initialization;
    private LibVLC? _vlc;
    private ProjectionSurface _current;
    private ProjectionSurface? _incoming, _outgoing, _retained, _background;
    private Cue? _currentCue, _outgoingCue, _retainedCue, _backgroundCue;
    private Request? _loading, _blend, _queued, _visible;
    private int _volume = 80;
    private bool _loop, _disposed, _desiredPause, _pauseImmediately, _blackout, _hidden;
    private bool _keepMediaAudio, _backgroundFading;
    private double _backgroundFadeMilliseconds;
    private TransitionOptions _options = new(), _appliedOptions = new();
    private BibleVisualStyle _style = new();
    public event Action<string>? PlaybackError;

    public ProjectionMixer(Func<LibVLC>? engineFactory = null)
    {
        _engineFactory = engineFactory ?? ProjectionSurface.CreatePlaybackEngine;
        Background = Brushes.Black; ClipToBounds = true;
        _current = CreateSurface(); Children.Add(_current);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick(), Dispatcher);
        _timer.Stop();
    }

    public ProjectionSurface ActiveSurface => _visible is { } visible && ShowsMedia(visible) && _current.HasLoadedMedia ? _current
        : BackgroundSurface ?? (_retained is not null && (_blackout || _hidden) ? _retained : _current);
    private ProjectionSurface? BackgroundSurface => _background ?? (KeepOutgoingMedia ? _outgoing : null);
    public bool HasBackgroundMedia => BackgroundSurface is not null;
    public Cue? RetainedMediaCue => (_backgroundCue ?? (KeepOutgoingMedia ? _outgoingCue : null)
        ?? (_keepMediaAudio && _current.HasLoadedMedia ? _currentCue : null)) is { } cue ? Copy(cue) : null;
    public bool KeepMediaAudio
    {
        get => _keepMediaAudio;
        set
        {
            if (_disposed || _keepMediaAudio == value) return;
            // Complete an already visible blend before changing ownership of its
            // outgoing child. The new mode then has one unambiguous live source.
            if (_blend is not null) FinishBlend();
            _keepMediaAudio = value;
            if (_background is not null)
            {
                _backgroundFading = !value;
                _backgroundFadeMilliseconds = _options.AudioFadeOutMilliseconds;
                _backgroundClock.Restart();
                _timer.Start();
            }
            ApplyVolumes();
            Tick();
        }
    }
    public Cue? DisplayedCue => _currentCue;
    public TransitionOptions PresentedTransitions => _appliedOptions with { };
    public bool IsTransitioning => _loading is not null || _blend is not null || _queued is not null || _backgroundFading || _current.IsTransitioning;
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

    public void RefreshWindowCapture() { foreach(var surface in Surfaces())surface.RefreshWindowCapture(); }

    private IEnumerable<ProjectionSurface> Surfaces() => new[] { _current, _incoming, _outgoing, _retained, _background }
        .OfType<ProjectionSurface>().Distinct();
    private ProjectionSurface CreateSurface()
    {
        var surface = new ProjectionSurface(sharedVlc: () => _vlc ?? throw new InvalidOperationException("El reproductor todavía se está preparando."))
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
        if (cue?.Kind == CueKind.WindowCapture && !blackout && !hidden) pauseAfter = false;
        var options = _options.Normalize();
        if (cue?.Kind == CueKind.Bible && !pauseAfter && _style.Transition != "System")
            options = options with { Kind = _style.Transition, DurationMilliseconds = _style.TransitionMilliseconds };
        // Native Move currently takes still-image inputs. Live windows use the
        // compositor's dissolve and report that actual kind instead of freezing.
        if (options.Kind == "Move" && (_keepMediaAudio || _background is not null
            || cue?.Kind == CueKind.WindowCapture || _currentCue?.Kind == CueKind.WindowCapture))
            options = options with { Kind = "Dissolve" };
        return new(cue is null ? null : Copy(cue), blackout, hidden, pauseAfter, options, _style.Normalize(), BackgroundImagePath);
    }
    private static Cue Copy(Cue cue) => new() { Id = cue.Id, Kind = cue.Kind, Title = cue.Title, Body = cue.Body,
        FilePath = cue.FilePath, Reference = cue.Reference, Version = cue.Version, HymnNumber = cue.HymnNumber, Notes = cue.Notes, Capture = cue.Capture is {} capture ? capture with {} : null };
    private static bool SameMedia(Cue? first, Cue? second) => first is not null && second is not null
        && first.Id == second.Id && first.Kind == second.Kind && first.FilePath == second.FilePath && first.Capture == second.Capture;
    private static bool IsMedia(Cue? cue) => cue?.Kind is CueKind.Video or CueKind.Audio or CueKind.WindowCapture;
    private static bool ShowsMedia(Request request) => !request.Blackout && !request.Hidden && IsMedia(request.Cue);
    private bool KeepOutgoingMedia => _keepMediaAudio && _blend is { } blend && !ShowsMedia(blend)
        && _outgoing?.HasLoadedMedia == true && IsMedia(_outgoingCue);
    private static bool SameSource(Cue? first, Cue? second)
    {
        if (!IsMedia(first) || first!.Kind != second?.Kind) return false;
        if (first.Kind == CueKind.WindowCapture) return first.Capture is { } source && second!.Capture is { } candidate
            // Browser titles change during playback; the exact window identity
            // and every effective capture/audio option still have to match.
            && source == candidate with { WindowTitle = source.WindowTitle };
        if (string.IsNullOrWhiteSpace(first.FilePath) || string.IsNullOrWhiteSpace(second!.FilePath)) return false;
        try { return StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(first.FilePath), Path.GetFullPath(second.FilePath)); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
    private bool ReusesSource(Cue? first, Cue? second) => SameMedia(first, second) || _keepMediaAudio && SameSource(first, second);

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
            && loading.Options == request.Options && loading.Style == request.Style && loading.Background == request.Background)
        {
            // Repeated projection of a loading file must not discard its decoder
            // and restart the wait for its first frame.
            SetPaused(request.PauseAfter);
            return;
        }
        if (_blend is not null)
        {
            // Finish the brief visible blend; keep only the newest queued request.
            // No third decoder and no frozen snapshot when operators click rapidly.
            _queued = request;
            return;
        }
        CancelLoading();
        Load(request);
    }

    private async void Load(Request request, bool retryEngine = true)
    {
        _desiredPause = request.PauseAfter; _pauseImmediately = false;
        _blackout = request.Blackout; _hidden = request.Hidden;
        _appliedOptions = request.Options with { };
        // A capture can run for an entire service before the first local file.
        // Warm the engine now, without opening a decoder or changing its audio.
        if (_vlc is null && request.Cue?.Kind == CueKind.WindowCapture && !request.Blackout && !request.Hidden)
            PreparePlaybackEngine();
        if (_vlc is null && request.Cue?.Kind is CueKind.Video or CueKind.Audio && !request.Blackout && !request.Hidden)
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
        if (!request.Blackout && !request.Hidden && !DisplayedBlackout && !DisplayedHidden
            && ReusesSource(_currentCue, request.Cue) && _current.HasLoadedMedia)
        {
            // Selecting the current clip again must not reload it or jump to zero.
            Configure(_current, request);
            _currentCue = request.Cue; _visible = request;
            _current.SetPaused(request.PauseAfter);
            ApplyVolumes();
            return;
        }
        if (request.Options.Kind == "Move" && !_keepMediaAudio && _background is null
            && request.Cue?.Kind != CueKind.WindowCapture && _currentCue?.Kind != CueKind.WindowCapture)
        {
            // The genuine OBS plugin currently accepts still-image inputs. Keep
            // that explicit native path instead of silently substituting an effect.
            ReleaseRetained();
            Configure(_current, request);
            _current.Transitions = request.Options;
            _current.Volume = _volume;
            _currentCue = request.Cue;
            _visible = request;
            if (request.PauseAfter) _current.TransitionToBackdrop(request.Cue, request.Blackout, request.Hidden);
            else if (request.Cue is not null) _current.TransitionTo(request.Cue);
            return;
        }
        var incoming = ShowsMedia(request) && ReusesSource(_backgroundCue, request.Cue) ? _background : null;
        if (incoming is not null)
        {
            _background = null; _backgroundCue = null; _backgroundFading = false;
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
        || request.Cue?.Kind is not (CueKind.Video or CueKind.Audio or CueKind.WindowCapture)
        || (request.Cue.Kind is CueKind.Video or CueKind.WindowCapture ? incoming.HasVideoFrame : incoming.IsPlaying
            || request.PauseAfter && incoming.HasLoadedMedia);

    private void Tick()
    {
        if (_disposed) return;
        if (_loading is { } loading && _incoming is { } incoming)
        {
            if (!IncomingReady(loading, incoming))
            {
                if (_backgroundFading && _backgroundClock.Elapsed.TotalMilliseconds >= _backgroundFadeMilliseconds) ReleaseBackground();
                ApplyVolumes();
                if (_clock.Elapsed.TotalSeconds >= 15) SurfaceFailed(incoming, "El nuevo medio no entregó imagen o audio.");
                return;
            }
            if (_desiredPause) incoming.SetPaused(true);
            _outgoing = _current; _outgoingCue = _currentCue;
            _current = incoming; _currentCue = loading.Cue;
            _visible = loading;
            _incoming = null; _loading = null; _blend = loading;
            _clock.Restart();
        }
        if (_blend is { } blend && _outgoing is not null)
        {
            var sample = Sample(blend);
            _outgoing.Opacity = 1 - sample.VisualProgress;
            ApplyTransforms(blend.Options.Kind, sample.VisualProgress);
            ApplyVolumes();
            if (sample.Complete) FinishBlend();
        }
        if (_backgroundFading && _backgroundClock.Elapsed.TotalMilliseconds >= _backgroundFadeMilliseconds) ReleaseBackground();
        ApplyVolumes();
        if (_loading is null && _blend is null && !_backgroundFading) _timer.Stop();
    }

    private CrossfadeSample Sample(Request request) => CrossfadeTimeline.Sample(_clock.Elapsed.TotalMilliseconds,
        request.Options.Kind == "Cut" ? 0 : request.Options.DurationMilliseconds,
        !KeepOutgoingMedia && (_outgoing?.HasLoadedMedia == true || _background is not null && ShowsMedia(request))
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
            _outgoing.Volume = _pauseImmediately ? 0 : KeepOutgoingMedia ? _volume : ProjectionSurface.NativeFadeVolume(_volume, sample.OutgoingGain);
            _current.Volume = _desiredPause ? 0 : blend.ReusedLiveMedia ? _volume : ProjectionSurface.NativeFadeVolume(_volume, sample.IncomingGain);
        }
        else _current.Volume = _pauseImmediately || (_loading is null && _desiredPause) ? 0 : _volume;
        if (_incoming is not null) _incoming.Volume = _loading?.ReusedLiveMedia == true ? _volume : 0;
        if (_retained is not null) _retained.Volume = 0;
        if (_background is not null)
        {
            var gain = _backgroundFading ? Math.Clamp(1 - _backgroundClock.Elapsed.TotalMilliseconds / Math.Max(1, _backgroundFadeMilliseconds), 0, 1) : 1;
            if (_blend is { } replacement && ShowsMedia(replacement)) gain = Math.Min(gain, Sample(replacement).OutgoingGain);
            _background.Volume = _pauseImmediately ? 0 : ProjectionSurface.NativeFadeVolume(_volume, gain);
        }
    }
    private static void ResetVisual(ProjectionSurface surface)
    { surface.Opacity = 1; surface.RenderTransform = Transform.Identity; surface.RenderTransformOrigin = new Point(); }

    private void FinishBlend()
    {
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
                && outgoing.HasLoadedMedia)
            {
                ReleaseRetained();
                outgoing.Volume = 0; outgoing.SetPaused(true);
                _retained = outgoing; _retainedCue = _outgoingCue;
            }
            else Retire(outgoing);
        }
        if (request is { Blackout: false, Hidden: false }) ReleaseRetained();
        if (request is not null && ShowsMedia(request)) ReleaseBackground();
        _outgoingCue = null;
        ApplyVolumes();
        if (_backgroundFading) _timer.Start();
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
            if (_keepMediaAudio && loading?.ReusedLiveMedia == true)
            { _background = pending; _backgroundCue = loading.Cue; }
            else Retire(pending);
        }
        if (_blend is null && !_backgroundFading) _timer.Stop();
    }
    private void ReleaseRetained()
    {
        if (_retained is { } retained) { _retained = null; _retainedCue = null; Retire(retained); }
    }
    private void ReleaseBackground()
    {
        if (_background is { } background) { _background = null; _backgroundCue = null; _backgroundFading = false; Retire(background); }
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
        if (_keepMediaAudio || _background is not null)
        {
            var cut = Capture(cue, blackout, hidden, blackout || hidden);
            Enqueue(cut with { Options = cut.Options with { Kind = "Cut", DurationMilliseconds = 0 } });
            return;
        }
        if (_vlc is null && cue?.Kind is CueKind.Video or CueKind.Audio && !blackout && !hidden)
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
            resumeCapture = cue?.Kind == CueKind.WindowCapture;
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
        _timer.Stop(); _disposed = true;
        var surfaces = Surfaces().ToArray(); Children.Clear();
        foreach (var surface in surfaces) _retired.Add(surface.RetireAsync());
        try { Task.WhenAll(_retired).GetAwaiter().GetResult(); }
        catch (Exception error) { Debug.WriteLine(error); }
        finally
        {
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
