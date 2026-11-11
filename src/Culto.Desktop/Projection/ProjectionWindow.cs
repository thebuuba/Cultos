using System.Windows;
using System.Windows.Media;

namespace Culto.Desktop.Projection;

public sealed class ProjectionWindow : Window
{
    public ProjectionWindow(ProjectionSurface surface, bool pauseOnClose = true)
    {
        Title = "Culto Studio — salida de congregación";
        Content = surface;
        Background = Brushes.Black;
        ShowInTaskbar = false;
        ShowActivated = false;
        Width = 960;
        Height = 600;
        if (pauseOnClose) Closed += (_, _) => surface.SetPaused(true);
    }
}
