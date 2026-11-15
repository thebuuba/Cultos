using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunTransitionSettingsTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var session = Field<PresentationSession>(window, "_session");
        var original = workspace.Transitions;
        var hadOutput = Field<ProjectionSurface?>(window, "_output") is not null;
        var output = Field<ProjectionSurface?>(window, "_output");
        var oldOutputOptions = output?.Transitions;
        var recoveryBlocked = Field<bool>(window, "_workspaceRecoveryBlocked");
        T Control<T>(TransitionDialog dialog, string name) where T : FrameworkElement =>
            Descendants(dialog).OfType<T>().Single(control => control.Name == name);
        TransitionDialog ExerciseSettings(Action<TransitionDialog> action)
        {
            Exception? error = null;
            TransitionDialog? editor = null;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    editor = Application.Current.Windows.OfType<TransitionDialog>().Single();
                    editor.UpdateLayout(); action(editor);
                }
                catch (Exception ex) { error = ex; }
                finally { if (editor?.IsVisible == true) editor.Close(); }
            }));
            Call(window, "TransitionSettingsClick", window, new RoutedEventArgs());
            if (error is not null) throw error;
            Check(editor is not null, "Transition settings did not open its editor");
            return editor!;
        }
        void Save(TransitionDialog dialog) => Control<Button>(dialog, "SaveTransitions")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            Test("Visual transition editor synchronizes timing cards, timeline and native Move controls", () =>
            {
                workspace.Transitions = new TransitionOptions { Kind = "Move", DurationMilliseconds = 1150,
                    AudioFadeOutMilliseconds = 1550, AudioFadeInMilliseconds = 1300 };
                ExerciseSettings(dialog =>
                {
                    var duration = Control<Slider>(dialog, "TransitionDuration");
                    Check(Descendants(dialog).OfType<TextBlock>().Any(t => t.Text == "Todo guardado"), "New editor starts dirty");
                    Control<Button>(dialog, "TransitionDurationIncrease").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(duration.Value == 1250 && dialog.Options.DurationMilliseconds == 1250, "Timing + did not update the draft");
                    Control<Button>(dialog, "TransitionDurationDecrease").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(duration.Value == 1150, "Timing - did not restore the duration");
                    Check(Descendants(dialog).OfType<TextBlock>().Any(t => t.Text == "Todo guardado"), "Reverting timing does not clear dirty status");
                    Check(Control<TextBlock>(dialog, "TransitionTotal").Text.Contains("1.55"), "Timeline total does not use the longest fade");
                    Control<Button>(dialog, "MoveInPosition26").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(dialog.Options.Move.In.Position == 26, "Position grid lost native upper-right value");
                    Control<ComboBox>(dialog, "MoveOutPosition").SelectedValue = 96;
                    Check(dialog.Options.Move.Out.Position == 96, "Advanced native position was lost");
                    Control<Button>(dialog, "MoveInEasing2").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(dialog.Options.Move.In.Easing == 2, "Easing segment did not update native settings");
                    Control<ComboBox>(dialog, "TransitionKind").SelectedValue = "Cut";
                    Check(!duration.IsEnabled && !Control<Button>(dialog, "MoveInPosition26").IsEnabled, "Cut leaves visual timing or Move controls enabled");
                    Control<ComboBox>(dialog, "TransitionKind").SelectedValue = "Move";
                    Check(duration.Value == 1150 && dialog.Options.Move.Out.Position == 96, "Switching effects lost the draft");
                    Control<ComboBox>(dialog, "MoveInPosition").SelectedValue = 6;
                    Control<ComboBox>(dialog, "MoveOutPosition").SelectedValue = 10;
                    Control<ComboBox>(dialog, "MoveInEasing").SelectedValue = 3;
                    Control<Slider>(dialog, "MoveInCurve").Value = .66;
                    Control<Slider>(dialog, "MoveOutCurve").Value = -.72;
                    var folder = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools", "transition-redesign");
                    System.IO.Directory.CreateDirectory(folder);
                    CaptureDesignWindow(dialog, System.IO.Path.Combine(folder, "editor.png"));
                    Control<ComboBox>(dialog, "MoveInFunction").BringIntoView(); dialog.UpdateLayout();
                    CaptureDesignWindow(dialog, System.IO.Path.Combine(folder, "move.png"));
                });
            });
            Test("Default transition settings expose fast independently editable fades", () =>
            {
                var defaults = new WorkspaceData().Transitions;
                Check(defaults.DurationMilliseconds == 300 && defaults.AudioFadeOutMilliseconds == 250
                    && defaults.AudioFadeInMilliseconds == 250, "New workspace does not use fast transition defaults");
                workspace.Transitions = defaults;
                ExerciseSettings(dialog =>
                {
                    Check(Control<Slider>(dialog, "TransitionDuration").Value == 300
                        && Control<Slider>(dialog, "AudioFadeOut").Value == 250
                        && Control<Slider>(dialog, "AudioFadeIn").Value == 250,
                        "Audio fade sliders do not show workspace defaults");
                    Check(Control<ComboBox>(dialog, "TransitionKind").SelectedValue as string == "Dissolve",
                        "Default visual transition is not selected");
                    Descendants(dialog).OfType<Button>().Single(button => button.IsCancel)
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
            });

            Test("Restoring fast defaults updates only the draft until saved", () =>
            {
                var custom = new TransitionOptions { Kind = "Slide", DurationMilliseconds = 1750,
                    AudioFadeOutMilliseconds = 2250, AudioFadeInMilliseconds = 850 };
                workspace.Transitions = custom;
                ExerciseSettings(dialog =>
                {
                    foreach (var name in new[] { "TransitionDuration", "AudioFadeOut", "AudioFadeIn" })
                    {
                        var slider = Control<Slider>(dialog, name);
                        Check(slider.SmallChange == 50 && slider.TickFrequency == 50,
                            "Transition controls cannot be adjusted in 50 millisecond steps");
                    }
                    Control<Button>(dialog, "RestoreFastTransitions").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(Control<ComboBox>(dialog, "TransitionKind").SelectedValue as string == "Dissolve"
                        && Control<Slider>(dialog, "TransitionDuration").Value == 300
                        && Control<Slider>(dialog, "AudioFadeOut").Value == 250
                        && Control<Slider>(dialog, "AudioFadeIn").Value == 250,
                        "Restore did not update all transition controls");
                    Check(dialog.Options.DurationMilliseconds == 300
                        && dialog.Options.AudioFadeOutMilliseconds == 250 && dialog.Options.AudioFadeInMilliseconds == 250,
                        "Restored controls and draft differ");
                    Check(ReferenceEquals(workspace.Transitions, custom) && custom.Kind == "Slide"
                        && custom.DurationMilliseconds == 1750 && custom.AudioFadeOutMilliseconds == 2250
                        && custom.AudioFadeInMilliseconds == 850, "Restore changed saved settings before confirmation");
                    Save(dialog);
                });
                Check(new WorkspaceStore().Load().Transitions == new TransitionOptions(),
                    "Saved fast defaults did not survive reload");
            });

            Test("Canceling edited transitions preserves saved settings and live output", () =>
            {
                workspace.Transitions = new TransitionOptions { Kind = "Cut", DurationMilliseconds = 700,
                    AudioFadeOutMilliseconds = 1500, AudioFadeInMilliseconds = 1500 };
                Check((bool)Call(window, "SaveWorkspace")!, "Could not save initial settings");
                Check((bool)Call(window, "EnsureOutput")!, "Could not initialize output");
                output = Field<ProjectionSurface>(window, "_output");
                var before = workspace.Transitions with { }; var outputBefore = output.Transitions;
                var live = session.Live; var prepared = session.Prepared;
                ExerciseSettings(dialog =>
                {
                    Control<ComboBox>(dialog, "TransitionKind").SelectedValue = "Slide";
                    Control<Slider>(dialog, "TransitionDuration").Value = 1000;
                    Control<Slider>(dialog, "AudioFadeOut").Value = 3000;
                    Control<Slider>(dialog, "AudioFadeIn").Value = 500;
                    Check(workspace.Transitions == before, "Draft edits mutated workspace before save");
                    Descendants(dialog).OfType<Button>().Single(button => button.IsCancel)
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                Check(workspace.Transitions == before && new WorkspaceStore().Load().Transitions == before,
                    "Cancel persisted draft settings");
                Check(output.Transitions == outputBefore, "Cancel changed output transition settings");
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared),
                    "Cancel changed the live or prepared cue");
            });

            Test("Saving transitions persists copied options without changing the presented frame", () =>
            {
                Check((bool)Call(window, "EnsureOutput")!, "Could not initialize output");
                output = Field<ProjectionSurface>(window, "_output");
                Until(() => !output.IsTransitioning, "Previous output transition did not settle before editing settings");
                var live = session.Live; var prepared = session.Prepared;
                var presented = output.PresentedTransitions;
                var expected = new TransitionOptions { Kind = "Zoom", DurationMilliseconds = 1200,
                    AudioFadeOutMilliseconds = 2100, AudioFadeInMilliseconds = 800 };
                var dialog = ExerciseSettings(editor =>
                {
                    Control<ComboBox>(editor, "TransitionKind").SelectedValue = expected.Kind;
                    Control<Slider>(editor, "TransitionDuration").Value = expected.DurationMilliseconds;
                    Control<Slider>(editor, "AudioFadeOut").Value = expected.AudioFadeOutMilliseconds;
                    Control<Slider>(editor, "AudioFadeIn").Value = expected.AudioFadeInMilliseconds;
                    Save(editor);
                });
                Check(workspace.Transitions == expected && new WorkspaceStore().Load().Transitions == expected,
                    "Save did not persist all transition settings");
                Check(output.Transitions == expected, "Active output did not adopt saved transition settings");
                Check(presented == output.PresentedTransitions,
                    "Saving settings re-presented the current frame");
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared),
                    "Saving settings changed the live or prepared cue");
                Field<TransitionOptions>(dialog, "_draft").Kind = "Cut";
                Check(workspace.Transitions == expected && output.Transitions == expected,
                    "Saved settings remain aliased to the dialog draft");
                workspace.Transitions.DurationMilliseconds = 2000;
                Check(output.Transitions == expected, "Output settings remain aliased to mutable workspace options");
                var retrieved = output.Transitions; retrieved.Kind = "Cut";
                Check(output.Transitions == expected, "Output getter leaked mutable settings");
                workspace.Transitions = expected with { };
            });

            Test("Failed transition persistence restores previous options and leaves output unchanged", () =>
            {
                var before = workspace.Transitions; var saved = new WorkspaceStore().Load().Transitions;
                var outputBefore = output!.Transitions; var live = session.Live;
                SetField(window, "_workspaceRecoveryBlocked", true);
                try
                {
                    ExerciseSettings(dialog =>
                    {
                        Control<ComboBox>(dialog, "TransitionKind").SelectedValue = "Slide";
                        Control<Slider>(dialog, "AudioFadeOut").Value = 4000;
                        Save(dialog);
                    });
                    Check(ReferenceEquals(before, workspace.Transitions), "Failed save retained edited workspace options");
                    Check(new WorkspaceStore().Load().Transitions == saved, "Failed save changed persisted settings");
                    Check(output.Transitions == outputBefore && ReferenceEquals(live, session.Live),
                        "Failed save changed output configuration or live cue");
                }
                finally { SetField(window, "_workspaceRecoveryBlocked", recoveryBlocked); }
            });

            Test("Cathedral controls edit only the draft and cancel preserves saved audio settings", () =>
            {
                var before = workspace.Transitions with { };
                Check((bool)Call(window, "SaveWorkspace")!, "Could not save settings before cathedral cancel test");
                var outputBefore = output!.Transitions; var live = session.Live; var prepared = session.Prepared;
                ExerciseSettings(dialog =>
                {
                    var effect = Control<ComboBox>(dialog, "AudioFadeEffect");
                    var intensity = Control<Slider>(dialog, "CathedralIntensity");
                    var tail = Control<Slider>(dialog, "CathedralTail");
                    Check(effect.SelectedValue as string == before.AudioFadeEffect, "Audio effect did not load saved settings");
                    effect.SelectedValue = "Cathedral"; intensity.Value = 42; tail.Value = 3700;
                    Check(intensity.IsEnabled && tail.IsEnabled && dialog.Options.AudioFadeEffect == "Cathedral"
                        && dialog.Options.CathedralIntensityPercent == 42 && dialog.Options.CathedralTailMilliseconds == 3700,
                        "Cathedral controls do not update normalized draft settings");
                    Check(workspace.Transitions == before && output.Transitions == outputBefore,
                        "Unsaved cathedral edits changed workspace or live output");
                    effect.SelectedValue = "Normal";
                    Check(!intensity.IsEnabled && !tail.IsEnabled && !Control<Button>(dialog, "PreviewCathedralAudio").IsEnabled,
                        "Normal fade leaves cathedral-only controls enabled");
                    Check(dialog.Options.AudioFadeEffect == "Normal" && dialog.Options.CathedralIntensityPercent == 42
                        && dialog.Options.CathedralTailMilliseconds == 3700, "Changing fade mode lost the cathedral draft");
                    Control<Button>(dialog, "CancelTransitions").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
                Check(workspace.Transitions == before && new WorkspaceStore().Load().Transitions == before,
                    "Cancel persisted cathedral draft edits");
                Check(output.Transitions == outputBefore && ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared),
                    "Canceling cathedral settings changed output or cues");
            });

            Test("Cathedral audio settings save reload preview and restore normal defaults", () =>
            {
                var before = workspace.Transitions with { }; var outputBefore = output!.Transitions;
                try
                {
                    ExerciseSettings(dialog =>
                    {
                        Control<ComboBox>(dialog, "AudioFadeEffect").SelectedValue = "Cathedral";
                        Control<Slider>(dialog, "CathedralIntensity").Value = 42;
                        Control<Slider>(dialog, "CathedralTail").Value = 3700;
                        Save(dialog);
                    });
                    var expected = before with { AudioFadeEffect = "Cathedral", CathedralIntensityPercent = 42, CathedralTailMilliseconds = 3700 };
                    Check(workspace.Transitions == expected && new WorkspaceStore().Load().Transitions == expected,
                        "Cathedral audio settings did not survive save and reload");
                    var live = session.Live; var prepared = session.Prepared; var presented = output.PresentedTransitions;
                    ExerciseSettings(dialog =>
                    {
                        Check(Control<ComboBox>(dialog, "AudioFadeEffect").SelectedValue as string == "Cathedral"
                            && Control<Slider>(dialog, "CathedralIntensity").Value == 42
                            && Control<Slider>(dialog, "CathedralTail").Value == 3700,
                            "Reopened editor did not load cathedral settings");
                        var preview = Control<Button>(dialog, "PreviewCathedralAudio");
                        preview.BringIntoView(); dialog.UpdateLayout(); PumpFor(100);
                        var folder = System.IO.Path.Combine(Environment.CurrentDirectory, ".tools"); System.IO.Directory.CreateDirectory(folder);
                        CaptureDesignWindow(dialog, System.IO.Path.Combine(folder, "cathedral-editor.png"));
                        preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var status = Control<TextBlock>(dialog, "CathedralAudioPreviewStatus");
                        Until(() => status.Text == "Prueba de audio finalizada." || status.Text.StartsWith("No se pudo reproducir"),
                            "Cathedral preview did not finish or report an error within eight seconds");
                        Check(status.Text == "Prueba de audio finalizada.", "Cathedral preview failed: " + status.Text);
                        Check(preview.IsEnabled && !Control<Button>(dialog, "StopCathedralAudio").IsEnabled,
                            "Cathedral preview did not restore playback controls");
                        Check(output.Transitions == expected && presented == output.PresentedTransitions
                            && ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared),
                            "Independent cathedral preview changed live output or cues");
                        Control<Button>(dialog, "RestoreFastTransitions").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(dialog.Options.AudioFadeEffect == "Normal" && dialog.Options.CathedralIntensityPercent == 25
                            && dialog.Options.CathedralTailMilliseconds == 2500 && !Control<Slider>(dialog, "CathedralIntensity").IsEnabled
                            && !Control<Slider>(dialog, "CathedralTail").IsEnabled,
                            "Restore did not reset cathedral controls to normal defaults");
                        Check(workspace.Transitions == expected, "Restoring audio defaults changed workspace before save");
                        Save(dialog);
                    });
                    Check(workspace.Transitions == new TransitionOptions() && new WorkspaceStore().Load().Transitions == new TransitionOptions(),
                        "Restored normal audio defaults did not persist");
                }
                finally
                {
                    workspace.Transitions = before; output.Transitions = outputBefore;
                    Call(window, "SaveWorkspace");
                }
            });
        }
        finally
        {
            SetField(window, "_workspaceRecoveryBlocked", recoveryBlocked);
            workspace.Transitions = original;
            if (!hadOutput) Call(window, "CloseOutput");
            else if (oldOutputOptions is not null && Field<ProjectionSurface?>(window, "_output") is { } currentOutput)
                currentOutput.Transitions = oldOutputOptions;
            Call(window, "SaveWorkspace");
        }
    }
}
