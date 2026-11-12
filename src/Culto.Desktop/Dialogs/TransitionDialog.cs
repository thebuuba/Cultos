using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Culto.Core;

namespace Culto.Desktop.Dialogs;

public sealed class TransitionDialog : Window
{
    private readonly TransitionOptions _draft;
    public TransitionOptions Options => _draft.Normalize();
    public TransitionDialog(TransitionOptions options)
    {
        _draft = options.Normalize();
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Transición del sistema"; Width = 540; Height = 510;
        MinWidth = 430; MinHeight = 450; MaxHeight = SystemParameters.WorkArea.Height - 32;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var fields = new StackPanel();
        root.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        fields.Children.Add(new TextBlock { Text = Title, FontSize = 25, Margin = new Thickness(0, 0, 0, 22) });
        var type = new ComboBox { Name = "TransitionKind", SelectedValuePath = "Tag", Margin = new Thickness(0, 6, 0, 20) };
        foreach (var (value, title) in new[] { ("Cut", "Corte"), ("Dissolve", "Disolución"), ("Slide", "Desplazamiento"), ("Zoom", "Zoom"), ("Move", "Move · OBS") })
            type.Items.Add(new ComboBoxItem { Tag = value, Content = title });
        type.SelectedValue = _draft.Kind;
        type.SelectionChanged += (_, _) => _draft.Kind = (string)type.SelectedValue;
        fields.Children.Add(new TextBlock { Text = "Efecto visual" }); fields.Children.Add(type);
        var visualDuration = AddSlider(fields, "Duración visual", "TransitionDuration", 100, 3000, _draft.DurationMilliseconds, v => _draft.DurationMilliseconds = v);
        var audioOut = AddSlider(fields, "Fundido de audio al salir", "AudioFadeOut", 0, 5000, _draft.AudioFadeOutMilliseconds, v => _draft.AudioFadeOutMilliseconds = v);
        var audioIn = AddSlider(fields, "Fundido de audio al entrar", "AudioFadeIn", 0, 5000, _draft.AudioFadeInMilliseconds, v => _draft.AudioFadeInMilliseconds = v);
        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 18, 0, 0) };
        var restore = new Button { Content = "Restablecer", Name = "RestoreFastTransitions", HorizontalAlignment = HorizontalAlignment.Left };
        restore.Click += (_, _) =>
        {
            var defaults = new TransitionOptions();
            type.SelectedValue = defaults.Kind;
            visualDuration.Value = defaults.DurationMilliseconds;
            audioOut.Value = defaults.AudioFadeOutMilliseconds;
            audioIn.Value = defaults.AudioFadeInMilliseconds;
        };
        DockPanel.SetDock(restore, Dock.Left); footer.Children.Add(restore);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Guardar", IsDefault = true, Name = "SaveTransitions", Style = (Style)FindResource("PrimaryButton") };
        save.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(save);
        DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions);
        Grid.SetRow(footer, 1); root.Children.Add(footer); Content = root;
    }
    private static Slider AddSlider(Panel parent, string title, string name, int min, int max, int current, Action<int> set)
    {
        var label = new TextBlock { Text = $"{title} · {current / 1000d:0.00} s" };
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, Value = current, TickFrequency = 50, SmallChange = 50, LargeChange = 250,
            IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 24) };
        AutomationProperties.SetName(slider, title);
        slider.ValueChanged += (_, _) => { set((int)slider.Value); label.Text = $"{title} · {slider.Value / 1000:0.00} s"; };
        parent.Children.Add(label); parent.Children.Add(slider);
        return slider;
    }
}
