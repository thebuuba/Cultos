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
        foreach (var (value, title) in new[] { ("Cut", "Corte"), ("Dissolve", "Disolución"), ("Slide", "Desplazamiento"), ("Zoom", "Zoom") })
            type.Items.Add(new ComboBoxItem { Tag = value, Content = title });
        type.SelectedValue = _draft.Kind;
        type.SelectionChanged += (_, _) => _draft.Kind = (string)type.SelectedValue;
        fields.Children.Add(new TextBlock { Text = "Efecto visual" }); fields.Children.Add(type);
        AddSlider(fields, "Duración visual", "TransitionDuration", 100, 3000, _draft.DurationMilliseconds, v => _draft.DurationMilliseconds = v);
        AddSlider(fields, "Fundido de audio al salir", "AudioFadeOut", 0, 5000, _draft.AudioFadeOutMilliseconds, v => _draft.AudioFadeOutMilliseconds = v);
        AddSlider(fields, "Fundido de audio al entrar", "AudioFadeIn", 0, 5000, _draft.AudioFadeInMilliseconds, v => _draft.AudioFadeInMilliseconds = v);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Guardar", IsDefault = true, Name = "SaveTransitions", Style = (Style)FindResource("PrimaryButton") };
        save.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel); actions.Children.Add(save); Grid.SetRow(actions, 1); root.Children.Add(actions); Content = root;
    }
    private static void AddSlider(Panel parent, string title, string name, int min, int max, int current, Action<int> set)
    {
        var label = new TextBlock { Text = $"{title} · {current / 1000d:0.0} s" };
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, Value = current, TickFrequency = 100, SmallChange = 100, LargeChange = 500,
            IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 24) };
        AutomationProperties.SetName(slider, title);
        slider.ValueChanged += (_, _) => { set((int)slider.Value); label.Text = $"{title} · {slider.Value / 1000:0.0} s"; };
        parent.Children.Add(label); parent.Children.Add(slider);
    }
}
