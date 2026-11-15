using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop.Dialogs;

namespace Culto.Desktop;

public partial class MainWindow
{
    private Guid? _selectedBrowserSource;

    private void BrowserSourcesClick(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var dialog = new BrowserSourcesDialog(_workspace.BrowserSources, _selectedBrowserSource) { Owner = this };
        dialog.ShowDialog();
        if (!SaveBrowserSources(dialog.Sources)) return;
        if (dialog.SelectedCue is not { } selected) return;
        _selectedBrowserSource = selected.Id;
        RefreshBrowserScene();
        switch (dialog.Action)
        {
            case BrowserSourceAction.Prepare:
                if (CheckMedia(selected)) PrepareCue(CopyCue(selected));
                break;
            case BrowserSourceAction.Project:
                ProjectBrowserSource(selected);
                break;
            case BrowserSourceAction.AddToOrder:
                AddToPlan(selected);
                break;
        }
    }

    private bool SaveBrowserSources(IEnumerable<Cue> sources)
    {
        var previous = _workspace.BrowserSources;
        _workspace.BrowserSources = sources.Select(c => CopyCue(c)).ToList();
        if (!SaveWorkspace()) { _workspace.BrowserSources = previous; return false; }
        RefreshBrowserScene();
        return true;
    }

    private void SceneBrowserClick(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var source = _workspace.BrowserSources.FirstOrDefault(c => c.Id == _selectedBrowserSource)
            ?? _workspace.BrowserSources.FirstOrDefault();
        if (source is null) { BrowserSourcesClick(sender, e); return; }
        ProjectBrowserSource(source);
    }

    private void ProjectBrowserSource(Cue source)
    {
        if (!CheckMedia(source)) return;
        _selectedBrowserSource = source.Id;
        RefreshBrowserScene();
        PrepareCue(CopyCue(source));
        ProjectPrepared();
    }

    private void SceneBrowserMenuOpened(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not ContextMenu menu) return;
        menu.Items.Clear();
        foreach (var source in _workspace.BrowserSources)
        {
            var item = new MenuItem { Header = source.Title, IsCheckable = true, IsChecked = source.Id == _selectedBrowserSource };
            item.Click += (_, _) => WithSceneMenuTransition(menu, () => ProjectBrowserSource(source)); menu.Items.Add(item);
        }
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        var manage = new MenuItem { Header = "Añadir / administrar navegadores…" };
        manage.Click += BrowserSourcesClick; menu.Items.Add(manage);
        var refresh = new MenuItem { Header = "Recargar navegador en pantalla", IsEnabled = _session.Live?.Kind == CueKind.BrowserSource };
        refresh.Click += (_, _) => { if (_session.Live is { } cue) _output?.ReloadBrowserSource(cue.Id); SetStatus("Navegador recargado."); };
        menu.Items.Add(refresh);
    }

    private void RefreshBrowserScene()
    {
        var source = _workspace.BrowserSources.FirstOrDefault(c => c.Id == _selectedBrowserSource)
            ?? _workspace.BrowserSources.FirstOrDefault();
        if (SceneBrowserTitle is not null) SceneBrowserTitle.Text = source?.Title ?? "Navegador";
        if (SceneBrowserButton is not null) SceneBrowserButton.ToolTip = source is null
            ? "Añadir una escena HTML o URL" : source.Title + "\n" + source.Browser?.Location + "\nClic derecho: elegir otra fuente o recargar";
    }
}
