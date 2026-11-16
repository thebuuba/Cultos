using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Culto.Desktop.Projection;

namespace Culto.Desktop.Dialogs;

public partial class PowerPointModeratorWindow : Window
{
    public event Action<int>? NavigateRequested;
    public event Action? PauseRequested, RestartRequested, BlackoutRequested;
    public OutputMonitor Monitor => ModeratorMonitor;

    public PowerPointModeratorWindow()
    {
        InitializeComponent();
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        Closed += (_, _) => Monitor.Dispose();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.F8 && ModeratorBlackoutButton.IsEnabled) BlackoutRequested?.Invoke();
            else if (e.Key is Key.Right or Key.PageDown && ModeratorNextButton.IsEnabled) NavigateRequested?.Invoke(1);
            else if (e.Key is Key.Left or Key.PageUp && ModeratorPreviousButton.IsEnabled) NavigateRequested?.Invoke(-1);
            else return;
            e.Handled = true;
        };
    }

    public void SetSlide(string notes, ImageSource? next)
    {
        ModeratorNotes.Text = string.IsNullOrWhiteSpace(notes) ? "Sin notas para esta diapositiva." : notes;
        ModeratorNotesScroll.ScrollToTop();
        ModeratorNextImage.Source = next;
        ModeratorNextEmpty.Visibility = next is null ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateState(string title, int slide, int count, bool running, bool controllable, bool paused, bool blackout, bool hidden)
    {
        ModeratorTitle.Text = running ? title : "Sin presentación en pantalla";
        ModeratorPosition.Text = running ? $"Diapositiva {slide} de {count}" : "Sin presentación en pantalla";
        ModeratorStatus.Text = !running ? "Sin salida" : blackout ? "Negro" : hidden ? "Logo" : paused ? "En pausa" : "En vivo";
        ModeratorPreviousButton.IsEnabled = ModeratorNextButton.IsEnabled = controllable;
        ModeratorPauseButton.IsEnabled = ModeratorRestartButton.IsEnabled = controllable;
        ModeratorBlackoutButton.IsEnabled = running;
        ModeratorPauseButton.Content = paused ? "Continuar" : "Pausar";
        ModeratorBlackoutButton.Content = blackout ? "Volver a mostrar" : "Pantalla negra";
        if (!running)
        {
            SetSlide("Inicia una presentación para ver sus notas.", null);
            ModeratorNextEmpty.Text = "Sin presentación";
        }
        else ModeratorNextEmpty.Text = "Fin de la presentación";
    }

    private void PreviousClick(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(-1);
    private void NextClick(object sender, RoutedEventArgs e) => NavigateRequested?.Invoke(1);
    private void PauseClick(object sender, RoutedEventArgs e) => PauseRequested?.Invoke();
    private void RestartClick(object sender, RoutedEventArgs e) => RestartRequested?.Invoke();
    private void BlackoutClick(object sender, RoutedEventArgs e) => BlackoutRequested?.Invoke();
    private void SmallerNotesClick(object sender, RoutedEventArgs e) => ModeratorNotes.FontSize = Math.Max(14, ModeratorNotes.FontSize - 2);
    private void LargerNotesClick(object sender, RoutedEventArgs e) => ModeratorNotes.FontSize = Math.Min(36, ModeratorNotes.FontSize + 2);
}
