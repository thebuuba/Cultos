using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop.Projection;
using static Culto.Desktop.Dialogs.TransitionEditorUi;

namespace Culto.Desktop.Dialogs;

public sealed partial class TransitionDialog
{
    private ComboBox _audioFadeEffect = null!;
    private Slider _cathedralIntensity = null!, _cathedralTail = null!;
    private TextBlock _cathedralIntensityValue = null!, _cathedralTailValue = null!, _audioPreviewStatus = null!;
    private Button _previewCathedralAudio = null!, _stopCathedralAudio = null!;
    private CancellationTokenSource? _audioPreviewCancellation;
    private bool _loadingAudioEffects, _audioPreviewClosed;

    private void BuildAudioEffectControls(Panel parent)
    {
        var fields = new StackPanel();
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(new Border { Width = 8, Height = 8, Background = Violet, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 10, 0) });
        title.Children.Add(Text("Efecto del fundido de audio", 17, bold: true)); fields.Children.Add(title);
        var description = Text("La reverberación se aplica solo al audio que sale. El siguiente archivo conserva su audio original, sin efecto.", 12, Muted);
        description.Margin = new Thickness(0, 8, 0, 14); fields.Children.Add(description);
        _audioFadeEffect = new ComboBox { Name = "AudioFadeEffect", SelectedValuePath = "Tag", Margin = new Thickness(0, 0, 0, 4) };
        _audioFadeEffect.Items.Add(new ComboBoxItem { Tag = "Normal", Content = "Fundido normal" });
        _audioFadeEffect.Items.Add(new ComboBoxItem { Tag = "Cathedral", Content = "Fundido con reverberación catedral" });
        AutomationProperties.SetName(_audioFadeEffect, "Efecto del fundido de audio"); fields.Children.Add(_audioFadeEffect);

        _cathedralIntensity = AddCathedralSlider(fields, "Intensidad", "CathedralIntensity", 0, 60, 1, Violet, out _cathedralIntensityValue);
        _cathedralTail = AddCathedralSlider(fields, "Cola de reverberación", "CathedralTail", 500, 8000, 100, Cyan, out _cathedralTailValue);
        var notice = Text("Si activas este efecto mientras un video está reproduciéndose, se aplicará a partir del próximo contenido que inicies.", 12, Muted);
        notice.Margin = new Thickness(0, 12, 0, 14); fields.Children.Add(notice);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        _previewCathedralAudio = Button("Probar audio", "PreviewCathedralAudio", () => { }, true);
        _previewCathedralAudio.Click += async (_, _) => await PreviewCathedralAudioAsync();
        _stopCathedralAudio = Button("Detener", "StopCathedralAudio", () => StopCathedralAudioPreview());
        _stopCathedralAudio.Margin = new Thickness(8, 0, 0, 0); _stopCathedralAudio.IsEnabled = false;
        actions.Children.Add(_previewCathedralAudio); actions.Children.Add(_stopCathedralAudio); fields.Children.Add(actions);
        _audioPreviewStatus = Text("La prueba usa un sonido de muestra independiente.", 12, Muted);
        _audioPreviewStatus.Name = "CathedralAudioPreviewStatus"; _audioPreviewStatus.Margin = new Thickness(0, 10, 0, 0);
        AutomationProperties.SetLiveSetting(_audioPreviewStatus, AutomationLiveSetting.Polite); fields.Children.Add(_audioPreviewStatus);
        var card = Card(fields, 22); card.Margin = new Thickness(0, 0, 0, 24); parent.Children.Add(card);

