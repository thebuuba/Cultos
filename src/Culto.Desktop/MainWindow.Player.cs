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
    }

    private static bool IsSeekKey(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down
        or Key.Home or Key.End or Key.PageUp or Key.PageDown;

    private void WireSeek(Slider slider)
    {
        slider.IsMoveToPointEnabled = true;
        slider.SmallChange = .01; slider.LargeChange = .1;
        void Scrubbing(bool value) => _outputScrubbing = value;
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
            if (!e.Canceled) { SeekOutput(); }
            Scrubbing(false);
        }));
        slider.LostMouseCapture += (_, _) => Scrubbing(false);
        slider.PreviewKeyDown += (_, e) => { if (IsSeekKey(e.Key)) Scrubbing(true); };
        slider.KeyUp += (_, e) => { if (IsSeekKey(e.Key)) Scrubbing(false); };
        slider.LostKeyboardFocus += (_, _) => Scrubbing(false);
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
        var label = muted ? "Restaurar volumen" : "Silenciar salida";
        MuteButton.ToolTip = label;
        AutomationProperties.SetName(MuteButton, label);
    }

    private void ToggleMuteClick(object sender, RoutedEventArgs e)
    {
        if (VolumeSlider.Value > 0) { _audibleVolume = VolumeSlider.Value; VolumeSlider.Value = 0; }
        else VolumeSlider.Value = _audibleVolume;
    }

    private void SeekOutput()
    {
        if (_output is not null && _session.Live?.Kind is CueKind.Video or CueKind.Audio
            && !_session.IsBlackout && !_session.IsTextHidden && _output.Duration > 0)
            _output.Seek(PositionSlider.Value);
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
        if (_session.Live?.Kind is CueKind.Video or CueKind.Audio && !_session.IsBlackout && !_session.IsTextHidden)
            Skip(_output, seconds);
    }
}
