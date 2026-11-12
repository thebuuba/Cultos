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
            Test("Default transition settings expose one and a half second audio fades", () =>
            {
                var defaults = new WorkspaceData().Transitions;
                Check(defaults.AudioFadeOutMilliseconds == 1500 && defaults.AudioFadeInMilliseconds == 1500,
                    "New workspace no longer defaults both audio fades to 1.5 seconds");
                workspace.Transitions = defaults;
                ExerciseSettings(dialog =>
                {
                    Check(Control<Slider>(dialog, "AudioFadeOut").Value == 1500
                        && Control<Slider>(dialog, "AudioFadeIn").Value == 1500,
                        "Audio fade sliders do not show workspace defaults");
                    Check(Control<ComboBox>(dialog, "TransitionKind").SelectedValue as string == "Dissolve",
                        "Default visual transition is not selected");
                    Descendants(dialog).OfType<Button>().Single(button => button.IsCancel)
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
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
                var presented = Field<TransitionOptions>(output, "_presentedTransitions");
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
                Check(ReferenceEquals(presented, Field<TransitionOptions>(output, "_presentedTransitions")),
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
