using System.Windows;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private OutputPlayerWindow? _outputPlayerWindow;
    private void ProgramFullscreenClick(object sender, RoutedEventArgs e)
    { OpenOutputPlayer(); _outputPlayerWindow?.EnterFullscreen(); }

    private void OutputMonitorMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        e.Handled = true; OpenOutputPlayer();
    }
    private void OpenOutputPlayer()
    {
        if (_outputPlayerWindow is not null)
        {
            if (_outputPlayerWindow.WindowState == WindowState.Minimized) _outputPlayerWindow.WindowState = WindowState.Normal;
            _outputPlayerWindow.Activate(); return;
        }
        var player = new OutputPlayerWindow();
        _outputPlayerWindow = player;
        player.PauseRequested += () => { TogglePause(); UpdateOutputPlayer(); };
        player.ReturnMediaRequested += ReturnToMedia;
        player.RestartRequested += RestartMedia;
        player.SkipRequested += SkipOutput;
        player.SeekRequested += position =>
        {
            if (CanControlFileMedia)
                _output?.SeekWithFade(position);
        };
        player.VolumeRequested += volume => VolumeSlider.Value = volume;
        player.MuteRequested += () => { ToggleMuteClick(this, new RoutedEventArgs()); UpdateOutputPlayer(); };
        player.LoopRequested += repeat => LoopCheck.IsChecked = repeat;
        player.Closed += (_, _) => { if (ReferenceEquals(_outputPlayerWindow, player)) _outputPlayerWindow = null; };
        UpdateOutputPlayer(); player.Show();
    }

    private void UpdateOutputPlayer()
    {
        var player = _outputPlayerWindow;
        if (player is null) return;
        player.Monitor.SetLocalSource(_output);
        var native = _powerPoint?.IsRunning == true ? LiveMonitor.NativeSource : IntPtr.Zero;
        player.Monitor.SetMirrorSource(native != IntPtr.Zero && _outputDisplay is null ? LiveMonitor : null);
        player.Monitor.SetNativeSource(_outputDisplay is not null ? native : IntPtr.Zero);
        var available = CanControlFileMedia;
        var media = CanControlFileMedia;
        var capture = !HasBackgroundFileAudio && _session.Live?.Kind == CueKind.WindowCapture;
        player.Update(new OutputPlayerState("Imagen: " + VisualChannelTitle + " · Audio: " + AudioChannelTitle,
            available && media, available && media && _output!.Duration > 0,
            _output?.IsPaused ?? true, capture, media ? _output?.Position ?? 0 : 0,
            media ? _output?.Duration ?? 0 : 0, VolumeSlider.Value, LoopCheck.IsChecked == true));
    }
}
