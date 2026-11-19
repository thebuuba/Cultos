using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private Window? _previewViewer;
    private OutputMonitor? _previewViewerMonitor;
    private bool _programFill, _preparedFill;
    private double _programZoom = 1, _preparedZoom = 1;

    private void MonitorViewClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var preview = (string)button.Tag == "preview";
        var live = !preview || PreviewFollowsOutput;
        var menu = new ContextMenu { PlacementTarget = button };
        void Item(string label, Action action, bool selected = false)
        {
            var item = new MenuItem { Header = label, IsCheckable = selected, IsChecked = selected };
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Item("Ampliar " + (preview ? "vista previa" : "programa"), () =>
        { if (preview) OpenPreviewViewer(); else OpenOutputPlayer(); });
        menu.Items.Add(new Separator());
        var fill = live ? _programFill : _preparedFill;
        var zoom = live ? _programZoom : _preparedZoom;
        void Apply(bool nextFill, double nextZoom)
        {
            if (live) { _programFill = nextFill; _programZoom = nextZoom; }
            else { _preparedFill = nextFill; _preparedZoom = nextZoom; }
            ApplyMonitorMediaView();
        }
        Item("Ajustar imagen completa", () => Apply(false, 1), !fill && zoom == 1);
        Item("Llenar pantalla (recortar bordes)", () => Apply(true, 1), fill && zoom == 1);
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
            Item($"Zoom {scale * 100:0}%", () => Apply(fill, scale), zoom == scale);
        button.ContextMenu = menu; menu.IsOpen = true;
    }

    private void ApplyMonitorMediaView()
    {
        if (_output is not null) { _output.MediaFill = _programFill; _output.MediaZoom = _programZoom; }
        if (_preview is not null) { _preview.MediaFill = _preparedFill; _preview.MediaZoom = _preparedZoom; }
    }

    private void UsePreparedMediaView()
    {
        if (PreviewFollowsOutput) return;
        _programFill = _preparedFill; _programZoom = _preparedZoom;
        ApplyMonitorMediaView();
    }

    private Size MonitorOutputSize()
    {
        if (_outputWindow is { ActualWidth: > 0, ActualHeight: > 0 } && _output is { ActualWidth: > 0, ActualHeight: > 0 })
            return new Size(_output.ActualWidth, _output.ActualHeight);
        var displays = DisplayService.GetDisplays();
        var device = DisplayService.GetWindowDevice(this);
        var display = _outputDisplay ?? DisplayService.SelectOutput(displays, _workspace.DisplayDevice, device)
            ?? displays.FirstOrDefault(screen => screen.DeviceName == device)
            ?? displays.FirstOrDefault(screen => screen.IsPrimary);
        return display is { Width: > 0, Height: > 0 } ? new Size(display.Width, display.Height) : new Size(1920, 1080);
    }

    private void UpdateMonitorGeometry()
    {
        if (LivePicture is null || PreviewPicture is null) return;
        var size = MonitorOutputSize();
        LivePicture.AspectRatio = PreviewPicture.AspectRatio = size.Width / size.Height;
        if (_outputWindow is null && _output is not null)
        {
            _output.Width = size.Width; _output.Height = size.Height;
            _output.Measure(size); _output.Arrange(new Rect(size)); _output.UpdateLayout();
        }
        ApplyMonitorMediaView();
        UpdatePreviewViewer();
    }

    private void OpenPreviewViewer()
    {
        if (_previewViewer is not null) { _previewViewer.Activate(); return; }
        var monitor = new OutputMonitor();
        var viewer = new Window
        {
            Title = "Vista previa ampliada", Width = 1100, Height = 700, MinWidth = 360, MinHeight = 240,
            Owner = this, Background = Brushes.Black,
            Content = new Border { Background = Brushes.Black, Child = monitor, ClipToBounds = true }
        };
        _previewViewer = viewer; _previewViewerMonitor = monitor;
        viewer.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) viewer.Close(); };
        viewer.Closed += (_, _) => { monitor.Dispose(); _previewViewer = null; _previewViewerMonitor = null; };
        UpdatePreviewViewer(); viewer.Show();
    }

    private void UpdatePreviewViewer()
    {
        if (_previewViewerMonitor is not { } monitor) return;
        var live = PreviewFollowsOutput && !_holdingBiblePreview;
        monitor.SetLocalSource(live ? _output : _preview);
        monitor.SetNativeSource(live ? PreviewLiveMonitor.NativeSource : IntPtr.Zero);
        monitor.SetMirrorSource(live && !_session.IsBlackout && !_session.IsTextHidden
            && _powerPoint?.IsRunning == true && _outputDisplay is null ? LocalPowerPointMonitor : null);
    }
}
