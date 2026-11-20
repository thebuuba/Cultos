using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop.Projection;

namespace Culto.Desktop;

public partial class MainWindow
{
    private double _audibleVolume = 80;
    private bool _outputScrubbing;

    private void InitializePlayerControls()
    {
        WireSeek(PositionSlider);
        PositionSlider.PreviewKeyUp += (_, e) => { if (IsSeekKey(e.Key)) SeekOutput(); };
        UpdatePlayButton(PauseButton, true, false);
        UpdateMuteButton();
        PlayerSeekHint.PlacementTarget = PositionSlider;
        ToolTipService.SetIsEnabled(PositionSlider, false);
        PlayerVolumePopup.Closed += (_, _) => DeferPlaybackOverlayUpdate();
        VolumeSlider.LostMouseCapture += PlayerVolumeMouseLeave;
        VolumeSlider.LostKeyboardFocus += PlayerVolumeMouseLeave;
        MuteButton.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Up) return;
            PlayerVolumePopup.IsOpen = true; VolumeSlider.Focus(); e.Handled = true;
        };
        PlayerVolumePopup.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            PlayerVolumePopup.IsOpen = false; PauseButton.Focus(); e.Handled = true;
        };
        InitializePlaybackOverlay();
    }

    private void PlayerSeekMouseMove(object sender, MouseEventArgs e)
    {
        var duration = _output?.Duration ?? 0;
        if (!PositionSlider.IsEnabled || duration <= 0) { PlayerSeekHint.IsOpen = false; return; }
        var x = e.GetPosition(PositionSlider).X;
        var fraction = Math.Clamp((x - 12) / Math.Max(1, PositionSlider.ActualWidth - 24), 0, 1);
        PlayerSeekHintText.Text = FormatTime(duration * fraction);
        PlayerSeekHint.HorizontalOffset = Math.Clamp(x - 18, 0, Math.Max(0, PositionSlider.ActualWidth - 36));
        PlayerSeekHint.IsOpen = true;
    }
    private void PlayerSeekMouseLeave(object sender, MouseEventArgs e) => PlayerSeekHint.IsOpen = false;
    private void PlayerVolumeMouseEnter(object sender, RoutedEventArgs e)
    {
        PlayerVolumePopup.IsOpen = true;
    }
    private void PlayerVolumeMouseLeave(object sender, RoutedEventArgs e) => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
    {
        if (!MuteButton.IsMouseOver && PlayerVolumePopup.Child?.IsMouseOver != true
            && !VolumeSlider.IsKeyboardFocusWithin && !VolumeSlider.IsMouseCaptureWithin) PlayerVolumePopup.IsOpen = false;
    }));
    private void PlayerFullscreenClick(object sender, RoutedEventArgs e)
    {
        PlayerSeekHint.IsOpen = PlayerVolumePopup.IsOpen = false;
        if (ReferenceEquals(_playbackOverlayHost, PreviewOverlayHost)) PreviewFullscreenClick(sender, e);
        else { OpenOutputPlayer(); _outputPlayerWindow?.EnterFullscreen(); }
    }

    private static bool IsSeekKey(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down
        or Key.Home or Key.End or Key.PageUp or Key.PageDown;

    private void WireSeek(Slider slider)
    {
        Services.SeekSliderDrag.Attach(slider);
        slider.IsMoveToPointEnabled = true;
        slider.SmallChange = .01; slider.LargeChange = .1;
        void Scrubbing(bool value)
        {
            _outputScrubbing = value;
            if (value) UpdatePlaybackOverlay();
            else DeferPlaybackOverlayUpdate();
        }
        slider.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            Scrubbing(true);
            // Slider handles jump-to-point before instance handlers; keep that position
            // stable until release, including when the pointer leaves the track.
            if (e.Handled && !slider.IsMouseCaptureWithin) slider.CaptureMouse();
        }), true);
        slider.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) =>
        {
            if (_outputScrubbing)
            { SeekOutput(); }
            Scrubbing(false);
            if (slider.IsMouseCaptured) slider.ReleaseMouseCapture();
        }), true);
        slider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => Scrubbing(true)));
        slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, e) =>
        {
            if (_outputScrubbing && !e.Canceled) { SeekOutput(); }
            Scrubbing(false);
        }));
        slider.LostMouseCapture += (_, _) => { if (!slider.IsMouseCaptureWithin) Scrubbing(false); };
        slider.PreviewKeyDown += (_, e) => { if (IsSeekKey(e.Key)) Scrubbing(true); };
        slider.KeyUp += (_, e) => { if (IsSeekKey(e.Key)) Scrubbing(false); };
        slider.LostKeyboardFocus += (_, _) => { if (!slider.IsMouseCaptureWithin) Scrubbing(false); };
        slider.ValueChanged += (_, _) =>
        {
            if (_outputScrubbing)
            {
                var duration = _output?.Duration ?? 0;
                TimeText.Text = FormatTime(slider.Value * duration);
            }
        };
    }

    private static void UpdatePlayButton(Button button, bool paused, bool preview)
    {
        button.Content = paused ? "\uE768" : "\uE769";
        var label = (paused ? "Reproducir" : "Pausar") + (preview ? " vista previa" : " salida");
        button.ToolTip = label;
        AutomationProperties.SetName(button, label);
    }

    private void UpdateMuteButton()
    {
        if (MuteButton is null || VolumeSlider is null) return;
        var muted = VolumeSlider.Value <= 0;
        MuteButton.Content = muted ? "\uE74F" : "\uE767";
        var label = muted ? "Restaurar volumen" : "Silenciar el programa";
        MuteButton.ToolTip = label + " · Pasa el cursor para ajustar el volumen. YouTube se controla en Chrome o Windows.";
        AutomationProperties.SetName(MuteButton, label);
    }

    private void ToggleMuteClick(object sender, RoutedEventArgs e)
    {
        if (VolumeSlider.Value > 0) { _audibleVolume = VolumeSlider.Value; VolumeSlider.Value = 0; }
        else VolumeSlider.Value = _audibleVolume;
    }

    private void SeekOutput()
    {
        if (CanControlFileMedia && _output!.Duration > 0)
            _output.SeekWithFade(PositionSlider.Value);
    }

    private static void Skip(ProjectionSurface? player, double seconds)
    {
        if (player is not null && player.Duration > 0)
            player.Seek(Math.Clamp((player.Position + seconds) / player.Duration, 0, 1));
    }
    private void OutputSkipBackClick(object sender, RoutedEventArgs e) => SkipOutput(-10);
    private void OutputSkipForwardClick(object sender, RoutedEventArgs e) => SkipOutput(10);
    private void SkipOutput(double seconds)
    {
        if (CanControlFileMedia)
            Skip(_output, seconds);
    }
}
