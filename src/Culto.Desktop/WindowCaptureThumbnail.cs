using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Projection;

namespace Culto.Desktop;

// Reuse the projection capture path so the thumbnail includes the saved crop.
public sealed class WindowCaptureThumbnail : ContentControl
{
    public static readonly DependencyProperty CueProperty = DependencyProperty.Register(nameof(Cue), typeof(Cue),
        typeof(WindowCaptureThumbnail), new PropertyMetadata(null, (sender, _) => ((WindowCaptureThumbnail)sender).Refresh()));
    private ProjectionSurface? _surface;
    public Cue? Cue { get => (Cue?)GetValue(CueProperty); set => SetValue(CueProperty, value); }
    internal void RefreshWindowCapture() => _surface?.RefreshWindowCapture();

    public WindowCaptureThumbnail()
    {
        Focusable = false; IsHitTestVisible = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        AutomationProperties.SetName(this, "Vista previa de la captura de YouTube");
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => Stop();
        IsVisibleChanged += (_, _) => Refresh();
    }

    private void Refresh()
    {
        Stop();
        if (!IsLoaded || !IsVisible || Cue is not { Kind: CueKind.WindowCapture, Capture: { } capture } cue) return;
        var preview = cue.Copy();
        preview.Capture = capture with { ControlAudio = false };
        var surface = _surface = new ProjectionSurface(preview: true) { IsCaptureThumbnail = true };
        surface.PlaybackError += ShowError;
        Content = surface;
        try { surface.ShowCue(preview); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException)
        { ShowError(error.Message); }
    }

    private void ShowError(string message)
    {
        Stop();
        Content = new TextBlock { Text = "Vista previa no disponible", ToolTip = message, Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    private void Stop()
    {
        if (_surface is { } surface)
        {
            surface.PlaybackError -= ShowError;
            surface.Dispose();
            _surface = null;
        }
        Content = null;
    }
}
