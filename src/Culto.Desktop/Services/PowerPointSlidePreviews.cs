using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Culto.Desktop.Services;

public sealed record PowerPointSlidePreview(int Number, string Title, string? ImagePath, BitmapSource? Image);

public static class PowerPointSlidePreviews
{
    // Office automation runs serially on an STA worker, away from the console and its live player.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static Task<IReadOnlyList<PowerPointSlidePreview>> LoadAsync(string path,
        IReadOnlyList<PowerPointSlideInfo> metadata, CancellationToken token)
    {
        var result = new TaskCompletionSource<IReadOnlyList<PowerPointSlidePreview>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            bool entered = false;
            try
            {
                Gate.Wait(token); entered = true; token.ThrowIfCancellationRequested();
                var file = new FileInfo(path);
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file.FullName + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length)))[..24];
                var folder = Path.Combine(Path.GetTempPath(), "CultoStudioPowerPointPreviews", key);
                Directory.CreateDirectory(folder);
                var manifest = Path.Combine(folder, "count.txt");
                int count = File.Exists(manifest) && int.TryParse(File.ReadAllText(manifest), out var saved) ? saved : 0;
                if (count <= 0 || Enumerable.Range(1, count).Any(i => !File.Exists(Path.Combine(folder, i + ".png"))))
                {
                    using var presentation = new PowerPointPresentation(); presentation.Open(path);
                    count = presentation.SlideCount;
                    for (int i = 1; i <= count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        presentation.ExportSlidePreview(i, Path.Combine(folder, i + ".png"), 480, 270);
                    }
                    File.WriteAllText(manifest, count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                var slides = new List<PowerPointSlidePreview>();
                for (int i = 1; i <= count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var imagePath = Path.Combine(folder, i + ".png");
                    using var stream = File.OpenRead(imagePath);
                    slides.Add(new(i, metadata.ElementAtOrDefault(i - 1)?.Title ?? $"Diapositiva {i}", imagePath,
                        PowerPointFileMetadata.LoadBitmap(stream, 480)));
                }
                result.TrySetResult(slides);
            }
            catch (OperationCanceledException) { result.TrySetCanceled(token); }
            catch (Exception error) { result.TrySetException(error); }
            finally { if (entered) Gate.Release(); }
        }) { IsBackground = true, Name = "Culto PowerPoint thumbnails" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return result.Task;
    }
}
