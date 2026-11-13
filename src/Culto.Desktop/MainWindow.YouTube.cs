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
    private readonly DispatcherTimer _youTubeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _refreshingYouTube;

    private void InitializeYouTube()
    {
        _youTubeTimer.Tick += YouTubeTimerTick;
        YouTubePanel.IsVisibleChanged += YouTubePanelVisibilityChanged;
        ScenesPanel.IsVisibleChanged += YouTubePanelVisibilityChanged;
        WorkspaceDock.ActiveContentChanged += YouTubeActiveContentChanged;
        RefreshYouTube();
        if (YouTubePanel.IsVisible || ScenesPanel.IsVisible) _youTubeTimer.Start();
    }

    private void DisposeYouTube()
    {
        _youTubeTimer.Stop();
        _youTubeTimer.Tick -= YouTubeTimerTick;
        YouTubePanel.IsVisibleChanged -= YouTubePanelVisibilityChanged;
        ScenesPanel.IsVisibleChanged -= YouTubePanelVisibilityChanged;
        WorkspaceDock.ActiveContentChanged -= YouTubeActiveContentChanged;
    }

    private void YouTubeTimerTick(object? sender, EventArgs e)
    {
        if (YouTubePanel.IsVisible || ScenesPanel.IsVisible) RefreshYouTube();
        else _youTubeTimer.Stop();
    }

    private void YouTubePanelVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_ready) return;
        if (YouTubePanel.IsVisible || ScenesPanel.IsVisible) { RefreshYouTube(); _youTubeTimer.Start(); }
        else _youTubeTimer.Stop();
    }

    private void YouTubeActiveContentChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(WorkspaceDock.ActiveContent, YouTubePanel) || ReferenceEquals(WorkspaceDock.ActiveContent, ScenesPanel)) RefreshYouTube();
    }

    private void OpenYouTubeClick(object sender, RoutedEventArgs e) => SelectContentSource(3);

    private void RefreshYouTubeClick(object sender, RoutedEventArgs e) => RefreshYouTube();

    private void RefreshYouTube(string? selectedKey = null)
    {
        if (!_ready) return;
        selectedKey ??= (YouTubeList.SelectedItem as YouTubeSource)?.Key;
        try
        {
            var sources = YouTubeDiscovery.Build(_workspace.YouTubeSources, WindowCaptureCatalog.List(), _workspace.YouTubeLayout,
                WindowCaptureCatalog.WindowStillExists);
            _refreshingYouTube = true;
            try
            {
                YouTubeList.ItemsSource = sources;
                YouTubeList.SelectedItem = sources.FirstOrDefault(source => source.Key == selectedKey) ?? sources.FirstOrDefault(source=>source.IsAvailable);
            }
            finally { _refreshingYouTube = false; }
            var available = sources.Count(source => source.IsAvailable);
            YouTubeCountText.Text = $"{available} ventanas de YouTube";
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
        RefreshSceneYouTube();
    }

    private YouTubeSource? SceneYouTubeSource() => YouTubeList.SelectedItem is YouTubeSource { IsAvailable: true } selected
        ? selected : YouTubeList.Items.OfType<YouTubeSource>().FirstOrDefault(source => source.IsAvailable);

    private void RefreshSceneYouTube()
    {
        var source = SceneYouTubeSource();
        SceneYouTubeButton.DataContext = source;
        SceneYouTubeButton.ToolTip = source is null ? "Abre YouTube en Chrome" : source.Title + "\n" + source.Detail;
    }

    private void SceneYouTubeClick(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        RefreshYouTube();
        if (SceneYouTubeSource() is { } source) ProjectSceneYouTube(source);
        else { SelectContentSource(3); SetStatus("Abre YouTube en una ventana de Chrome."); }
    }

    private void SceneYouTubeMenuOpened(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not ContextMenu menu) return;
        RefreshYouTube();
        menu.Items.Clear();
        foreach (var source in YouTubeList.Items.OfType<YouTubeSource>().Where(source => source.IsAvailable))
        {
            var item = new MenuItem { Header = source.Title, ToolTip = source.Detail,
                IsCheckable = true, IsChecked = source.Key == SceneYouTubeSource()?.Key };
            item.Click += (_, _) => ProjectSceneYouTube(source);
            menu.Items.Add(item);
        }
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        var choose = new MenuItem { Header = "Actualizar YouTube" };
        choose.Click += ChooseSceneYouTubeClick;
        menu.Items.Add(choose);
    }

    private void ProjectSceneYouTube(YouTubeSource source)
    {
        // Discovery replaces the rows while a context menu can remain open.
        var current = YouTubeList.Items.OfType<YouTubeSource>().FirstOrDefault(item => item.Key == source.Key);
        if (current is not { IsAvailable: true }) { SetStatus("Esta ventana ya no está disponible.", true); return; }
        source = current;
        if (!CheckMedia(source.Cue)) return;
        _refreshingYouTube = true;
        try { YouTubeList.SelectedItem = source; }
        finally { _refreshingYouTube = false; }
        UpdateYouTubeActions();
        PrepareCue(CopyCue(source.Cue));
        ProjectPrepared();
    }

    private void ChooseSceneYouTubeClick(object sender, RoutedEventArgs e)
    {
        SelectContentSource(3);
        RefreshYouTube();
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
        var dialog = new WindowCaptureDialog(CopyCue(source.Cue), _workspace.YouTubeLayout) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedCue is not { } edited) return;
        if(edited.Capture is not {} capture || !YouTubeDiscovery.IsYouTube(capture))
        {SetStatus("Selecciona YouTube en Chrome.",true);return;}
        var previousLayout=_workspace.YouTubeLayout;
        RememberYouTubeLayout(edited);
        if(!SaveWorkspace()){_workspace.YouTubeLayout=previousLayout;return;}
        RefreshYouTube(source.Key);
        PrepareCue(WithYouTubeLayout(edited)!);
        SetStatus("Recorte de YouTube guardado.");
    }

    private void AddCaptureToPlan(Cue cue)
    {
        var previousLayout=_workspace.YouTubeLayout;
        RememberYouTubeLayout(cue);
        var item=CopyCue(WithYouTubeLayout(cue)!,true);
        _plan.Add(item);UpdatePlanCount();
        if(!SaveWorkspace())
        {
            _plan.Remove(item);_workspace.Plan.Items=_plan.ToList();
            _workspace.YouTubeLayout=previousLayout;UpdatePlanCount();return;
        }
        RefreshYouTube();PlanList.SelectedItem=item;PlanList.ScrollIntoView(item);PrepareCue(item);
        SetStatus("YouTube agregado al orden.");
    }

    private void RememberYouTubeLayout(Cue cue)
    {
        if (cue.Kind == CueKind.WindowCapture && cue.Capture is { } capture && YouTubeDiscovery.IsYouTube(capture))
            _workspace.YouTubeLayout = WindowCaptureLayout.From(capture.Normalize());
    }

    private Cue? WithYouTubeLayout(Cue? cue) => cue is null ? null : YouTubeDiscovery.ApplyLayout(cue, _workspace.YouTubeLayout);
}
