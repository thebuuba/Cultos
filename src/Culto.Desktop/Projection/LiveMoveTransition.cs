using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Culto.Core;

namespace Culto.Desktop.Projection;

// Both source surfaces remain in the compositor. Only their live pixels cross
// the process boundary; audio remains with the existing overlapping decoders.
internal sealed class LiveMoveTransition : IDisposable
{
    private sealed record FrameSource(string Id, FrameworkElement Visual, SharedVideoFrame Map, int Width, int Height, bool Animated,
        CompositedVideo? Video, byte[]? Pixels);
    private readonly Dictionary<string, FrameSource> _sources = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cancel = new();
    private readonly ObsLiveMoveClient _client;
    private readonly DispatcherTimer _capture;
    private readonly Stopwatch _clock = new();
    private readonly Action<string> _error;
    private bool _disposed;
    private int _frameQueued;
    private BitmapSource? _latest;
    public Image Overlay { get; } = new() { Stretch = Stretch.Fill, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    public bool Started { get; private set; }
    public bool Complete { get; private set; }
    public bool Failed { get; private set; }
    public double ElapsedMilliseconds => _clock.Elapsed.TotalMilliseconds;

    public LiveMoveTransition(ObsLiveMoveClient client, ProjectionSurface outgoing, ProjectionSurface incoming,
        Cue? outgoingCue, Cue? incomingCue, TransitionOptions options, int width, int height, Action<string> error)
    {
        _client = client; _error = error;
        _capture = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, (_, _) => Capture(false), Overlay.Dispatcher);
        _capture.Stop();
        _ = Start(outgoing, incoming, outgoingCue, incomingCue, options.Normalize(), width, height);
    }
    private async Task Start(ProjectionSurface outgoing, ProjectionSurface incoming, Cue? from, Cue? to, TransitionOptions options, int width, int height)
    {
        try
        {
            await Overlay.Dispatcher.InvokeAsync(() => { outgoing.UpdateLayout(); incoming.UpdateLayout(); }, DispatcherPriority.Loaded, _cancel.Token);
            if (_disposed) return;
            var before = Describe(outgoing, from, width, height);
            var after = Describe(incoming, to, width, height);
            Capture(true); if (Failed) return; _capture.Start();
            var command = new ObsLiveMoveCommand(width, height, options.DurationMilliseconds, options.Move, before, after);
            await _client.RenderAsync(command, pixels =>
            {
                if (_disposed || _cancel.IsCancellationRequested) return;
                var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); image.Freeze();
                Interlocked.Exchange(ref _latest, image);
                if (Interlocked.Exchange(ref _frameQueued, 1) != 0) return;
                Overlay.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    var frame = Interlocked.Exchange(ref _latest, null); Interlocked.Exchange(ref _frameQueued, 0);
                    if (_disposed || frame is null || Complete) return;
                    if (!Started) { Started = true; _clock.Restart(); }
                    Overlay.Source = frame; Overlay.Visibility = Visibility.Visible;
                }));
            }, _cancel.Token);
            if (!_disposed) { if (!Started) { Started = true; _clock.Restart(); } Complete = true; Overlay.Visibility = Visibility.Collapsed; _capture.Stop(); }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_disposed) { Failed = Complete = true; _capture.Stop(); Overlay.Visibility = Visibility.Collapsed;
                _error("Move en vivo: " + error.Message + " El contenido continúa sin el efecto."); }
        }
    }
    private List<ObsLiveLayer> Describe(ProjectionSurface surface, Cue? cue, int width, int height)
    {
        var layers = surface.DisplayedBlackout || surface.DisplayedHidden ? Array.Empty<LiveSceneLayer>() : surface.GetLiveLayers();
        if (layers.Count == 0)
        {
            // A suppressed surface must be independent of its hidden media key.
            var key = surface.DisplayedBlackout || surface.DisplayedHidden ? "backdrop-" + Guid.NewGuid().ToString("N") : cue?.Id.ToString("N") ?? Guid.NewGuid().ToString("N");
            var frame = Add(key, surface, width, height, cue?.Kind is CueKind.Video or CueKind.WindowCapture or CueKind.BrowserSource);
            return [new(key, frame.Map.MapName, frame.Width, frame.Height, 0, 0, width, height)];
        }
        var scene = cue?.Scene;
        var scale = Math.Min(width / (double)(scene?.Width ?? 1920), height / (double)(scene?.Height ?? 1080));
        var offsetX = (width - (scene?.Width ?? 1920) * scale) / 2;
        var offsetY = (height - (scene?.Height ?? 1080) * scale) / 2;
        var result = new List<ObsLiveLayer>();
        if (scene is not null)
        {
            var paper = new Border { Background = new SolidColorBrush(Color.FromRgb(16, 23, 34)), Width = 4, Height = 4 };
            paper.Measure(new Size(4, 4)); paper.Arrange(new Rect(0, 0, 4, 4));
            var baseFrame = Add("canvas-base", paper, 4, 4, false);
            result.Add(new(baseFrame.Id, baseFrame.Map.MapName, 4, 4, 0, 0, width, height));
            var color = (Color)ColorConverter.ConvertFromString(scene.Background);
            var solid = new Border { Background = new SolidColorBrush(color), Width = 4, Height = 4 };
            solid.Measure(new Size(4, 4)); solid.Arrange(new Rect(0, 0, 4, 4));
            var background = Add("background-" + scene.Background, solid, 4, 4, false);
            result.Add(new(background.Id, background.Map.MapName, 4, 4, offsetX, offsetY, scene.Width * scale, scene.Height * scale));
        }
        var instances = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in layers.Where(l => l.Layer.Visible))
        {
            var frame = Add(item.SourceId, item.Surface, Math.Min(1280, width), Math.Min(720, height),
                item.Cue.Kind is CueKind.Video or CueKind.WindowCapture or CueKind.BrowserSource);
            var identity = instances.Add(item.SourceId) ? item.SourceId : item.SourceId + ":" + item.Layer.Id;
            result.Add(new(identity, frame.Map.MapName, frame.Width, frame.Height,
                offsetX + item.Layer.X * scale, offsetY + item.Layer.Y * scale, item.Layer.Width * scale, item.Layer.Height * scale, item.Layer.Rotation, item.Layer.Opacity));
        }
        return result;
    }
    private FrameSource Add(string id, FrameworkElement visual, int width, int height, bool animated)
    {
        if (_sources.TryGetValue(id, out var existing)) return existing;
        var video = (visual as ProjectionSurface)?.LiveVideoPixels;
        byte[]? pixels = null;
        if (video is not null)
        {
            var size = video.FrameSize;
            // Preserve the source surface's letterboxing, including a shared layer's
            // aspect ratio before the scene stretches/rotates that layer.
            var aspect = visual.ActualWidth > 0 && visual.ActualHeight > 0 ? visual.ActualWidth / visual.ActualHeight : width / (double)height;
            var canvasWidth = Math.Max(size.Width, (int)Math.Ceiling(size.Height * aspect));
            var canvasHeight = Math.Max(size.Height, (int)Math.Ceiling(size.Width / aspect));
            if ((long)canvasWidth * canvasHeight <= ObsMoveProtocol.MaximumPayloadBytes / 4)
            {
                width = canvasWidth; height = canvasHeight;
                pixels = new byte[width * height * 4];
                for (var alpha = 3; alpha < pixels.Length; alpha += 4) pixels[alpha] = 255;
            }
            else video = null; // Unusual, extreme aspect ratios retain the existing compositor path.
        }
        var frame = new FrameSource(id, visual, SharedVideoFrame.Create(width, height), width, height, animated, video, pixels);
        _sources.Add(id, frame); return frame;
    }
    private void Capture(bool all)
    {
        if (_disposed) return;
        try
        {
        foreach (var source in _sources.Values)
        {
            if (!all && !source.Animated) continue;
            if (source.Video is not null && ReferenceEquals((source.Visual as ProjectionSurface)?.LiveVideoPixels, source.Video)
                && source.Video.CopyFrame(source.Pixels!, source.Width, source.Height, all))
            {
                source.Map.Write(source.Pixels!);
                continue;
            }
            var canvas = new DrawingVisual();
            using (var drawing = canvas.RenderOpen()) drawing.DrawRectangle(new VisualBrush(source.Visual) { Stretch = Stretch.Fill }, null, new Rect(0, 0, source.Width, source.Height));
            var bitmap = new RenderTargetBitmap(source.Width, source.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[source.Width * source.Height * 4]; converted.CopyPixels(pixels, source.Width * 4, 0);
            source.Map.Write(pixels);
        }
        }
        catch (Exception error)
        {
            Failed = Complete = true; _capture.Stop(); _cancel.Cancel(); Overlay.Visibility = Visibility.Collapsed;
            _error("No se pudo actualizar una fuente Move. " + error.Message);
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _capture.Stop(); _cancel.Cancel(); Overlay.Source = null;
        foreach (var source in _sources.Values) source.Map.Dispose(); _sources.Clear();
        // The async operation owns the cancellation token until it observes cancellation.
    }
}
