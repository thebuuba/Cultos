using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private CancellationTokenSource? _youTubeLinkProjection;

    private async void PasteYouTubeLinkClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText()) { SetStatus("Copia primero el enlace de un video de YouTube."); return; }
            await AddYouTubeLinksToOrderAsync(Clipboard.GetText());
        }
        catch (ExternalException) { SetStatus("No se pudo leer el portapapeles. Intenta pegar el enlace de nuevo.", true); }
    }

    private async Task<int> AddYouTubeLinksToOrderAsync(string text, bool readMetadata = true)
    {
        var links = text.Split(new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => YouTubeLink.TryParse(value, out var link) ? link : null).OfType<YouTubeLink>().Take(50).ToArray();
        if (links.Length == 0) { SetStatus("Pega un enlace de video de YouTube válido.", true); return 0; }
        var added = new List<Cue>();
        foreach (var link in links)
        {
            var cue = new Cue { Kind = CueKind.WindowCapture, Title = "Video de YouTube · " + link.VideoId, YouTubeUrl = link.Url };
            _plan.Add(cue); added.Add(cue);
        }
        UpdatePlanCount();
        if (!SaveWorkspace())
        {
            foreach (var cue in added) _plan.Remove(cue);
            UpdatePlanCount(); return 0;
        }
        PlanList.SelectedItem = added[0]; PlanList.ScrollIntoView(added[0]);
        SetStatus(added.Count == 1 ? "Video de YouTube añadido al orden. Doble clic para proyectarlo." : $"{added.Count} videos de YouTube añadidos al orden.");
        if (readMetadata)
        {
            foreach (var cue in added)
            {
                if (!YouTubeLink.TryParse(cue.YouTubeUrl, out var link)) continue;
                var title = await YouTubeVideoMetadata.ReadTitleAsync(link!);
                if (_closing || !_plan.Contains(cue) || string.IsNullOrWhiteSpace(title)) continue;
                var index = _plan.IndexOf(cue);
                var updated = cue.Copy();
                updated.Title = title;
                var selected = ReferenceEquals(PlanList.SelectedItem, cue);
                _plan[index] = updated;
                if (selected) PlanList.SelectedItem = updated;
                SaveWorkspace();
            }
        }
        return added.Count;
    }

    private async Task ProjectYouTubeLinkAsync(Cue cue, bool activate, bool preserve)
    {
        _youTubeLinkProjection?.Cancel(); _youTubeLinkProjection?.Dispose();
        var request = _youTubeLinkProjection = new CancellationTokenSource();
        var timing = System.Diagnostics.Stopwatch.StartNew();
        var timingId = Guid.NewGuid().ToString("N")[..8];
        void Record(string stage)
        {
            try
            {
                var path = Path.Combine(_store.DirectoryPath, "youtube-timing.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256_000) File.WriteAllText(path, "");
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {timingId} {stage} {timing.ElapsedMilliseconds}ms\n");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        async Task ObserveVisibleAsync()
        {
            while (!request.IsCancellationRequested && !_closing && _session.Live?.Id == cue.Id)
            {
                if (_output?.DisplayedCue?.Id == cue.Id && !_output.IsTransitioning) { Record("visible"); return; }
                if (timing.Elapsed > TimeSpan.FromSeconds(30)) return;
                await Task.Delay(100, request.Token);
            }
        }
        try
        {
            Record("requested");
            SetStatus("Cargando YouTube: " + cue.Title + "…");
            var preferred = (YouTubeList.SelectedItem as YouTubeSource)?.Cue.Capture ?? _session.Live?.Capture;
            var target = await YouTubeProjectionBrowser.NavigateAsync(_store.DirectoryPath, cue.YouTubeUrl!, request.Token, preferred);
            Record("navigated");
            if (_closing || request.IsCancellationRequested || !ReferenceEquals(request, _youTubeLinkProjection)) return;
            var resolved = cue.Copy();
            resolved.Capture = _workspace.YouTubeLayout?.ApplyTo(target) ?? target;
            _session.Prepare(resolved);
            ProjectPreparedCore(activate, preserve, resolveYouTube: false);
            Record("projected");
            if (_session.Live?.Id == cue.Id)
            {
                await Task.WhenAll(ObserveVisibleAsync(), YouTubeProjectionBrowser.EnsurePlaybackAsync(target, request.Token));
                Record("playing");
            }
        }
        catch (OperationCanceledException) { if (!request.IsCancellationRequested) SetStatus("YouTube tardó demasiado en responder. Inténtalo de nuevo.", true); }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or System.Net.Http.HttpRequestException
            or COMException or System.Windows.Automation.ElementNotAvailableException or System.ComponentModel.Win32Exception)
        { Record("failed:" + error.GetType().Name); if (!request.IsCancellationRequested) SetStatus((_session.Live?.Id == cue.Id ? "YouTube está en Programa. " : "No se pudo proyectar YouTube. ") + error.Message, true); }
        finally
        {
            if (ReferenceEquals(request, _youTubeLinkProjection)) _youTubeLinkProjection = null;
            request.Dispose();
        }
    }
}
