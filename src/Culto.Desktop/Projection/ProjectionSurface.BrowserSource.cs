using System;
using System.Windows;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private BrowserSourceView? _browserSource;
    private void ShowBrowserSource(Cue cue, bool paused)
    {
        if (cue.Browser is null) throw new ArgumentException("La fuente navegador no tiene ubicación.");
        _pauseRequested = paused || _suppressed;
        if (_browserSource is null)
        {
            var browser = _browserSource = new BrowserSourceView();
            _root.Children.Add(browser);
            browser.FrameReady += () => { if (_waitingForFrame) BeginDissolve(); ApplyBrowserAudioMute(); };
            browser.Error += message => { if (!_disposed && ReferenceEquals(browser, _browserSource)) ReportError(message); };
        }
        _root.Background = System.Windows.Media.Brushes.Black;
        _browserSource.Visibility = Visibility.Visible;
        _browserSource.SetSource(cue.Browser, _preview || _suppressed || _pauseRequested || _volume == 0);
    }
    public void ReloadBrowserSource(Guid id)
    {
        if (_mixer is not null) { _mixer.ReloadBrowserSource(id); return; }
        if (_shownCue?.Id == id) _browserSource?.Reload();
    }
    private void ApplyBrowserAudioMute() => _browserSource?.SetMuted(_preview || _suppressed || _pauseRequested || _volume == 0);
    private void StopBrowserSource()
    {
        if (_browserSource is not { } browser) return;
        _browserSource = null; _root.Children.Remove(browser); browser.Dispose();
    }
}
