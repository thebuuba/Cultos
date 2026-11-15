using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using static Culto.Desktop.Dialogs.TransitionEditorUi;

namespace Culto.Desktop.Dialogs;

public sealed partial class TransitionDialog : Window
{
    private readonly TransitionOptions _draft;
    private readonly TransitionTimeline _timeline = new() { Height = 140, Name = "TransitionTimeline" };
    private readonly TextBlock _total = Text("", 12, Muted), _dirtyLabel = Text("", 12, Muted);
    private readonly List<(string Kind, Button Button)> _effects = [];
    private Action _refreshEditor = () => { };
    private string _savedSignature = "";
    public TransitionOptions Options => _draft.Normalize();

    public TransitionDialog(TransitionOptions options, IEnumerable<TransitionPreset>? presets = null, string? activePresetId = null)
    {
        _draft = options.Normalize(); _currentOptions = _draft.Normalize();
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/CultoStudio;component/Dialogs/TransitionEditorTheme.xaml", UriKind.Relative) });
        Resources["AccentBrush"] = Lime; Resources["ActionBrush"] = Lime;
        Resources["RaisedBrush"] = Brush("#303030"); Resources["InputBrush"] = Brush("#303030");
        Resources["PanelBrush"] = Brush("#242424"); Resources["PanelBorderBrush"] = Brush("#353535");
        Resources["TextBrush"] = Brushes.WhiteSmoke; Resources["MutedBrush"] = Muted;
        Title = "Transiciones"; Width = Math.Min(1380, SystemParameters.WorkArea.Width - 36);
        Height = Math.Min(940, SystemParameters.WorkArea.Height - 36);
        MinWidth = 1000; MinHeight = 620; MaxHeight = SystemParameters.WorkArea.Height - 24;
        Background = Brush("#191919"); FontFamily = new FontFamily("Segoe UI");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(218) }); body.ColumnDefinitions.Add(new ColumnDefinition());
        root.Children.Add(body);
        var sidebar = new StackPanel { Margin = new Thickness(16, 22, 16, 16) };
        var sideHost = new Border { Background = Brush("#202020"), BorderBrush = Brush("#353535"), BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        body.Children.Add(sideHost); BuildPresetControls(sidebar, presets, activePresetId);
        var fields = new StackPanel { Margin = new Thickness(26, 22, 26, 22) };
        var scroll = new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(scroll, 1); body.Children.Add(scroll);
        var header = new Grid { Margin = new Thickness(0, 0, 0, 22) };
        header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _presetName.FontFamily = FontFamily; _presetName.FontSize = 27; _presetName.FontWeight = FontWeights.SemiBold; _presetName.Background = Brushes.Transparent;
        _presetName.BorderThickness = new Thickness(0); _presetName.Padding = new Thickness(0, 5, 0, 5);
        header.Children.Add(_presetName);
        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var duplicate = Button("Duplicar", "DuplicateTransitionPreset", () => { CommitPreset(); AddPreset(); });
        var remove = Button("Eliminar", "RemoveTransitionPreset", RemovePreset); remove.Margin = new Thickness(8, 0, 0, 0);
        headerActions.Children.Add(duplicate); headerActions.Children.Add(remove); Grid.SetColumn(headerActions, 1); header.Children.Add(headerActions);
        fields.Children.Add(header);
        fields.Children.Add(Text("Efecto visual", 16, bold: true));
        var type = new ComboBox { Name = "TransitionKind", SelectedValuePath = "Tag", Visibility = Visibility.Collapsed };
        var effectRow = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Margin = new Thickness(0, 10, 0, 22) };
        foreach (var (value, title) in new[] { ("Cut", "Corte"), ("Dissolve", "Disolución"), ("Slide", "Deslizar"), ("Zoom", "Zoom"), ("Move", "Move") })
        {
            type.Items.Add(new ComboBoxItem { Tag = value, Content = title });
            var effect = Button(title, "Effect" + value, () => type.SelectedValue = value); effect.Height = 44; effect.Margin = new Thickness(0, 0, 8, 0);
            effectRow.Children.Add(effect); _effects.Add((value, effect));
        }
        type.SelectedValue = _draft.Kind; fields.Children.Add(type); fields.Children.Add(effectRow);
        var previewAndTimes = new Grid { Margin = new Thickness(0, 0, 0, 24) };
        previewAndTimes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        previewAndTimes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        previewAndTimes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 300 });
        var previewFields = new StackPanel(); previewFields.Children.Add(Text("Vista previa", 17, bold: true));
        var caption = Text("Imágenes de muestra", 12, Muted); caption.Margin = new Thickness(0, 3, 0, 12); previewFields.Children.Add(caption);
        BuildTransitionPreview(previewFields);
        previewFields.Children.Add(new Border { Background = Brush("#353535"), Height = 1, Margin = new Thickness(0, 18, 0, 12) });
        previewFields.Children.Add(_timeline); var previewCard = Card(previewFields); previewCard.VerticalAlignment = VerticalAlignment.Top; previewAndTimes.Children.Add(previewCard);
        var times = new StackPanel(); var timesHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        _total.Name = "TransitionTotal"; DockPanel.SetDock(_total, Dock.Right); timesHeader.Children.Add(_total); timesHeader.Children.Add(Text("Tiempos", 18, bold: true)); times.Children.Add(timesHeader);
        var visualDuration = AddTimingCard(times, "Duración visual", "Cambio de imagen", "TransitionDuration", "\uE714", 100, 3000, _draft.DurationMilliseconds, Lime, v => _draft.DurationMilliseconds = v);
        var audioOut = AddTimingCard(times, "Audio al salir", "Fundido del contenido anterior", "AudioFadeOut", "\uE767", 0, 5000, _draft.AudioFadeOutMilliseconds, Violet, v => _draft.AudioFadeOutMilliseconds = v);
        var audioIn = AddTimingCard(times, "Audio al entrar", "Fundido del contenido nuevo", "AudioFadeIn", "\uE9D9", 0, 5000, _draft.AudioFadeInMilliseconds, Cyan, v => _draft.AudioFadeInMilliseconds = v);
        Grid.SetColumn(times, 2); previewAndTimes.Children.Add(times); fields.Children.Add(previewAndTimes);
        BuildAudioEffectControls(fields);
        var moveSection = new StackPanel();
        var moveTitle = new StackPanel { Orientation = Orientation.Horizontal }; moveTitle.Children.Add(Text("Move · entrada y salida", 18, bold: true));
        var badge = Card(Text("AVANZADO", 10, Brushes.White, true), 5); badge.Background = Violet; badge.CornerRadius = new CornerRadius(9); badge.Margin = new Thickness(10, 0, 0, 0); moveTitle.Children.Add(badge);
        moveSection.Children.Add(moveTitle); var help = Text("Define desde dónde entra el contenido nuevo y hacia dónde sale el anterior.", 13, Muted); help.Margin = new Thickness(0, 5, 0, 16); moveSection.Children.Add(help);
        var moveFields = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        _moveIn = new MoveSettingsEditor("Contenido que entra", "MoveIn", _draft.Move.In);
        _moveOut = new MoveSettingsEditor("Contenido que sale", "MoveOut", _draft.Move.Out);
        _moveIn.Changed += () => { UpdateMoveDraft(); RefreshVisuals(); }; _moveOut.Changed += () => { UpdateMoveDraft(); RefreshVisuals(); };
        moveFields.Children.Add(_moveIn); moveFields.Children.Add(_moveOut); moveSection.Children.Add(moveFields);
        var moveNotice = Text("Move conserva las fuentes en vivo. Las capas compartidas se desplazan entre sus posiciones; edítalas desde Escenas.", 12, Muted);
        moveNotice.Margin = new Thickness(0, 16, 0, 0); moveSection.Children.Add(moveNotice); fields.Children.Add(Card(moveSection));
        _refreshEditor = () =>
        {
            var cut = _draft.Kind == "Cut"; visualDuration.IsEnabled = !cut; moveFields.IsEnabled = _draft.Kind == "Move";
            foreach (var (kind, button) in _effects)
            {
                button.Background = kind == _draft.Kind ? Lime : Brush("#303030"); button.Foreground = kind == _draft.Kind ? Brushes.Black : Brushes.WhiteSmoke;
                System.Windows.Automation.AutomationProperties.SetItemStatus(button, kind == _draft.Kind ? "Seleccionado" : "");
            }
            var total = Math.Max(cut ? 0 : _draft.DurationMilliseconds, Math.Max(_draft.AudioFadeOutMilliseconds, _draft.AudioFadeInMilliseconds));
            _total.Text = "Total " + (total / 1000d).ToString("0.00", CultureInfo.InvariantCulture) + " s";
            _timeline.Update(Options);
        };
        type.SelectionChanged += (_, _) => { if (type.SelectedValue is string kind) { _draft.Kind = kind; RefreshVisuals(); } };
        _loadOptions = value =>
        {
            type.SelectedValue = value.Kind; visualDuration.Value = value.DurationMilliseconds;
            audioOut.Value = value.AudioFadeOutMilliseconds; audioIn.Value = value.AudioFadeInMilliseconds;
            _draft.Move = value.Move.Normalize(); _moveIn.Load(_draft.Move.In); _moveOut.Load(_draft.Move.Out); RefreshVisuals();
            _draft.AudioFadeEffect = value.AudioFadeEffect; _draft.CathedralIntensityPercent = value.CathedralIntensityPercent;
            _draft.CathedralTailMilliseconds = value.CathedralTailMilliseconds; LoadAudioEffectControls(value); RefreshVisuals();
        };
        _presetPicker.SelectionChanged += (_, _) => SelectPreset();
        _presetName.TextChanged += (_, _) => RefreshDirtyState();
        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(22, 14, 22, 14) };
        var restore = Button("Restablecer", "RestoreFastTransitions", () => _loadOptions(new TransitionOptions()));
        DockPanel.SetDock(restore, Dock.Left); footer.Children.Add(restore);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _dirtyLabel.Margin = new Thickness(16, 0, 16, 0); actions.Children.Add(_dirtyLabel);
        var cancel = Button("Cancelar", "CancelTransitions", () => DialogResult = false); cancel.IsCancel = true; cancel.Margin = new Thickness(0, 0, 8, 0);
        var save = Button("Guardar", "SaveTransitions", () => { CommitPreset(); DialogResult = true; }, true); save.IsDefault = true;
        actions.Children.Add(cancel); actions.Children.Add(save); DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions);
        var footerFrame = new Border { Background = Brush("#242424"), BorderBrush = Brush("#353535"), BorderThickness = new Thickness(0, 1, 0, 0), Child = footer };
        Grid.SetRow(footerFrame, 1); root.Children.Add(footerFrame); Content = root; _refreshEditor();
        _savedSignature = DraftSignature(); RefreshDirtyState();
    }
    private string DraftSignature() => System.Text.Json.JsonSerializer.Serialize(new
    {
        Options, ActivePresetId,
        Presets = _presets.Select(p => p.Id == _selectedPresetId ? p with
        { Options = Options, Name = string.IsNullOrWhiteSpace(_presetName.Text) ? p.Name : _presetName.Text.Trim() } : p)
    });
    private void RefreshDirtyState() => _dirtyLabel.Text = DraftSignature() == _savedSignature ? "Todo guardado" : "Cambios sin guardar";
    private void RefreshVisuals() { _refreshEditor(); RefreshDirtyState(); }

    private Slider AddTimingCard(Panel parent, string title, string caption, string name, string glyph, int min, int max, int current, Brush color, Action<int> set)
    {
        var panel = new StackPanel(); var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Text(glyph, 15); icon.FontFamily = new FontFamily("Segoe MDL2 Assets"); icon.HorizontalAlignment = HorizontalAlignment.Center;
        heading.Children.Add(new Border { Background = Brush("#303030"), CornerRadius = new CornerRadius(18), Width = 36, Height = 36, Child = icon, Margin = new Thickness(0, 0, 10, 0) });
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; labels.Children.Add(Text(title, 14, bold: true)); labels.Children.Add(Text(caption, 12, Muted)); heading.Children.Add(labels); top.Children.Add(heading);
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, Value = current, TickFrequency = 50, SmallChange = 50, LargeChange = 250, IsSnapToTickEnabled = true, Foreground = color, Margin = new Thickness(0, 8, 0, 0) };
        slider.SetResourceReference(StyleProperty, "EditorSlider"); System.Windows.Automation.AutomationProperties.SetName(slider, title);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        var minus = Button("−", name + "Decrease", () => slider.Value = Math.Max(min, slider.Value - 100)); minus.Width = 32; minus.Height = 32; minus.MinHeight = 32; minus.Padding = new Thickness(0); minus.FontSize = 18;
        var plus = Button("+", name + "Increase", () => slider.Value = Math.Min(max, slider.Value + 100)); plus.Width = 32; plus.Height = 32; plus.MinHeight = 32; plus.Padding = new Thickness(0); plus.FontSize = 18; plus.Background = Brushes.WhiteSmoke; plus.Foreground = Brushes.Black; plus.Margin = new Thickness(4, 0, 0, 0);
        System.Windows.Automation.AutomationProperties.SetName(minus, "Reducir " + title); System.Windows.Automation.AutomationProperties.SetName(plus, "Aumentar " + title);
        buttons.Children.Add(minus); buttons.Children.Add(plus); Grid.SetColumn(buttons, 1); top.Children.Add(buttons); panel.Children.Add(top);
        var number = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        number.Children.Add(new Border { Background = color, CornerRadius = new CornerRadius(4), Width = 8, Height = 8, Margin = new Thickness(0, 0, 5, 7), VerticalAlignment = VerticalAlignment.Bottom });
        var value = Text((current / 1000d).ToString("0.00", CultureInfo.InvariantCulture), 34, bold: true); number.Children.Add(value);
        var unit = Text("seg", 13, Muted); unit.Margin = new Thickness(5, 0, 0, 7); unit.VerticalAlignment = VerticalAlignment.Bottom; number.Children.Add(unit); panel.Children.Add(number); panel.Children.Add(slider);
        var card = Card(panel, 16); card.Margin = new Thickness(0, 0, 0, 12); parent.Children.Add(card);
        slider.ValueChanged += (_, _) => { set((int)slider.Value); value.Text = (slider.Value / 1000).ToString("0.00", CultureInfo.InvariantCulture); minus.IsEnabled = slider.Value > min; plus.IsEnabled = slider.Value < max; RefreshVisuals(); };
        slider.IsEnabledChanged += (_, _) => { minus.IsEnabled = slider.IsEnabled && slider.Value > min; plus.IsEnabled = slider.IsEnabled && slider.Value < max; };
        minus.IsEnabled = current > min; plus.IsEnabled = current < max; return slider;
    }
}
