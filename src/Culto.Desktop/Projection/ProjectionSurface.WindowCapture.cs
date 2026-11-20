using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private ObsWindowCaptureClient? _captureClient;
    private CancellationTokenSource? _captureCancellation;
    private CapturedApplicationAudio.Lease? _captureAudio;
    private string? _captureKey;
    private bool _captureReady;
    private bool _captureContentAllowed = true;
    private int _captureGeneration, _captureFrameQueued;
    private sealed record CaptureFrame(int Generation, ObsWindowCaptureClient Client, byte[] Pixels);
    private CaptureFrame? _captureLatest;
    private WriteableBitmap? _captureBitmap;
    private string? _captureAudioError;
    internal bool IsCaptureThumbnail { get; init; }
    private int CaptureWidth => IsCaptureThumbnail ? 320 : 1920;
    private int CaptureHeight => IsCaptureThumbnail ? 180 : 1080;

    public void RefreshWindowCapture()
    {
        if(_disposed)return;
        if(_mixer is not null)_mixer.RefreshWindowCapture();
        else if(_captureClient is {} client)_=client.RefreshAsync();
    }

    private void ShowWindowCapture(Cue cue, bool paused)
    {
        if (cue.Capture is null) throw new ArgumentException("La captura no tiene una ventana seleccionada.");
        var key = cue.Id + "|" + cue.Capture;
        if (_captureKey != key || _captureClient is null)
        {
            StopWindowCapture();
            _pauseRequested = paused || _suppressed;
            var source = WindowCaptureCatalog.Resolve(cue.Capture);
            if (!ObsWindowCaptureClient.IsAvailable)
                throw new NotSupportedException("No se encuentra el motor de captura de ventanas.");
            _captureKey = key;
            var cancellation = _captureCancellation = new CancellationTokenSource();
            var client = _captureClient = new ObsWindowCaptureClient();
            if (source.ControlAudio)
                _captureAudio = CapturedApplicationAudio.Shared.Acquire(source.ProcessId);
            _audioGain = 1; _awaitingAudioStart = false;
            ApplyAudioVolume();
            _ = CaptureWindowAsync(client, source, ++_captureGeneration, cancellation);
        }
        _root.Background = Brushes.Black;
        _image.Visibility = Visibility.Visible;
    }

    private async Task CaptureWindowAsync(ObsWindowCaptureClient client, WindowCaptureOptions source,
        int generation, CancellationTokenSource cancellation)
    {
        int failures = 0;
        try
        {
            while (true)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                try
                {
                    // Keep waiting for this exact window during a long minimize.
                    // No new worker or application-audio lease is needed while it is hidden.
                    var available = WindowCaptureCatalog.ResolveForReconnect(source);
                    if (available is null)
                    {
                        await Task.Delay(1000, cancellation.Token);
                        continue;
                    }
                    source = available;
                    var attempt = client;
                    await attempt.CaptureAsync(source, CaptureWidth, CaptureHeight, pixels =>
                    {
                        if (cancellation.IsCancellationRequested || !ReferenceEquals(attempt, _captureClient)) return;
                        Interlocked.Exchange(ref _captureLatest, new CaptureFrame(generation, attempt, pixels));
                        if (Interlocked.Exchange(ref _captureFrameQueued, 1) != 0) return;
                        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            var frame = Interlocked.Exchange(ref _captureLatest, null);
                            Interlocked.Exchange(ref _captureFrameQueued, 0);
                            if (_disposed || frame?.Generation != _captureGeneration || !ReferenceEquals(frame.Client, _captureClient)) return;
                            failures = 0;
                            _captureContentAllowed=WindowCaptureCatalog.IsContentSelected(source);
                            if(!_captureContentAllowed)Array.Clear(frame.Pixels);
                            // Keep one frame available for a paused restore, then freeze image only.
                            if (_captureReady && (_pauseRequested || _suppressed)) return;
                            _captureBitmap ??= new WriteableBitmap(CaptureWidth, CaptureHeight, 96, 96, PixelFormats.Bgra32, null);
                            _captureBitmap.WritePixels(new Int32Rect(0, 0, CaptureWidth, CaptureHeight), frame.Pixels, CaptureWidth * 4, 0);
                            _image.Source = _captureBitmap;
                            _captureReady = true;
                            ApplyAudioVolume();
                        }));
                    }, cancellation.Token);
                    throw new IOException("La captura se cerró antes de recibir la siguiente imagen.");
                }
                catch (Exception error) when (!cancellation.IsCancellationRequested
                    && error is IOException and not FileNotFoundException
                        or TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    client.Dispose();
                    if (_disposed || generation != _captureGeneration) return;
                    // Identity loss is terminal; never substitute another browser window.
                    var available = WindowCaptureCatalog.ResolveForReconnect(source);
                    // Initial preparation still reports unsupported/protected
                    // windows instead of leaving an incoming cue loading forever.
                    if (!_captureReady && available is not null) throw;
                    Interlocked.Exchange(ref _captureLatest, null);
                    await Task.Delay(Math.Min(2000, 500 * ++failures), cancellation.Token);
                    client = new ObsWindowCaptureClient();
                    _captureClient = client;
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_disposed && !cancellation.IsCancellationRequested && generation == _captureGeneration)
            {
                _pauseRequested = true;
                _captureAudio?.SetGain(0);
                _captureClient = null;
                PlaybackError?.Invoke("Captura de ventana: " + error.Message);
            }
        }
        finally { client.Dispose(); }
    }

    private void ApplyCapturedAudioVolume()
    {
        if (_captureAudio is null) return;
        var silent = _disposed || _preview || !_captureReady || !_captureContentAllowed || _suppressed || _pauseRequested;
        // The mixer supplies VLC's native percentage so video and capture share
        // the same master volume. Session volume itself is linear amplitude.
        _captureAudio.SetGain(silent ? 0 : Math.Pow(_volume / 100d, 3) * _audioGain);
        if (_captureAudio.LastError is { } error && error != _captureAudioError)
        {
            _captureAudioError = error;
            var generation = _captureGeneration;
            Dispatcher.BeginInvoke(new Action(() => { if (!_disposed && generation == _captureGeneration) PlaybackError?.Invoke("Audio de captura: " + error); }));
        }
    }

    private void StopWindowCapture()
    {
        if (_captureKey is not null) _image.Source = null;
        _captureGeneration++;
        _captureCancellation?.Cancel();
        _captureCancellation?.Dispose(); _captureCancellation = null;
        _captureClient?.Dispose(); _captureClient = null;
        _captureAudio?.Dispose(); _captureAudio = null;
        _captureKey = null; _captureReady = false; _captureBitmap = null;
        _captureContentAllowed = true;
        _captureAudioError = null;
        Interlocked.Exchange(ref _captureLatest, null);
    }
}
