using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private ContentControl? _playbackOverlayHost;
    private bool? _playbackOverlayVisible;
    private bool PlaybackKeyboardFocusHeld => MediaPlaybackPanel.IsKeyboardFocusWithin
        && InputManager.Current.MostRecentInputDevice is KeyboardDevice;

    private bool PreviewCanHostPlayback => _session.PreviewFollowsLive
        && !(_preparedBibleStyleChanged && _session.Prepared?.Kind == CueKind.Bible)
        && PreviewLiveMonitor?.Visibility == Visibility.Visible;

    private void InitializePlaybackOverlay()
    {
        if (MediaPlaybackPanel is null || LiveOverlayHost is null) return;
        PlacePlaybackOverlay(LiveOverlayHost);
        ShowPlaybackOverlay(false);
        MediaPlaybackPanel.PreviewKeyDown += (_, _) => ShowPlaybackOverlay(true);
        MediaPlaybackPanel.GotMouseCapture += (_, _) => UpdatePlaybackOverlay();
        MediaPlaybackPanel.LostMouseCapture += (_, _) => DeferPlaybackOverlayUpdate();
    }

    private void PlacePlaybackOverlay(ContentControl host)
    {
        if (MediaPlaybackPanel is null || LiveOverlayHost is null || host is null) return;
        if (ReferenceEquals(host, PreviewOverlayHost) && !PreviewCanHostPlayback) host = LiveOverlayHost;
        if (ReferenceEquals(host.Content, MediaPlaybackPanel))
        {
            _playbackOverlayHost = host;
            return;
        }
        if (MediaPlaybackPanel.Parent is ContentControl previous) previous.Content = null;
        else if (MediaPlaybackPanel.Parent is Panel previousPanel) previousPanel.Children.Remove(MediaPlaybackPanel);
        host.Content = MediaPlaybackPanel;
        _playbackOverlayHost = host;
    }

    private void ShowPlaybackOverlay(bool visible)
    {
        if (MediaPlaybackPanel is null) return;
        MediaPlaybackPanel.IsHitTestVisible = visible;
        if (_playbackOverlayVisible == visible) return;
        _playbackOverlayVisible = visible;
        UiMotion.Fade(MediaPlaybackPanel, visible ? 1 : 0);
    }

    private void UpdatePlaybackOverlay()
    {
        if (MediaPlaybackPanel is null || LiveOverlayHost is null) return;
        if (_playbackOverlayHost is null || ReferenceEquals(_playbackOverlayHost, PreviewOverlayHost) && !PreviewCanHostPlayback)
            PlacePlaybackOverlay(LiveOverlayHost);

        var held = _outputScrubbing || MediaPlaybackPanel.IsMouseCaptureWithin || PlaybackKeyboardFocusHeld;
        if (!held)
        {
            if (PreviewCanHostPlayback && PreviewViewport?.IsMouseOver == true) PlacePlaybackOverlay(PreviewOverlayHost);
            else if (OutputViewport?.IsMouseOver == true) PlacePlaybackOverlay(LiveOverlayHost);
        }
        var viewport = ReferenceEquals(_playbackOverlayHost, PreviewOverlayHost) ? PreviewViewport : OutputViewport;
        ShowPlaybackOverlay(held || viewport?.IsMouseOver == true);
    }

    private void PlaybackViewportMouseEnter(object sender, MouseEventArgs e)
    {
        if (MediaPlaybackPanel is null || LiveOverlayHost is null) return;
        var preview = ReferenceEquals(sender, PreviewViewport);
        if (preview && !PreviewCanHostPlayback) return;
        if (!_outputScrubbing && !MediaPlaybackPanel.IsMouseCaptureWithin && !PlaybackKeyboardFocusHeld)
            PlacePlaybackOverlay(preview ? PreviewOverlayHost : LiveOverlayHost);
        UpdatePlaybackOverlay();
    }

    private void PlaybackViewportMouseLeave(object sender, MouseEventArgs e) => DeferPlaybackOverlayUpdate();

    private void PlaybackViewportGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (LiveOverlayHost is null) return;
        if (!ReferenceEquals(sender, PreviewViewport) || PreviewCanHostPlayback)
            PlacePlaybackOverlay(ReferenceEquals(sender, PreviewViewport) ? PreviewOverlayHost : LiveOverlayHost);
        UpdatePlaybackOverlay();
    }

    private void PlaybackViewportLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => DeferPlaybackOverlayUpdate();

    // Capture and focus routed events can arrive before their Within properties settle.
    private void DeferPlaybackOverlayUpdate() => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(UpdatePlaybackOverlay));
}
