using System;
using System.IO;
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

    public ProjectionSurface(bool preview = false)
    {
        _preview = preview;
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
        _root.Children.Add(_text);
        _root.Children.Add(_preview ? _video : _compositedVideo);
        InitializeAudioFade();
        InitializeDissolve();
        ShowCue(null);
    }

    private static TextBlock Text(double size) => new()
    {
        FontFamily = new FontFamily("Segoe UI"), FontSize = size, Foreground = Brushes.White,
        TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
    };

    public int Volume
    {
        get => _preview ? 0 : _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            ApplyAudioVolume();
        }
    }
    public bool Loop { get; set; }
    public double Position => _player is null || _disposed ? 0 : Math.Max(0, _player.Time / 1000d);
    public double Duration => _player is null || _disposed ? 0 : Math.Max(0, _player.Length / 1000d);
    public bool IsPlaying => !_disposed && _player?.IsPlaying == true;

    public void ShowCue(Cue? cue, bool blackout = false, bool hideText = false)
        => Present(new Presentation(cue, blackout, hideText, false));

    private void ShowCueCore(Cue? cue, bool blackout = false, bool hideText = false, bool paused = false)
    {
        if (_disposed) return;
        CancelDissolve();
        _cueFailed = false;
        _suppressed = blackout || hideText;
        _root.Background = blackout ? Brushes.Black : BackgroundBrush;
        _image.Visibility = _text.Visibility = _video.Visibility = _compositedVideo.Visibility = Visibility.Collapsed;
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
        if (paused) _pauseRequested = true;
        if (_suppressed) return;

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
                case CueKind.Bible:
                    ShowText(string.IsNullOrWhiteSpace(cue.Body) ? cue.Title : cue.Body, cue.Reference, cue.Version);
                    break;
                case CueKind.Image:
                    RequireFile(cue.FilePath);
                    using (var stream = File.Open(cue.FilePath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                        _image.Source = bitmap;
                    }
                    _image.Visibility = Visibility.Visible;
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or VLCException)
        {
            ReportError("No se pudo mostrar el contenido. " + ex.Message);
        }
    }

    private static void RequireFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("El archivo ya no está disponible.");
    }

    private void ShowText(string body, string reference = "", string version = "")
    {
        _text.Visibility = Visibility.Visible;
        _body.Text = body; _reference.Text = reference; _version.Text = version;
        _reference.Visibility = string.IsNullOrWhiteSpace(reference) ? Visibility.Collapsed : Visibility.Visible;
        _version.Visibility = string.IsNullOrWhiteSpace(version) ? Visibility.Collapsed : Visibility.Visible;
        // Fit wrapped text to a full-width 16:9 canvas, keeping room for citations.
        double small = 1, large = 76;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            double size = (small + large) / 2;
            _body.FontSize = size; _reference.FontSize = size * .56; _version.FontSize = size * .39;
            _words.InvalidateMeasure();
            _words.Measure(new Size(1400, double.PositiveInfinity));
            if (_words.DesiredSize.Height > 760) large = size; else small = size;
        }
        _body.FontSize = small; _reference.FontSize = small * .56; _version.FontSize = small * .39;
    }

    private void EnsurePlayer()
    {
        if (_player is not null) return;
        _vlc = new LibVLC("--no-video-title-show", "--quiet");
        _player = new MediaPlayer(_vlc) { Volume = 0, Mute = true, EnableMouseInput = false, EnableKeyInput = false };
        if (_preview) _video.MediaPlayer = _player;
        else _compositedVideo.Attach(_player);
        _player.EncounteredError += OnPlaybackError;
        _player.Playing += OnPlaying;
        _player.EndReached += OnEndReached;
    }

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
                FadeAudio(1, 350);
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
        else if (_pendingPresentation is not null) return;
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
                FadeAudio(1, 350);
            }
        }
    }

    public void Restart()
    {
        if (_disposed || _player is null || _media is null) return;
        if (_pendingPresentation is not null) return; // The pending cue will start at its beginning.
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
        if (_disposed || _player is null || _pendingPresentation is not null || !double.IsFinite(fraction)) return;
        _player.Position = (float)Math.Clamp(fraction, 0, 1);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _pendingPresentation = null;
        _playbackGeneration++;
        CancelAudioFade(silence: true);
        CancelDissolve();
        _disposed = true;
        if (_player is not null)
        {
            _player.EncounteredError -= OnPlaybackError; _player.Playing -= OnPlaying; _player.EndReached -= OnEndReached;
            _player.Stop(); _video.MediaPlayer = null; _player.Dispose();
        }
        _video.Dispose(); _compositedVideo.Dispose(); _media?.Dispose(); _vlc?.Dispose();
        _image.Source = null;
    }
}
