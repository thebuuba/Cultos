using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private ObsMoveTransitionClient? _moveClient;
    private CancellationTokenSource? _moveCancellation;
    private sealed record NativeMoveFrame(int Generation, BitmapSource Image);
    private NativeMoveFrame? _latestMoveFrame;
    private int _moveFrameQueued;

    private async void BeginNativeMove()
    {
        var generation = _dissolveGeneration;
        var cancellation = new CancellationTokenSource();
        _moveCancellation = cancellation;
        var folder = Path.Combine(Path.GetTempPath(), "CultoMove", Guid.NewGuid().ToString("N"));
        try
        {
            if (_dissolve.Source is not BitmapSource outgoing) throw new InvalidOperationException("No hay una imagen de salida preparada para Move.");
            var move = _presentedTransitions.Move.Normalize();
            // Source changes are rendered asynchronously. Capturing in the same
            // turn can send the old image as both the outgoing and incoming scene.
            await Dispatcher.InvokeAsync(_root.UpdateLayout, System.Windows.Threading.DispatcherPriority.Loaded, cancellation.Token);
            if (_disposed || generation != _dissolveGeneration) return;
            // libobs aligns BGRA output width to four pixels and height to two.
            // Render on that canvas; the overlay scales it back into the viewport.
            var width = ((int)Math.Ceiling(ActualWidth) + 3) & ~3;
            var height = ((int)Math.Ceiling(ActualHeight) + 1) & ~1;
            var incoming = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            incoming.Render(_root); incoming.Freeze();
            Directory.CreateDirectory(folder);
            var before = Path.Combine(folder, "outgoing.png");
            var after = Path.Combine(folder, "incoming.png");
            await Task.Run(() => { WriteMoveFrame(outgoing, before); WriteMoveFrame(incoming, after); }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (_disposed || generation != _dissolveGeneration) return;
            _moveClient ??= new ObsMoveTransitionClient();
            await _moveClient.RenderAsync(before, after, width, height, _dissolveMilliseconds, pixels =>
            {
                if (cancellation.IsCancellationRequested) return;
                var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, checked(width * 4));
                image.Freeze();
                if (cancellation.IsCancellationRequested) return;
                Interlocked.Exchange(ref _latestMoveFrame, new NativeMoveFrame(generation, image));
                if (Interlocked.Exchange(ref _moveFrameQueued, 1) != 0) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var frame = Interlocked.Exchange(ref _latestMoveFrame, null);
                    Interlocked.Exchange(ref _moveFrameQueued, 0);
                    if (!_disposed && frame?.Generation == _dissolveGeneration) _dissolve.Source = frame.Image;
                }));
            }, cancellation.Token, move);
            if (!_disposed && generation == _dissolveGeneration) CancelDissolve();
        }
        catch (OperationCanceledException)
        {
            if (!cancellation.IsCancellationRequested && !_disposed && generation == _dissolveGeneration)
            {
                FailNativeMove("Move no respondió a tiempo.");
            }
        }
        catch (Exception error)
        {
            if (!cancellation.IsCancellationRequested && !_disposed && generation == _dissolveGeneration)
            {
                FailNativeMove("No se pudo ejecutar el complemento Move. " + error.Message);
            }
        }
        finally
        {
            if (ReferenceEquals(_moveCancellation, cancellation)) _moveCancellation = null;
            cancellation.Dispose();
            try
            {
                // This unique directory was created by this invocation; no user files.
                foreach (var name in new[] { "outgoing.png", "incoming.png" }) File.Delete(Path.Combine(folder, name));
                if (Directory.Exists(folder)) Directory.Delete(folder);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private void FailNativeMove(string message)
    {
        CancelDissolve();
        _moveClient?.Dispose(); _moveClient = null;
        // An optional transition failure must not stop valid destination video,
        // silence its audio or replace its image with a media-error placeholder.
        PlaybackError?.Invoke(message + " El contenido se muestra sin el efecto.");
    }

    private static void WriteMoveFrame(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
