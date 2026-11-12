using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

namespace Culto.Desktop;

public partial class MainWindow
{
    private readonly DispatcherTimer _youTubeTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private bool _refreshingYouTube;

    private void InitializeYouTube()
    {
        _youTubeTimer.Tick += YouTubeTimerTick;
        ContentTabs.SelectionChanged += YouTubeTabChanged;
        RefreshYouTube();
        if (YouTubeTab.IsSelected) _youTubeTimer.Start();
    }

    private void DisposeYouTube()
    {
        _youTubeTimer.Stop();
        _youTubeTimer.Tick -= YouTubeTimerTick;
        ContentTabs.SelectionChanged -= YouTubeTabChanged;
    }

    private void YouTubeTimerTick(object? sender, EventArgs e) => RefreshYouTube();

    private void YouTubeTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || !ReferenceEquals(e.Source, ContentTabs)) return;
        if (YouTubeTab.IsSelected) { RefreshYouTube(); _youTubeTimer.Start(); }
        else _youTubeTimer.Stop();
    }

    private void OpenYouTubeClick(object sender, RoutedEventArgs e)
    {
        ActivateWorkspacePanel("sources");
        ContentTabs.SelectedItem = YouTubeTab;
        YouTubeList.Focus();
    }

    private void RefreshYouTubeClick(object sender, RoutedEventArgs e) => RefreshYouTube();

    private void RefreshYouTube(string? selectedKey = null)
    {
        if (!_ready) return;
        selectedKey ??= (YouTubeList.SelectedItem as YouTubeSource)?.Key;
        try
        {
            var sources = YouTubeDiscovery.Build(_workspace.YouTubeSources, WindowCaptureCatalog.List());
            _refreshingYouTube = true;
            try
            {
                YouTubeList.ItemsSource = sources;
                YouTubeList.SelectedItem = sources.FirstOrDefault(source => source.Key == selectedKey);
            }
            finally { _refreshingYouTube = false; }
            var available = sources.Count(source => source.IsAvailable);
            YouTubeCountText.Text = $"{available} disponibles · {_workspace.YouTubeSources.Count} guardadas";
            YouTubeEmpty.Visibility = sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateYouTubeActions();
        }
        catch (Exception error) { YouTubeCountText.Text = "No se pudieron actualizar las ventanas: " + error.Message; }
    }

    private void UpdateYouTubeActions()
    {
        var source = YouTubeList.SelectedItem as YouTubeSource;
        ProjectYouTubeButton.IsEnabled = source?.IsAvailable == true;
        AddYouTubeButton.IsEnabled = source?.IsAvailable == true;
        EditYouTubeButton.IsEnabled = source is not null;
    }

    private void YouTubeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _refreshingYouTube) return;
        UpdateYouTubeActions();
        if (YouTubeList.SelectedItem is not YouTubeSource source) return;
        if (!source.IsAvailable) { SetStatus(source.Detail); return; }
        PrepareCue(CopyCue(source.Cue));
    }

    private void YouTubeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || e.ChangedButton != MouseButton.Left) return;
        var element = e.OriginalSource as DependencyObject;
        while (element is FrameworkContentElement content) element = content.Parent;
        if (element is null || ItemsControl.ContainerFromElement(YouTubeList, element) is not ListBoxItem { DataContext: YouTubeSource }) return;
        e.Handled = true;
        ProjectYouTubeClick(sender, e);
    }

    private void ProjectYouTubeClick(object sender, RoutedEventArgs e)
    {
        if (YouTubeList.SelectedItem is not YouTubeSource { IsAvailable: true } source)
        { SetStatus("Selecciona una ventana disponible de YouTube."); return; }
        PrepareCue(CopyCue(source.Cue));
        ProjectPrepared();
    }

    private void AddYouTubeToPlanClick(object sender, RoutedEventArgs e)
    {
        if (YouTubeList.SelectedItem is not YouTubeSource { IsAvailable: true } source)
        { SetStatus("Selecciona una ventana disponible de YouTube."); return; }
        AddCaptureToPlan(source.Cue);
    }

    private void EditYouTubeClick(object sender, RoutedEventArgs e)
    {
        if (YouTubeList.SelectedItem is not YouTubeSource source) return;
        var dialog = new WindowCaptureDialog(CopyCue(source.Cue)) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedCue is not { } edited) return;
        var index = _workspace.YouTubeSources.FindIndex(cue => cue.Id == edited.Id);
        var previous = index >= 0 ? _workspace.YouTubeSources[index] : null;
        var saved = CopyCue(edited);
        if (index >= 0) _workspace.YouTubeSources[index] = saved;
        else _workspace.YouTubeSources.Add(saved);
        if (!SaveWorkspace())
        {
            if (index >= 0) _workspace.YouTubeSources[index] = previous!;
            else _workspace.YouTubeSources.Remove(saved);
            return;
        }
        RefreshYouTube("saved:" + saved.Id);
        PrepareCue(edited);
        SetStatus("Captura guardada en YouTube.");
    }

    private void AddCaptureToPlan(Cue cue)
    {
        var saved = _workspace.YouTubeSources.FirstOrDefault(source => source.Id == cue.Id);
        var added = saved is null;
        if (added) { saved = CopyCue(cue); _workspace.YouTubeSources.Add(saved); }
        var item = CopyCue(cue, true);
        _plan.Add(item);
        UpdatePlanCount();
        if (!SaveWorkspace())
        {
            if (added) _workspace.YouTubeSources.Remove(saved!);
            _plan.Remove(item);
            _workspace.Plan.Items = _plan.ToList();
            UpdatePlanCount();
            return;
        }
        RefreshYouTube("saved:" + cue.Id);
        PlanList.SelectedItem = item;
        PlanList.ScrollIntoView(item);
        PrepareCue(item);
        SetStatus("Captura añadida al orden y guardada en YouTube.");
    }
}