        _audioFadeEffect.SelectionChanged += (_, _) =>
        {
            UpdateAudioEffectControls();
            if (_loadingAudioEffects) return;
            _draft.AudioFadeEffect = _audioFadeEffect.SelectedValue as string ?? "Normal";
            if (_draft.AudioFadeEffect != "Cathedral") StopCathedralAudioPreview();
            RefreshVisuals();
        };
        _cathedralIntensity.ValueChanged += (_, _) =>
        {
            UpdateAudioEffectValues();
            if (_loadingAudioEffects) return;
            _draft.CathedralIntensityPercent = (int)Math.Round(_cathedralIntensity.Value); RefreshVisuals();
        };
        _cathedralTail.ValueChanged += (_, _) =>
        {
            UpdateAudioEffectValues();
            if (_loadingAudioEffects) return;
            _draft.CathedralTailMilliseconds = (int)Math.Round(_cathedralTail.Value); RefreshVisuals();
        };
        Closed += (_, _) => { _audioPreviewClosed = true; StopCathedralAudioPreview(); };
        LoadAudioEffectControls(_draft);
    }

    private static Slider AddCathedralSlider(Panel parent, string title, string name, int min, int max, int tick, Brush accent, out TextBlock value)
    {
        var heading = new DockPanel { Margin = new Thickness(0, 16, 0, 6) };
        value = Text("", 12, accent, true);
        var pill = new Border { Background = Brush("#333333"), CornerRadius = new CornerRadius(7), Padding = new Thickness(9, 4, 9, 4), Child = value };
        DockPanel.SetDock(pill, Dock.Right); heading.Children.Add(pill); heading.Children.Add(Text(title)); parent.Children.Add(heading);
        var slider = new Slider { Name = name, Minimum = min, Maximum = max, TickFrequency = tick, SmallChange = tick,
            LargeChange = tick * 5, IsSnapToTickEnabled = true, Foreground = accent };
        slider.SetResourceReference(StyleProperty, "EditorSlider"); AutomationProperties.SetName(slider, title); parent.Children.Add(slider); return slider;
    }

    private void LoadAudioEffectControls(TransitionOptions options)
    {
        StopCathedralAudioPreview();
        _loadingAudioEffects = true;
        try
        {
            _audioFadeEffect.SelectedValue = options.AudioFadeEffect == "Cathedral" ? "Cathedral" : "Normal";
            _cathedralIntensity.Value = Math.Clamp(options.CathedralIntensityPercent, 0, 60);
            _cathedralTail.Value = Math.Clamp(options.CathedralTailMilliseconds, 500, 8000);
            UpdateAudioEffectValues(); UpdateAudioEffectControls();
        }
        finally { _loadingAudioEffects = false; }
    }

    private void UpdateAudioEffectValues()
    {
        _cathedralIntensityValue.Text = ((int)Math.Round(_cathedralIntensity.Value)).ToString(CultureInfo.InvariantCulture) + "%";
        _cathedralTailValue.Text = (_cathedralTail.Value / 1000d).ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }

    private void UpdateAudioEffectControls()
    {
        var cathedral = _audioFadeEffect.SelectedValue as string == "Cathedral";
        _cathedralIntensity.IsEnabled = cathedral; _cathedralTail.IsEnabled = cathedral;
        _previewCathedralAudio.IsEnabled = cathedral && _audioPreviewCancellation is null;
    }

    private void StopCathedralAudioPreview()
    {
        if (_audioPreviewCancellation is null) return;
        _audioPreviewCancellation.Cancel();
        if (!_audioPreviewClosed) _audioPreviewStatus.Text = "Deteniendo la prueba de audio…";
    }

    private async Task PreviewCathedralAudioAsync()
    {
        if (_audioPreviewCancellation is not null || _audioPreviewClosed) return;
        var cancellation = new CancellationTokenSource(); _audioPreviewCancellation = cancellation;
        CathedralAudioOutput? output = null;
        _previewCathedralAudio.IsEnabled = false; _stopCathedralAudio.IsEnabled = true;
        _audioPreviewStatus.Text = "Reproduciendo la prueba de reverberación…";
        try
        {
            output = await CathedralAudioOutput.PlayPreviewAsync((int)Math.Round(_cathedralTail.Value), (int)Math.Round(_cathedralIntensity.Value), cancellation.Token);
            while (!output.TailComplete) await Task.Delay(50, cancellation.Token);
            if (output.HasError) throw new InvalidOperationException(output.LastError);
            if (!_audioPreviewClosed) _audioPreviewStatus.Text = "Prueba de audio finalizada.";
        }
        catch (OperationCanceledException) { if (!_audioPreviewClosed) _audioPreviewStatus.Text = "Prueba de audio detenida."; }
        catch (Exception)
        {
            if (!_audioPreviewClosed) _audioPreviewStatus.Text = "No se pudo reproducir la prueba de audio. Comprueba la salida de sonido.";
        }
        finally
        {
            output?.Dispose(); cancellation.Dispose(); _audioPreviewCancellation = null;
            if (!_audioPreviewClosed) { _stopCathedralAudio.IsEnabled = false; UpdateAudioEffectControls(); }
        }
    }
}
