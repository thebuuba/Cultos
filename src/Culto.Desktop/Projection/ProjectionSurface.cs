using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface : UserControl, IDisposable
{
    private readonly bool _preview;
    private readonly Func<LibVLC>? _sharedVlc;
    private readonly ProjectionMixer? _mixer;
    private Cue? _shownCue;
    private bool _loop;
    private string? _backgroundImagePath;
    private readonly Grid _root = new() { Background = BackgroundBrush, ClipToBounds = true };
    private readonly TextBlock _body = Text(72);
    private readonly TextBlock _reference = Text(40);
    private readonly TextBlock _version = Text(28);
    private readonly Viewbox _text;
    private readonly StackPanel _words;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly VideoView _video = new() { Visibility = Visibility.Collapsed };
    private readonly CompositedVideo _compositedVideo = new() { Visibility = Visibility.Collapsed };
    private LibVLC? _vlc;
    private MediaPlayer? _player;
    private Media? _media;
    private string? _mediaKey;
    private bool _suppressed;
    private bool _pauseRequested;
    private bool _started;
    private bool _disposed;
    private int _volume = 80;
    private static readonly Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(16, 23, 34));

    public event Action<string>? PlaybackError;

    public ProjectionSurface(bool preview = false, bool smoothTransitions = false, Func<LibVLC>? sharedVlc = null)
    {
        _preview = preview;
        _sharedVlc = sharedVlc;
        _words = new StackPanel { Width = 1400, VerticalAlignment = VerticalAlignment.Center };
        _words.Children.Add(_body);
        _reference.Margin = new Thickness(0, 48, 0, 0);
        _words.Children.Add(_reference);
        _version.Margin = new Thickness(0, 18, 0, 0);
        _words.Children.Add(_version);
        var canvas = new Grid { Width = 1600, Height = 900 };
        canvas.Children.Add(_words);
        _text = new Viewbox { Stretch = Stretch.Uniform, Child = canvas, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _root.Children.Add(_image);
        _root.Children.Add(_bibleDim);
        _root.Children.Add(_text);
        _root.Children.Add(_preview ? _video : _compositedVideo);
        InitializeAudioFade();
        InitializeDissolve();
        ShowCue(null);
        if (smoothTransitions && !preview)
        {
            _mixer = new ProjectionMixer();
            _mixer.PlaybackError += message => PlaybackError?.Invoke(message);
            Content = _mixer;
        }
    }

    private static TextBlock Text(double size) => new()
    {
        FontFamily = new FontFamily("Segoe UI"), FontSize = size, Foreground = Brushes.White,
        TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
    };

    public int Volume
    {
        get => _mixer?.Volume ?? (_preview ? 0 : _volume);
        set
        {
            if (_mixer is not null) { _mixer.Volume = value; return; }
            _volume = Math.Clamp(value, 0, 100);
            ApplyAudioVolume();
            ApplyBrowserAudioMute();
        }
    }
    public bool Loop { get => _mixer?.Loop ?? _loop; set { _loop = value; if (_mixer is not null) _mixer.Loop = value; } }
    public bool KeepMediaAudio { get => _mixer?.KeepMediaAudio == true; set { if (_mixer is not null) _mixer.KeepMediaAudio = value; } }
    public Cue? AudioCue => _mixer?.AudioCue ?? (_mixer is null && HasLoadedMedia && !HasEnded && _shownCue?.Kind is CueKind.Video or CueKind.Audio ? _shownCue : null);
    public void TransitionVisual(Cue? cue, bool blackout = false, bool hideText = false)
    {
        if (_mixer is not null) _mixer.TransitionVisual(cue, blackout, hideText);
        else TransitionToBackdrop(cue, blackout, hideText);
    }
    public void PlayAudioKeepingVisual(Cue cue) => _mixer?.PlayAudioKeepingVisual(cue);
    public Cue? RetainedMediaCue => _mixer?.RetainedMediaCue;
    public bool HasBackgroundMedia => _mixer?.HasBackgroundMedia == true;
    public string? BackgroundImagePath { get => _mixer?.BackgroundImagePath ?? _backgroundImagePath;
        set { _backgroundImagePath = value; if (_mixer is not null) _mixer.BackgroundImagePath = value; } }
    public double Position => _mixer?.Position ?? (_player is null || _disposed ? 0 : Math.Max(0, _player.Time / 1000d));
    public double Duration => _mixer?.Duration ?? (_player is null || _disposed ? 0 : Math.Max(0, _player.Length / 1000d));
    public bool IsPlaying => !_disposed && (_mixer?.IsPlaying ?? (_browserSource is not null ? _browserSource.IsReady && !_pauseRequested && !_suppressed : _captureClient is not null
        ? _captureReady && !_pauseRequested && !_suppressed : _player?.IsPlaying == true));
    public bool HasEnded => _mixer is not null ? !_mixer.IsTransitioning && _mixer.ActiveSurface.HasEnded : _ended;
    public bool IsPreparingPlaybackEngine => _mixer?.IsPreparingPlaybackEngine == true;
    public void PreparePlaybackEngine() => _mixer?.PreparePlaybackEngine();
    internal bool HasVideoFrame => _browserSource is not null ? _browserSource.IsReady : _captureClient is not null ? _captureReady : _compositedVideo.Source is not null;
    internal bool HasLoadedMedia => _media is not null || _captureClient is not null || _browserSource is not null;
    public bool DisplayedBlackout => _mixer?.DisplayedBlackout ?? _suppressed;
    public bool DisplayedHidden => _mixer?.DisplayedHidden ?? false;
    public ProjectionSurface ActiveSurface => _mixer?.ActiveSurface ?? this;
    public Cue? DisplayedCue => _mixer?.DisplayedCue ?? _shownCue;
    public TransitionOptions PresentedTransitions => _mixer?.PresentedTransitions ?? _presentedTransitions with { };

    public void ShowCue(Cue? cue, bool blackout = false, bool hideText = false)
    {
        if (_mixer is not null) _mixer.ShowCue(cue, blackout, hideText);
        else Present(CreatePresentation(cue, blackout, hideText, false));
    }

    private void ShowCueCore(Cue? cue, bool blackout = false, bool hideText = false, bool paused = false)
    {
        if (_disposed) return;
        _shownCue = cue;
        if (cue?.Kind != CueKind.WindowCapture) StopWindowCapture();
        if (cue?.Kind != CueKind.BrowserSource) StopBrowserSource();
        if (_browserSource is not null) _browserSource.Visibility = Visibility.Collapsed;
        CancelDissolve();
        _cueFailed = false;
        _suppressed = blackout || hideText;
        _root.Background = blackout || (!hideText && cue?.Kind == CueKind.Video) ? Brushes.Black : BackgroundBrush;
        _image.Visibility = _text.Visibility = _video.Visibility = _compositedVideo.Visibility = Visibility.Collapsed;
        ResetVisualStyle();
        if (_suppressed) SetPaused(true);

        bool mediaCue = cue?.Kind is CueKind.Video or CueKind.Audio;
        string? key = mediaCue ? $"{cue!.Id}|{cue.Kind}|{cue.FilePath}" : null;
        if (key != _mediaKey)
        {
            _playbackGeneration++;
            CancelAudioFade(silence: true);
            _player?.Stop();
            _compositedVideo.Clear();
            if (_player is not null) _player.Media = null;
            _media?.Dispose(); _media = null;
            _mediaKey = key;
            _started = false;
            _ended = false;
            _pauseRequested = paused || _suppressed;
        }
        if (paused) SetPaused(true);
        if (_suppressed)
        {
            if (!_preview && hideText && !blackout) { _root.Background = Brushes.Black; ShowBackgroundImage(); }
            return;
        }

        try
        {
            if (cue is null)
            {
                if (_preview) ShowText("Vista previa");
                return;
            }
            switch (cue.Kind)
            {
                case CueKind.Text:
                case CueKind.PowerPoint:
                    ShowText(string.IsNullOrWhiteSpace(cue.Body) ? cue.Title : cue.Body, cue.Reference, cue.Version);
                    break;
                case CueKind.Bible:
                    ApplyBibleStyle();
                    ShowText(string.IsNullOrWhiteSpace(cue.Body) ? cue.Title : cue.Body, cue.Reference, cue.Version, _presentedBibleStyle.FontSize, bible: true);
                    break;
                case CueKind.Image:
                    _image.Source = ReadImage(cue.FilePath);
                    _image.Visibility = Visibility.Visible;
                    break;
                case CueKind.BrowserSource:
                    ShowBrowserSource(cue, paused);
                    break;
                case CueKind.WindowCapture:
                    ShowWindowCapture(cue, paused);
                    break;
                case CueKind.Video:
                case CueKind.Audio:
                    RequireFile(cue.FilePath);
                    EnsurePlayer();
                    if (cue.Kind == CueKind.Video)
                    {
                        if (_preview) _video.Visibility = Visibility.Visible;
                        else _compositedVideo.Visibility = Visibility.Visible;
                    }
                    else ShowText(cue.Title);
                    if (_media is null)
                    {
                        _media = new Media(_vlc!, Path.GetFullPath(cue.FilePath!), FromType.FromPath);
                        if (_preview) _media.AddOption(":no-audio");
                        _player!.Media = _media;
                    }
                    if (!_started && !_pauseRequested) Start();
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or VLCException
            || cue?.Kind == CueKind.WindowCapture && ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ReportError("No se pudo mostrar el contenido. " + ex.Message);
        }
    }

    private static void RequireFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("El archivo ya no está disponible.");
    }

    private static BitmapImage ReadImage(string? path)
    {
        RequireFile(path);
        using var stream = File.Open(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream;
        bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }

    private void ShowBackgroundImage()
    {
        _image.Source = null;
        if (string.IsNullOrWhiteSpace(BackgroundImagePath)) return;
        try
        {
            _image.Source = ReadImage(BackgroundImagePath);
            _image.Visibility = Visibility.Visible;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or FileFormatException)
        {
            // A removed or damaged optional backdrop leaves the neutral background and paused media.
            _image.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowText(string body, string reference = "", string version = "", double maximumSize = 76, bool bible = false)
    {
        _text.Visibility = Visibility.Visible;
        _body.Text = body; _reference.Text = reference; _version.Text = version;
        _reference.Visibility = string.IsNullOrWhiteSpace(reference) ? Visibility.Collapsed : Visibility.Visible;
        _version.Visibility = string.IsNullOrWhiteSpace(version) ? Visibility.Collapsed : Visibility.Visible;
        // Fit wrapped text to a full-width 16:9 canvas, keeping room for citations.
        void SetSizes(double size)
        {
            _body.FontSize = size;
            _reference.FontSize = bible ? Math.Clamp(size * .56, 22, 48) : size * .56;
            _version.FontSize = bible ? Math.Clamp(size * .39, 18, 32) : size * .39;
            _words.InvalidateMeasure();
            _words.Measure(new Size(1400, double.PositiveInfinity));
        }
        SetSizes(maximumSize);
        if (_words.DesiredSize.Height <= 760) return;
        double small = 1, large = maximumSize;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            double size = (small + large) / 2;
            SetSizes(size);
            if (_words.DesiredSize.Height > 760) large = size; else small = size;
        }
        SetSizes(small);
    }

    private void EnsurePlayer()
    {
        if (_player is not null) return;
        _vlc = _sharedVlc?.Invoke() ?? CreatePlaybackEngine();
        _player = new MediaPlayer(_vlc) { Volume = 0, Mute = true, EnableMouseInput = false, EnableKeyInput = false };
        if (_preview) _video.MediaPlayer = _player;
        else _compositedVideo.Attach(_player);
        _player.EncounteredError += OnPlaybackError;
        _player.Playing += OnPlaying;
        _player.EndReached += OnEndReached;
        _player.TimeChanged += OnAudioOutputChanged;
    }

    // VLC's default mmdevice output changes a shared Windows audio session.
    // Muting/preloading one decoder would also mute the live decoder. DirectSound
    // controls each decoder's own buffer, including independent volume and mute.
    internal static LibVLC CreatePlaybackEngine() => new("--no-video-title-show", "--quiet",
        "--aout=directsound", "--no-volume-save");

    private void OnPlaybackError(object? sender, EventArgs args)
    {
        var generation = System.Threading.Volatile.Read(ref _playbackGeneration);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_disposed && generation == _playbackGeneration && _pendingPresentation is null)
                ReportError("No se pudo reproducir el archivo. Compruebe que sea compatible y esté disponible.");
        }));
    }

    private void OnPlaying(object? sender, EventArgs args)
    {
        var generation = System.Threading.Volatile.Read(ref _playbackGeneration);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_disposed || _player is null || generation != _playbackGeneration) return;
            if (_suppressed || _pauseRequested)
            {
                CancelAudioFade(silence: true);
                _player.SetPause(true);
                return;
            }
            if (_pendingPresentation is not null) return;
            if (_awaitingAudioStart)
            {
                _awaitingAudioStart = false;
                ApplyAudioVolume();
                FadeAudio(1, _presentedTransitions.AudioFadeInMilliseconds);
            }
            else ApplyAudioVolume();
        }));
    }

    private void OnEndReached(object? sender, EventArgs args)
    {
        var generation = System.Threading.Volatile.Read(ref _playbackGeneration);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_disposed || generation != _playbackGeneration || _pendingPresentation is not null) return;
            _ended = true;
            _started = false;
            CancelAudioFade(silence: true);
            if (Loop && !_suppressed && !_pauseRequested) Restart();
            else _pauseRequested = true;
        }));
    }

    private void ReportError(string message)
    {
        if (_disposed) return;
        _cueFailed = true;
        _pendingPresentation = null;
        _playbackGeneration++;
        CancelAudioFade(silence: true);
        _pauseRequested = true;
        _started = false;
        CancelDissolve();
        _player?.Stop();
        if (_browserSource is not null) { _browserSource.SetMuted(true); _browserSource.Visibility = Visibility.Collapsed; }
        _video.Visibility = _compositedVideo.Visibility = _image.Visibility = Visibility.Collapsed;
        if (!_suppressed) ShowText("No se pudo mostrar el contenido");
        PlaybackError?.Invoke(message);
    }

    private void Start()
    {
        if (_player is null || _media is null) return;
        _playbackGeneration++;
        BeginAudioPlayback();
        if (_ended) { _player.Stop(); _ended = false; }
        _started = _player.Play();
        if (!_started) ReportError("No se pudo iniciar la reproducción del archivo.");
    }

    public void SetPaused(bool paused)
    {
        if (_mixer is not null) { _mixer.SetPaused(paused); return; }
        if (_disposed) return;
        var wasPaused = _pauseRequested;
        _pauseRequested = paused || _suppressed;
        if (_pauseRequested)
        {
            var pending = _pendingPresentation;
            _pendingPresentation = null;
            CancelAudioFade(silence: true);
            if (pending is not null) ApplyPresentation(pending, paused: true);
            if (_waitingForFrame) CancelDissolve();
        }
        else if (_pendingPresentation is { } queued)
        {
            if (!queued.Blackout && !queued.HideText)
                _pendingPresentation = queued with { PauseAfter = false };
            return;
        }
        if (_browserSource is not null) { ApplyBrowserAudioMute(); return; }
        if (_captureClient is not null)
        {
            if (!_pauseRequested) FadeAudio(1, _presentedTransitions.AudioFadeInMilliseconds);
            else ApplyCapturedAudioVolume();
            return;
        }
        if (_player is null) return;
        if (!_pauseRequested && !_started) Start();
        else
        {
            if (!_pauseRequested && (wasPaused || !_player.IsPlaying)) BeginAudioPlayback();
            _player.SetPause(_pauseRequested);
            if (!_pauseRequested && _awaitingAudioStart && _player.IsPlaying)
            {
                _awaitingAudioStart = false;
                ApplyAudioVolume();
                FadeAudio(1, _presentedTransitions.AudioFadeInMilliseconds);
            }
        }
    }

    public void Restart()
    {
        if (_mixer is not null) { _mixer.Restart(); return; }
        if (_disposed || _player is null || _media is null) return;
        if (_pendingPresentation is { } pending)
        {
            // A new cue already starts at its beginning. A backdrop restore needs an explicit restart.
            if (pending.Blackout || pending.HideText) return;
            var cue = pending.Cue;
            var key = cue?.Kind is CueKind.Video or CueKind.Audio ? $"{cue.Id}|{cue.Kind}|{cue.FilePath}" : null;
            if (!pending.PauseAfter && key != _mediaKey) return;
            _pendingPresentation = null;
            CancelAudioFade(silence: true);
            ApplyPresentation(pending with { PauseAfter = false });
        }
        _playbackGeneration++;
        CancelAudioFade(silence: true);
        _player.Stop();
        _started = false;
        _ended = false;
        _pauseRequested = _suppressed;
        if (!_suppressed) Start();
    }

    public void Seek(double fraction)
    {
        if (_mixer is not null) { _mixer.Seek(fraction); return; }
        if (_disposed || _player is null || _pendingPresentation is not null || !double.IsFinite(fraction)) return;
        _player.Position = (float)Math.Clamp(fraction, 0, 1);
    }

    public void Dispose() => DisposePlayback(background: false).GetAwaiter().GetResult();
    internal Task RetireAsync() => DisposePlayback(background: true);

    private Task DisposePlayback(bool background)
    {
        if (_disposed) return Task.CompletedTask;
        _pendingPresentation = null;
        _playbackGeneration++;
        CancelAudioFade(silence: true);
        CancelDissolve();
        _disposed = true;
        StopWindowCapture();
        StopBrowserSource();
        _mixer?.Dispose();
        _moveClient?.Dispose(); _moveClient = null;
        if (_player is not null)
        {
            _player.EncounteredError -= OnPlaybackError; _player.Playing -= OnPlaying; _player.EndReached -= OnEndReached;
            _player.TimeChanged -= OnAudioOutputChanged;
            _video.MediaPlayer = null;
        }
        _video.Dispose(); _compositedVideo.BeginDispose();
        _image.Source = null;
        void ReleaseNative()
        {
            try { _player?.Stop(); }
            finally
            {
                _player?.Dispose(); _media?.Dispose(); _compositedVideo.FinishDispose();
                if (_sharedVlc is null) _vlc?.Dispose();
            }
        }
        if (background) return Task.Run(ReleaseNative);
        ReleaseNative();
        return Task.CompletedTask;
    }
}
