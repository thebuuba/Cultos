using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunPlayerControlsTests(MainWindow window)
    {
        var volume = (Slider)window.FindName("VolumeSlider");
        var oldVolume = volume.Value;
        var cue = new Cue { Title = "Reproductor de prueba", Kind = CueKind.Video,
            FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
        try
        {
            Call(window, "CloseOutput"); volume.Value = 0;
            Call(window, "PrepareCue", cue, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            var preview = Field<ProjectionSurface>(window, "_preview");
            Until(() => output.IsPlaying && output.Duration > 0 && !output.IsTransitioning,
                "Player test video did not load");
            output.SetPaused(true);
            PumpFor(100);

            Test("Inline player is one compact neutral row without a live border", () =>
            {
                Call(window,"PlacePlaybackOverlay",window.FindName("LiveOverlayHost"));
                Call(window,"ShowPlaybackOverlay",true);window.UpdateLayout();PumpFor(180);
                var panel=(FrameworkElement)window.FindName("MediaPlaybackPanel");
                var viewport=(Border)window.FindName("OutputViewport");
                Check(viewport.BorderThickness==new Thickness(0),"Green live border remains");
                Check(panel.ActualHeight<=36,"Inline player is taller than one compact row");
                foreach(var name in new[]{"PauseButton","PositionSlider","TimeText","OutputDurationText","MuteButton","VolumeSlider"})
                    CheckWithin((FrameworkElement)window.FindName(name),viewport,new Rect(viewport.RenderSize),name);
                Check(((Button)window.FindName("PauseButton")).Background is System.Windows.Media.SolidColorBrush {Color.A:0},"Large colored play button remains");
                CaptureDesignWindow(window,Path.Combine(Environment.CurrentDirectory,".tools","minimal-player.png"));
            });

            Test("Player scrubbing preserves the selected position and paused output", () =>
            {
                var slider = (Slider)window.FindName("PositionSlider");
                slider.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                slider.Value = .6;
                Call(window, "TimerTick", null, EventArgs.Empty);
                Check(Math.Abs(slider.Value - .6) < .001, "Playback timer overwrote the drag position");
                slider.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                Until(() => Math.Abs(output.Position / output.Duration - .6) < .15, "Dragging did not seek native output");
                Check(output.IsPaused && !output.IsPlaying, "Seeking resumed paused output");
                Check(!Field<bool>(window, "_outputScrubbing"), "Drag did not release seek state");
                slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Handled = true });
                slider.Value = .35;
                Call(window, "TimerTick", null, EventArgs.Empty);
                Check(Math.Abs(slider.Value - .35) < .001, "A handled track click lost its position to the timer");
                slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent, Handled = true });
                Until(() => Math.Abs(output.Position / output.Duration - .35) < .15, "Handled track click did not seek");
                Check(!Field<bool>(window, "_outputScrubbing") && !slider.IsMouseCaptured, "Track release retained scrubbing or mouse capture");
            });

            Test("Player skip controls operate the shared output without a preview decoder", () =>
            {
                output.Seek(.6); PumpFor(150);
                ((Button)window.FindName("OutputSkipBackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => output.Position < .3, "Output rewind did not clamp to the beginning");
                Check(output.IsPaused && !preview.IsPlaying && preview.Duration == 0, "Skip started another player or resumed output");
            });

            Test("Player mute restores chosen volume and play controls expose their state", () =>
            {
                volume.Value = 37;
                var mute = (Button)window.FindName("MuteButton");
                mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(volume.Value == 0 && output.Volume == 0 && AutomationProperties.GetName(mute) == "Restaurar volumen", "Mute did not silence output");
                mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(volume.Value == 37 && output.Volume == 37, "Unmute lost the chosen volume");
                Call(window, "TimerTick", null, EventArgs.Empty);
                var pause = (Button)window.FindName("PauseButton");
                Check(AutomationProperties.GetName(pause) == "Reproducir salida", "Paused playback has no accessible play label");
                pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => output.IsPlaying, "Play control did not resume output");
                Check(AutomationProperties.GetName(pause) == "Pausar salida", "Running playback has no accessible pause label");
                pause.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => !output.IsPlaying, "Pause control did not pause output");
            });

            Test("Logo and black hide and deactivate the retained video controls", () =>
            {
                var session=Field<PresentationSession>(window,"_session");
                var workspace=Field<WorkspaceData>(window,"_workspace");
                var oldBackground=workspace.BackgroundImagePath;
                var imagePath=Path.Combine(Path.GetTempPath(),"CultoPlayerLogo-"+Guid.NewGuid().ToString("N")+".png");
                var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(
                    System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Bgr32,null,new byte[]{0,0,0,255},4)));
                using(var stream=File.Create(imagePath))encoder.Save(stream);
                try
                {
                    workspace.BackgroundImagePath=imagePath;
                    foreach(var keep in new[]{false,true})
                    {
                        output.Seek(0);output.SetPaused(false);
                        Check((bool)Call(window,keep ? "ShowLogoPreservingAudio" : "ShowLogo")!,"Logo did not activate");
                        Until(()=>output.DisplayedHidden && !output.IsTransitioning,"Logo did not finish its transition");
                        foreach(var update in new[]{"UpdateUsabilityState","UpdateBackgroundAudioState"})
                        {
                            Call(window,update);Call(window,"ShowPlaybackOverlay",true);
                            var panel=(FrameworkElement)window.FindName("MediaPlaybackPanel");
                            Check(panel.Visibility == (keep ? Visibility.Visible : Visibility.Collapsed), "Logo transport visibility ignored its audio channel after " + update);
                        }
                        Check(keep ? output.IsPlaying && output.HasBackgroundMedia : !output.IsPlaying, "Logo did not honor its one-shot audio action");
                        Call(window,keep ? "ShowBlackPreservingAudio" : "ShowBlackBackdrop");
                        Until(()=>output.DisplayedBlackout && !output.IsTransitioning,"Black transition did not finish");
                        Call(window,"TimerTick",null,EventArgs.Empty);
                        Check(((FrameworkElement)window.FindName("MediaPlaybackPanel")).Visibility == (keep ? Visibility.Visible : Visibility.Collapsed), "Black transport visibility ignored its audio channel");
                        session.RestoreContent();Call(window,"ApplyBackdropTransition");
                        Until(()=>!output.DisplayedBlackout && !output.DisplayedHidden && !output.IsTransitioning,"Video did not restore");
                        Call(window,"UpdateBackgroundAudioState");
                        Check(((FrameworkElement)window.FindName("MediaPlaybackPanel")).Visibility==Visibility.Visible,"Restored video lost its controls");
                        output.SetPaused(true);
                    }
                }
                finally { workspace.BackgroundImagePath=oldBackground;File.Delete(imagePath); }
            });
        }
        finally { Call(window, "CloseOutput"); volume.Value = oldVolume; }
    }
}
