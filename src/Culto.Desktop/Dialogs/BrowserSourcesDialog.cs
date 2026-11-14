using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Projection;

namespace Culto.Desktop.Dialogs;

public enum BrowserSourceAction { None, Prepare, Project, AddToOrder }

public sealed class BrowserSourcesDialog : Window
{
    private readonly ObservableCollection<Cue> _sources;
    private readonly ListBox _list;
    private readonly BrowserSourceView _preview = new();
    private readonly TextBlock _detail;
    public IReadOnlyList<Cue> Sources => _sources.ToArray();
    public Cue? SelectedCue => _list.SelectedItem as Cue;
    public BrowserSourceAction Action { get; private set; }

    public BrowserSourcesDialog(IEnumerable<Cue> sources, Guid? selected = null)
    {
        _sources = new ObservableCollection<Cue>(sources.Select(Clone));
        var panel = DialogUi.Panel(this, "Escenas de navegador", "Guarda páginas web y archivos HTML con sus títulos, animaciones o relojes.");
        Width = 760;
        _list = new ListBox { ItemsSource = _sources, DisplayMemberPath = "Title", Height = 140 };
        System.Windows.Automation.AutomationProperties.SetName(_list, "Fuentes de navegador guardadas");
        panel.Children.Add(_list);
        var manage = new WrapPanel(); panel.Children.Add(manage);
        BrowserSourceDialog.AddAction(manage, "+ Añadir navegador", () => Edit(null));
        var edit = BrowserSourceDialog.AddAction(manage, "Propiedades", () => { if (SelectedCue is { } cue) Edit(cue); });
        var remove = BrowserSourceDialog.AddAction(manage, "Eliminar", () =>
        {
            if (SelectedCue is { } cue) _sources.Remove(cue);
            _list.SelectedItem = _sources.FirstOrDefault();
        });
        panel.Children.Add(new Border { Height = 220, Background = Brushes.Black,
            Margin = new Thickness(0, 12, 0, 0), Child = _preview });
        _detail = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            Style = (Style)FindResource("MutedText") }; panel.Children.Add(_detail);
        _preview.Error += message => _detail.Text = message;
        var actions = DialogUi.Actions(panel);
        var prepare = BrowserSourceDialog.AddAction(actions, "Preparar", () => Finish(BrowserSourceAction.Prepare));
        var add = BrowserSourceDialog.AddAction(actions, "+ Al orden", () => Finish(BrowserSourceAction.AddToOrder));
        var project = BrowserSourceDialog.AddAction(actions, "Proyectar", () => Finish(BrowserSourceAction.Project), true);
        var close = BrowserSourceDialog.AddAction(actions, "Cerrar", () => DialogResult = true); close.IsCancel = true;
        _list.SelectionChanged += (_, _) =>
        {
            var enabled = SelectedCue is not null;
            edit.IsEnabled = remove.IsEnabled = prepare.IsEnabled = add.IsEnabled = project.IsEnabled = enabled;
            ShowPreview();
        };
        _list.MouseDoubleClick += (_, e) => { if (SelectedCue is { } cue) { Edit(cue); e.Handled = true; } };
        _list.SelectedItem = _sources.FirstOrDefault(c => c.Id == selected) ?? _sources.FirstOrDefault();
        if (_sources.Count == 0)
        {
            edit.IsEnabled = remove.IsEnabled = prepare.IsEnabled = add.IsEnabled = project.IsEnabled = false;
            _detail.Text = "Pulsa + Añadir navegador para elegir un archivo HTML o una URL.";
        }
        Closed += (_, _) => _preview.Dispose();
    }

    private void Edit(Cue? cue)
    {
        var editor = new BrowserSourceDialog(cue) { Owner = this };
        if (editor.ShowDialog() != true || editor.SelectedCue is not { } edited) return;
        if (cue is null) _sources.Add(edited);
        else _sources[_sources.IndexOf(cue)] = edited;
        _list.SelectedItem = edited;
        ShowPreview();
    }
    private void ShowPreview()
    {
        if (SelectedCue?.Browser is not { } options) { _preview.Visibility = Visibility.Hidden; _preview.SetMuted(true); return; }
        _detail.Text = options.Location + $" · {options.Width} × {options.Height} · {options.FramesPerSecond} FPS";
        _preview.Visibility = Visibility.Visible;
        try { _preview.SetSource(options, true); }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException)
        { _detail.Text = error.Message; }
    }
    private void Finish(BrowserSourceAction action)
    {
        if (SelectedCue is null) return;
        Action = action; DialogResult = true;
    }
    private static Cue Clone(Cue cue) => new() { Id = cue.Id, Title = cue.Title, Kind = CueKind.BrowserSource,
        Browser = cue.Browser is { } options ? options with { } : null, Notes = cue.Notes };
}
