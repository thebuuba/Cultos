using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using Culto.Core;

namespace Culto.Desktop.Dialogs;

public sealed partial class TransitionDialog : Window
{
    private readonly TransitionOptions _draft;
    public TransitionOptions Options => _draft.Normalize();
    public TransitionDialog(TransitionOptions options, IEnumerable<TransitionPreset>? presets = null, string? activePresetId = null)
    {
        _draft = options.Normalize();
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "Transiciones"; Width = 760; Height = 840;
        MinWidth = 430; MinHeight = 450; MaxHeight = SystemParameters.WorkArea.Height - 32;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var fields = new StackPanel();
        root.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        fields.Children.Add(new TextBlock { Text = Title, FontSize = 25, Margin = new Thickness(0, 0, 0, 22) });
        BuildPresetControls(fields, presets, activePresetId);
        var type = new ComboBox { Name = "TransitionKind", SelectedValuePath = "Tag", Margin = new Thickness(0, 6, 0, 20) };
        foreach (var (value, title) in new[] { ("Cut", "Corte"), ("Dissolve", "Disolución"), ("Slide", "Desplazamiento"), ("Zoom", "Zoom"), ("Move", "Move · OBS") })
            type.Items.Add(new ComboBoxItem { Tag = value, Content = title });
        type.SelectedValue = _draft.Kind;
        type.SelectionChanged += (_, _) => _draft.Kind = (string)type.SelectedValue;
        fields.Children.Add(new TextBlock { Text = "Efecto visual" }); fields.Children.Add(type);
        var visualDuration = AddSlider(fields, "Duración visual", "TransitionDuration", 100, 3000, _draft.DurationMilliseconds, v => _draft.DurationMilliseconds = v);
        var audioOut = AddSlider(fields, "Fundido de audio al salir", "AudioFadeOut", 0, 5000, _draft.AudioFadeOutMilliseconds, v => _draft.AudioFadeOutMilliseconds = v);
        var audioIn = AddSlider(fields, "Fundido de audio al entrar", "AudioFadeIn", 0, 5000, _draft.AudioFadeInMilliseconds, v => _draft.AudioFadeInMilliseconds = v);
        var moveFields = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        _moveIn = new MoveSettingsEditor("Contenido que entra", "MoveIn", _draft.Move.In);
        _moveOut = new MoveSettingsEditor("Contenido que sale", "MoveOut", _draft.Move.Out);
        _moveIn.Changed += UpdateMoveDraft; _moveOut.Changed += UpdateMoveDraft;
        moveFields.Children.Add(_moveIn); moveFields.Children.Add(_moveOut);
        moveFields.IsEnabled = _draft.Kind == "Move";
        type.SelectionChanged += (_, _) => moveFields.IsEnabled = _draft.Kind == "Move";
        fields.Children.Add(new TextBlock { Text = "Move · entrada y salida", FontSize = 18, FontWeight = FontWeights.SemiBold });
        fields.Children.Add(new TextBlock { Text = "Escala 0%: aparece desde un punto. 100%: conserva el tamaño. Las opciones Move se aplican al elegir ese efecto.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 4) });
        fields.Children.Add(moveFields);
        fields.Children.Add(new TextBlock { Text = "Move mantiene las fuentes en vivo. Las capas con la misma fuente se desplazan entre sus posiciones; crea o edita sus capas desde Escenas.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8) });
        BuildTransitionPreview(fields);
        _loadOptions = value =>
        {
            type.SelectedValue = value.Kind; visualDuration.Value = value.DurationMilliseconds;
            audioOut.Value = value.AudioFadeOutMilliseconds; audioIn.Value = value.AudioFadeInMilliseconds;
            _draft.Move = value.Move.Normalize(); _moveIn.Load(_draft.Move.In); _moveOut.Load(_draft.Move.Out);
        };
        _presetPicker.SelectionChanged += (_, _) => SelectPreset();
        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 18, 0, 0) };
        var restore = new Button { Content = "Restablecer", Name = "RestoreFastTransitions", HorizontalAlignment = HorizontalAlignment.Left };
        restore.Click += (_, _) =>
        {
            var defaults = new TransitionOptions();
            type.SelectedValue = defaults.Kind;
            visualDuration.Value = defaults.DurationMilliseconds;
            audioOut.Value = defaults.AudioFadeOutMilliseconds;
            audioIn.Value = defaults.AudioFadeInMilliseconds;
            _draft.Move = defaults.Move; _moveIn.Load(defaults.Move.In); _moveOut.Load(defaults.Move.Out);
        };
        DockPanel.SetDock(restore, Dock.Left); footer.Children.Add(restore);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Button { Content = "Guardar", IsDefault = true, Name = "SaveTransitions", Style = (Style)FindResource("PrimaryButton") };
        save.Click += (_, _) => { CommitPreset(); DialogResult = true; };
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
