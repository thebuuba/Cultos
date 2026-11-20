using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int failures;
    private static int closePromptsObserved;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object? Call(object instance, string method, params object?[] args) => instance.GetType().GetMethod(method, Private)!.Invoke(instance, args);
    private static T Field<T>(object instance, string name)
    {
        if (instance is ProjectionSurface surface && name != "_mixer") instance = surface.ActiveSurface;
        return (T)instance.GetType().GetField(name, Private)!.GetValue(instance)!;
    }
    private static void SetField(object instance, string name, object? value) => instance.GetType().GetField(name, Private)!.SetValue(instance, value);
    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
    private static void Test(string name, Action action)
    {
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + (error.InnerException ?? error).Message); }
    }
    private static void PumpFor(int milliseconds) => Wait(Task.Delay(milliseconds));
    private static void Until(Func<bool> condition, string failure)
    {
        var until = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < until) PumpFor(50);
        Check(condition(), failure);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject value)
    {
        yield return value;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(value, i))) yield return child;
    }
    private static IEnumerable<DependencyObject> Ancestors(DependencyObject value)
    {
        for (var parent = VisualTreeHelper.GetParent(value); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            yield return parent;
    }
    private static Rect BoundsWithin(FrameworkElement element, Visual ancestor) =>
        element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
    private static void CheckWithin(FrameworkElement element, Visual ancestor, Rect viewport, string description)
    {
        Check(element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0, description + " is not visible");
        var bounds = BoundsWithin(element, ancestor);
        var tolerantViewport = viewport; tolerantViewport.Inflate(1.5, 1.5);
        Check(tolerantViewport.Contains(bounds), $"{description} overflows its viewport: {bounds} outside {viewport}");
    }
    private static void CheckOrderNumber(ListBox plan, int index, string expected)
    {
        plan.ScrollIntoView(plan.Items[index]); plan.UpdateLayout();
        var row = plan.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
        Check(row is not null, "Order row was not rendered");
        Check(Descendants(row!).OfType<TextBlock>().Any(text => text.IsVisible && text.ActualWidth > 0 && text.Text == expected),
            $"Order row {index + 1} does not display number {expected}");
    }
    private static void AnswerCloseDialog(string buttonName)
    {
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Cambios sin guardar");
            if (dialog is null) return;
            closePromptsObserved++;
            var buttons = Descendants(dialog).OfType<Button>().ToArray();
            Check(buttons.Single(b => b.Name == "KeepOpenButton").IsDefault, "Cancel must be the default close decision");
            buttons.Single(b => b.Name == buttonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--bundled-content")) return RunBundledContentTests();
        // This route creates only disconnected controls: no App, Window, native
        // player, dispatcher loop or desktop interaction.
        if (args.Contains("--scene-config")) return RunSceneConfigurationTests();
        if (args.Contains("--video-scene-selection")) return RunVideoSceneSelectionTests();
        if (args.Contains("--youtube-sources")) return RunYouTubeSourceTests();
        if (args.Contains("--capture-layout-headless")) return RunCaptureLayoutHeadlessTests();
        if (args.Contains("--media-layout-headless")) return RunMediaLayoutHeadlessTests();
        var directory = Path.Combine(Path.GetTempPath(), "CultoDesktopSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable("CULTO_DATA_DIR", directory);
        try
        {
            Test("Initial plan and library have independent cues", () =>
            {
                var seeded = new WorkspaceStore().Load();
                seeded.Library[0].Body = "Biblioteca editada";
                Check(seeded.Plan.Items[0].Body == "Bienvenidos\na la casa de Dios", "Library edits changed the seeded plan");
                Check(seeded.Plan.Items[0].Id != seeded.Library[0].Id, "Seeded library and plan share cue IDs");
            });

            if ((args.Contains("--cold-play") || args.Contains("--first-play") || args.Contains("--floating-continuity")) && Environment.GetEnvironmentVariable("CULTO_FIRST_PLAY_WORKSPACE") is { } sourceDirectory)
                foreach (var name in new[] { "workspace.json", "biblia.json", "dock-layout.xml", "paneles-v3.xml" })
                    if (File.Exists(Path.Combine(sourceDirectory, name))) File.Copy(Path.Combine(sourceDirectory, name), Path.Combine(directory, name), true);
            if (args.Contains("--logo-startup")) SeedLogoStartupWorkspace(directory, args.Contains("--missing-logo"));
            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (args.Contains("--native-startup"))
            {
                app.Startup += (_, _) => app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    Test("Native startup creates one visible console without an explicit host window", () =>
                    {
                        Check(app.Windows.OfType<MainWindow>().Count() == 1 && app.MainWindow is MainWindow { IsVisible: true }, "Native startup did not show exactly one console");
                        Check(app.MainWindow.Icon is { Width: > 0, Height: > 0 }, "Console application icon is missing or could not be decoded");
                    });
                    app.MainWindow?.Close();
                }));
                app.Run();
                return failures == 0 ? 0 : 1;
            }
            // Rendering/animation checks need a visible compositor surface even
            // when the user's normal console or Office is also open.
            var window = new MainWindow { Topmost = true, WindowState = WindowState.Normal };
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                window.Show();
                app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    if (args.Contains("--updates")) RunUpdateTests(window);
                    try { Test("Smoke host contains only its explicit console window", () => Check(app.Windows.OfType<MainWindow>().Count() == 1, "StartupUri opened a second console")); if (args.Contains("--bible-style-save")) RunBibleStylePersistenceTests(window); else if (args.Contains("--text-selection")) RunTextSelectionTests(window); else if (args.Contains("--output-controls")) RunOutputControlTests(window); else if (args.Contains("--church-setup")) RunChurchSetupTests(window); else if (args.Contains("--panel-geometry")) RunWorkspaceGeometryTests(window); else if (args.Contains("--dense-media")) RunDenseMediaTests(window); else if (args.Contains("--logo-startup")) RunLogoStartupTests(window, args.Contains("--missing-logo")); else if (args.Contains("--compact-panels")) RunCompactPanelTests(window); else if (args.Contains("--compact-order")) RunCompactOrderTests(window); else if (args.Contains("--layer-editor")) RunLayerEditorTests(window); else if (args.Contains("--transition-presets")) RunTransitionPresetTests(window); else if (args.Contains("--scene-gestures")) RunSceneGestureTests(window); else if (args.Contains("--editable-scenes")) RunEditableSceneTests(window); else if (args.Contains("--bible-versions")) RunBibleVersionTests(window); else if (args.Contains("--notes")) RunNotesTests(window); else if (args.Contains("--text-audit")) RunTextAuditTests(window); else if (args.Contains("--typography")) RunTypographyTests(window); else if (args.Contains("--tab-text")) RunTabTextTests(window); else if (args.Contains("--bible-projection")) { RunBibleProjectionTests(window); RunBibleSelectionTests(window); } else if (args.Contains("--floating-continuity")) RunFloatingContinuityTests(window); else if (args.Contains("--simple-console")) RunSimpleConsoleTests(window); else if (args.Contains("--docking")) RunDockingTests(window, directory); else if (args.Contains("--powerpoint-refresh")) RunPowerPointRefreshTests(window, directory); else if (args.Contains("--powerpoint-card")) RunPowerPointPanelTests(window, directory); else if (args.Contains("--powerpoint-panel")) { RunPowerPointPanelTests(window, directory); RunPowerPointTests(window, directory); RunPresenterHotkeysTests(window); } else if (args.Contains("--split-controls")) RunSplitControlsTests(window); else if (args.Contains("--youtube-order-links")) RunYouTubeOrderLinkTests(window, directory, args.Contains("--native-youtube-links")); else if (args.Contains("--file-location")) RunFileLocationTests(window, directory); else if (args.Contains("--hymn-isolation")) RunHymnIsolationTests(window); else if (args.Contains("--hymn-playlist")) RunHymnPlaylistTests(window); else if (args.Contains("--chrome-occluded")) RunChromeOccludedTests(window, args.Contains("--chrome-occluded-baseline")); else if (args.Contains("--remote-scenes")) RunRemoteSceneTests(window); else if (args.Contains("--browser-rendering")) RunBrowserRenderingTests(window); else if (args.Contains("--browser-sources")) RunBrowserSourcesTests(window); else if (args.Contains("--chrome-maximized")) RunChromeMaximizedTests(window); else if (args.Contains("--reported-playback")) RunReportedPlaybackTests(window); else if (args.Contains("--video-lists")) RunVideoListTests(window); else if (args.Contains("--fixed-monitors")) RunFixedMonitorTests(window); else if (args.Contains("--production-demo")) RunProductionDemo(window); else if (args.Contains("--production-card")) RunProductionCardTests(window); else if (args.Contains("--sequence-removal")) RunSequenceRemovalTests(window); else if (args.Contains("--panel-visibility")) RunPanelVisibilityTests(window); else if (args.Contains("--panel-drag-demo")) { window.Title = "Culto Studio - Ensayo de arrastre"; window.Width = 1600; window.Height = 900; Call(window, "ApplyWorkspacePreset", "principal", false); window.UpdateLayout(); Console.WriteLine("DRAG READY"); PumpFor(55000); } else if (args.Contains("--panel-sizing")) RunPanelSizingTests(window); else if (args.Contains("--workspace-layouts")) RunWorkspacePresetTests(window, directory); else if (args.Contains("--visual-theme")) RunVisualThemeTests(window); else if (args.Contains("--browser-capture-compat")) RunBrowserCaptureCompatibilityTests(window); else if (args.Contains("--content-drag")) RunContentDragDropTests(window); else if (args.Contains("--youtube-controls")) RunYouTubeControlsTests(window); else if (args.Contains("--bible-to-video")) RunBibleToVideoTests(window); else if (args.Contains("--first-play")) RunColdPlaybackTests(window, engineTests: false); else if (args.Contains("--cold-play")) RunColdPlaybackTests(window); else if (args.Contains("--order")) RunOrderInteractionTests(window); else if (args.Contains("--output-player")) RunOutputPlayerWindowTests(window); else if (args.Contains("--window-capture-picker")) RunWindowCapturePickerTests(window); else if (args.Contains("--transition-settings")) RunTransitionSettingsTests(window); else if (args.Contains("--powerpoint-moderator")) RunPowerPointModeratorTests(window, directory); else if (args.Contains("--powerpoint")) RunPowerPointTests(window, directory); else if (args.Contains("--stream-deck-native-fixture")) RunStreamDeckNativeFixture(window, directory); else if (args.Contains("--stream-deck-native")) RunStreamDeckNativeTests(window, directory); else if (args.Contains("--stream-deck")) RunStreamDeckTests(window); else if (args.Contains("--presenter")) RunPresenterHotkeysTests(window); else if (args.Contains("--saved-sets")) RunSavedSetsTests(window); else if (args.Contains("--bible-tools")) { RunBibleSearchTests(window, directory); RunBibleSelectionTests(window); RunBibleStyleIntegrationTests(window); RunBibleEditorTests(window); } else if (args.Contains("--content-sources")) { RunLocalMediaFolderServiceTests(); RunFolderSourcesTests(window); RunContentDragDropTests(window); } else if (args.Contains("--local-output")) RunLocalOutputTests(window); else if (args.Contains("--compact-output")) RunCompactOutputTests(window); else if (args.Contains("--player")) RunPlayerControlsTests(window); else if (args.Contains("--backdrops")) RunBackdropControlsTests(window); else if (args.Contains("--remote-order")) { RunRemoteOrderTests(window); RunRemoteDialogTests(window); } else RunWindowTests(window, directory); }
                    catch (Exception error) { failures++; Console.WriteLine("FAIL smoke callback: " + error); }
                    finally
                    {
                        if (!Field<bool>(window, "_closing"))
                        {
                            Call(window, "DisposePresenterHotkeys"); Call(window, "DisposeStreamDeckHotkeys");
                            Wait((Task)Call(window, "DisposeStreamDeckPluginAsync")!);
                            Call(window, "DisposeLibraryFolders");
                            Call(window, "CloseOutput");
                            Field<IDisposable>(window, "_preview").Dispose();
                            Field<DispatcherTimer>(window, "_timer").Stop();
                            SetField(window, "_closing", true);
                            window.Close();
                            app.Shutdown();
                        }
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    }
                }));
            }));
            Dispatcher.Run();
            // This host bypasses Application.Run/OnExit and releases its owned resources explicitly.
            Field<FileStream?>(app, "_workspaceSession")?.Dispose();
        }
        catch (Exception error) { failures++; Console.WriteLine("FAIL smoke host: " + error); }
        finally
        {
            Environment.SetEnvironmentVariable("CULTO_DATA_DIR", null);
            Directory.Delete(directory, true);
        }
        Console.WriteLine($"Desktop smoke: {failures} failure(s)");
        return failures == 0 ? 0 : 1;
    }

    private static void RunWindowTests(MainWindow window, string directory)
    {
        var session = Field<PresentationSession>(window, "_session");
        Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
        Test("Preparing never changes live snapshot", () =>
        {
            var original = new Cue { Title = "Sintético A", Body = "Original sintético" };
            Call(window, "PrepareCue", original, null);
            Call(window, "ProjectPrepared");
            original.Body = "Editado sintético";
            Call(window, "PrepareCue", new Cue { Title = "Sintético B" }, null);
            Check(session.Live!.Body == "Original sintético", "Preparing/edits changed live");
        });

        RunDesignTests(window, directory, session);

        Test("Bible navigation keeps live reading after another reading is prepared", () =>
        {
            Cue Verse(string title) => new() { Title = title, Body = "Contenido sintético " + title, Kind = CueKind.Bible };
            IReadOnlyList<Cue> a = [Verse("A1"), Verse("A2"), Verse("A3")];
            IReadOnlyList<Cue> b = [Verse("B1"), Verse("B2")];
            Call(window, "PrepareCue", a[0], a); Call(window, "ProjectPrepared");
            Call(window, "PrepareCue", b[0], b);
            Call(window, "MoveLive", 1);
            Check(session.Live!.Title == "A2", "Next used selection instead of live");
            Call(window, "ProjectPrepared"); Call(window, "MoveLive", 1);
            Check(session.Live!.Title == "A3", "Reprojecting changed reading context to B");
            Call(window, "MoveLive", -1);
            Check(session.Live!.Title == "A2", "Previous left reading A");
        });

        Test("Unknown live cue cannot jump to first verse", () =>
        {
            var unrelated = new Cue { Title = "Ajeno", Kind = CueKind.Bible };
            session.Prepare(unrelated); session.GoLive();
            SetField(window, "_liveReading", new[] { new Cue { Title = "No proyectar" } });
            Call(window, "MoveLive", 1);
            Check(session.Live!.Id == unrelated.Id, "Missing live ID was treated as position -1");
        });

        Test("Invalid image preserves live content", () =>
        {
            var before = session.Live!.Id;
            var path = Path.Combine(directory, "invalid.png"); File.WriteAllText(path, "Imagen sintética inválida");
            Call(window, "PrepareCue", MediaLibrary.FromFile(path), null); Call(window, "ProjectPrepared");
            Check(session.Live!.Id == before, "Invalid image replaced live");
        });

        Test("Workspace save persists UI changes", () =>
        {
            ((TextBox)window.FindName("PlanNameBox")).Text = "Culto sintético guardado";
            Call(window, "SaveWorkspace");
            Check(new WorkspaceStore().Load().Plan.Name == "Culto sintético guardado", "Workspace name did not persist");
        });

        var biblePath = Path.Combine(directory, "biblia.json");
        BibleDocument SyntheticBible(string name) => new() { Name = name, Attribution = "Prueba sintética", Verses = [new("Juan", 3, 16, "Primero sintético"), new("Juan", 3, 17, "Segundo sintético"), new("Juan", 3, 18, "Tercero sintético")] };
        Test("Saved Bible recovers valid backup", () =>
        {
            JsonFileStore.Save(biblePath, SyntheticBible("Respaldo sintético"));
            JsonFileStore.Save(biblePath, SyntheticBible("Principal sintético"));
            File.WriteAllText(biblePath, "Principal inválido");
            SetField(window, "_bible", null);
            Call(window, "LoadSavedBible");
            Check(Field<BibleCatalog?>(window, "_bible")?.Document.Name == "Respaldo sintético", "Saved Bible did not recover .bak");
        });
        Test("Single-verse spontaneous reading advances canonically", () =>
        {
            JsonFileStore.Save(biblePath, SyntheticBible("Lectura sintética"));
            Call(window, "LoadSavedBible");
            ((TextBox)window.FindName("BibleQuery")).Text = "Juan 3:16";
            Call(window, "SearchBible"); Call(window, "ProjectPrepared");
            Check(session.Live!.Reference == "Juan 3:16", "Single verse was not projected");
            Call(window, "MoveLive", 1);
            Check(session.Live!.Reference == "Juan 3:17", "Single-verse reading stopped at the search result");
        });
        Test("Saved Bible loads backup when principal is missing", () =>
        {
            File.Delete(biblePath);
            SetField(window, "_bible", null);
            Call(window, "LoadSavedBible");
            Check(Field<BibleCatalog?>(window, "_bible") is not null, "Backup-only Bible was ignored");
        });
        Test("New import keeps old live reading independent", () =>
        {
            var liveVersion = session.Live!.Version;
            var external = Path.Combine(directory, "external.json");
            JsonFileStore.Save(external, SyntheticBible("Nueva versión sintética"));
            Wait((Task)Call(window, "ImportBibleFileAsync", external)!);
            Call(window, "MoveLive", 1);
            Check(session.Live!.Reference == "Juan 3:18" && session.Live.Version == liveVersion, "Import changed the current live reading");
        });
        Test("Phrase result advances to adjacent canonical verse", () =>
        {
            ((TextBox)window.FindName("BibleQuery")).Text = "Primero";
            Call(window, "SearchBible"); Call(window, "ProjectPrepared");
            Call(window, "MoveLive", 1);
            Check(session.Live!.Reference == "Juan 3:17", "Phrase result did not advance canonically");
        });
        Test("Invalid new import preserves saved and live Bible", () =>
        {
            var catalog = Field<BibleCatalog>(window, "_bible");
            var before = session.Live!.Id;
            var invalid = Path.Combine(directory, "invalid-bible.json"); File.WriteAllText(invalid, "Inválido");
            try { Wait((Task)Call(window, "ImportBibleFileAsync", invalid)!); throw new InvalidOperationException("Invalid source accepted"); }
            catch (InvalidDataException) { }
            Check(ReferenceEquals(catalog, Field<BibleCatalog>(window, "_bible")), "Invalid import changed working catalog");
            Check(session.Live!.Id == before, "Invalid import changed live");
            Check(JsonFileStore.Load<BibleDocument>(biblePath)!.Name == catalog.Document.Name, "Invalid import changed saved Bible");
        });
        Test("Valid reimport archives corrupt Bible files before replacing", () =>
        {
            File.WriteAllText(biblePath, "Principal dañado"); File.WriteAllText(biblePath + ".bak", "Respaldo dañado");
            var external = Path.Combine(directory, "external.json");
            Wait((Task)Call(window, "ImportBibleFileAsync", external)!);
            Check(Directory.GetFiles(directory, "biblia.json*.recovery-*").Length == 2, "Damaged Bible originals were not both archived");
            Check(JsonFileStore.Load<BibleDocument>(biblePath)!.Name == "Nueva versión sintética", "Valid import was not persisted");
        });
        Test("Corrupt startup workspace archives originals and permits new saves", () =>
        {
            var recoveryDirectory = Path.Combine(directory, "startup-recovery"); Directory.CreateDirectory(recoveryDirectory);
            var path = Path.Combine(recoveryDirectory, "workspace.json");
            File.WriteAllText(path, "Principal sintético dañado"); File.WriteAllText(path + ".bak", "Respaldo sintético dañado");
            Environment.SetEnvironmentVariable("CULTO_DATA_DIR", recoveryDirectory);
            MainWindow? recovered = null;
            try
            {
                recovered = new MainWindow(); recovered.Show();
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                ((TextBox)recovered.FindName("PlanNameBox")).Text = "Recuperado sintético";
                Check(Call(recovered, "SaveWorkspace") is true, "New workspace still blocked by corrupt principal");
                Check(new WorkspaceStore().Load().Plan.Name == "Recuperado sintético", "Recovered workspace not saved");
                var archives = Directory.GetFiles(recoveryDirectory, "*.recovery-*");
                Check(archives.Length == 2, "Corrupt workspace files not archived");
                Check(archives.Select(File.ReadAllText).Contains("Principal sintético dañado"), "Corrupt principal content was lost");
            }
            finally
            {
                if (recovered is not null)
                {
                    Field<IDisposable>(recovered, "_preview").Dispose(); Field<DispatcherTimer>(recovered, "_timer").Stop();
                    SetField(recovered, "_closing", true); recovered.Close();
                }
                Environment.SetEnvironmentVariable("CULTO_DATA_DIR", directory);
            }
        });

        Test("Blackout restore after reopening output never starts native playback", () =>
        {
            var video = MediaLibrary.FromFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv"));
            ((Slider)window.FindName("VolumeSlider")).Value = 0;
            Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
            var output = Field<ProjectionSurface>(window, "_output");
            Until(() => output.IsPlaying && output.Duration > 2.5, "Synthetic MKV did not play through native VLC");
            Call(window, "ToggleBlackout");
            Until(() => !output.IsPlaying, "Blackout did not pause native VLC");
            Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
            output = Field<ProjectionSurface>(window, "_output");
            Check(session.IsBlackout, "Reopening output lost retained blackout state");
            Call(window, "ToggleBlackout");
            for (var i = 0; i < 12; i++)
            {
                PumpFor(50);
                Check(!output.IsPlaying, "Restoring recreated output restarted native playback without Continue");
            }
            var pausedPosition = output.Position;
            PumpFor(250);
            Check(Math.Abs(output.Position - pausedPosition) < .15, "Restored native playback position advanced while paused");
            Call(window, "TogglePause");
            Until(() => output.IsPlaying, "Explicit Continue did not start native playback");
            Call(window, "ToggleBlackout"); Until(() => !output.IsPlaying, "Second blackout did not pause");
            Call(window, "ProjectPrepared");
            Until(() => output.IsPlaying, "Explicit Project did not start retained media");
            Call(window, "CloseOutput");
        });

        Test("Projecting locally does not open a rehearsal window", () =>
        {
            Call(window, "CloseOutput");
            Call(window, "PrepareCue", new Cue { Title = "Salida local", Body = "Sin HDMI" }, null);
            Call(window, "ProjectPrepared");
            Check(Field<ProjectionWindow?>(window, "_outputWindow") is null, "Project opened an unwanted window on the operator screen");
            Check(session.Live?.Title == "Salida local", "Missing HDMI blocked the local output");
        });
        RunLocalOutputTests(window);
        Call(window, "OpenOutput", DisplayService.GetDisplays().First(), true);
        RunOutputControlTests(window);
        RunThemeTests(window);
        RunCompactOutputTests(window);
        RunDockingTests(window, directory);
        RunUsabilityTests(window, directory);
        RunOrderInteractionTests(window);
        RunBibleNavigationTests(window, directory);
        RunBibleSearchTests(window, directory);
        RunBibleSelectionTests(window);
        RunBibleStyleIntegrationTests(window);
        RunBibleEditorTests(window);
        RunSavedSetsTests(window); RunPresenterHotkeysTests(window); RunTransitionSettingsTests(window);
        RunRemoteOrderTests(window);
        RunRemoteDialogTests(window);
        RunPlayerControlsTests(window);
        RunBackdropControlsTests(window);
        RunLocalMediaFolderServiceTests();
        RunFolderSourcesTests(window);
        RunContentDragDropTests(window);

        Test("Failed save prevents closing", () =>
        {
            var path = Path.Combine(directory, "workspace.json");
            File.WriteAllText(path, "Principal dañado");
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            Check(Call(window, "SaveWorkspace") is false, "SaveWorkspace did not expose failure");
            var closing = new CancelEventArgs();
            var before = closePromptsObserved;
            AnswerCloseDialog("KeepOpenButton");
            Call(window, "OnWindowClosing", window, closing);
            Check(closePromptsObserved == before + 1, "Failed save did not offer a deliberate close/cancel decision");
            Check(closing.Cancel && !Field<bool>(window, "_closing"), "Closing discarded unsaved edits");
            Check(window.IsVisible, "Window closed after a failed save");
        });
        Test("Export and further edits still require explicit close-without-save choice", () =>
        {
            var exported = Path.Combine(directory, "export", "culto.culto.json");
            Call(window, "ExportPlan", exported);
            Check(JsonFileStore.Load<ServicePlan>(exported) is not null, "Export after local-save failure was not persisted");
            ((TextBox)window.FindName("PlanNameBox")).Text = "Nueva edición después de exportar";
            var before = closePromptsObserved;
            AnswerCloseDialog("KeepOpenButton");
            var cancel = new CancelEventArgs(); Call(window, "OnWindowClosing", window, cancel);
            Check(closePromptsObserved == before + 1 && cancel.Cancel && !Field<bool>(window, "_closing"), "Further edits bypassed unsaved protection");
            AnswerCloseDialog("CloseWithoutSavingButton");
            window.Close();
            Check(closePromptsObserved == before + 2 && Field<bool>(window, "_closing"), "Explicit user choice did not permit closing");
            PumpFor(100);
            Check(!window.IsVisible, "User could not close after export despite explicit consent");
        });
    }

    private static void RunDesignTests(MainWindow window, string directory, PresentationSession session)
    {

        var library = (ListBox)window.FindName("LibraryList");
        var filter = (ListBox)window.FindName("LibraryFilter");
        var search = (TextBox)window.FindName("LibrarySearch");
        var count = (TextBlock)window.FindName("LibraryCountText");
        var clear = (Button)window.FindName("ClearSearchButton");
        var plan = (ListBox)window.FindName("PlanList");

        Test("Library search and categories show counts while clear retains category and live", () =>
        {
            Call(window, "SelectContentSource", 0); filter.SelectedIndex = 0; search.Clear();
            var previousCount = library.Items.Count;
            var live = session.Live!; var liveBody = live.Body;
            var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv");
            var first = Path.Combine(directory, "042 Alabanza sintética.mkv");
            var second = Path.Combine(directory, "043 Meditación sintética.mkv");
            var image = Path.Combine(directory, "Imagen sintética.png");
            File.Copy(fixture, first); File.Copy(fixture, second);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 40, 80, 100, 255 }, 4)));
            using (var stream = File.Create(image)) encoder.Save(stream);
            Wait((Task)Call(window, "ImportMediaAsync", (object)new[] { first, second, image })!);
            Field<WorkspaceData>(window, "_workspace").Library.Add(new Cue { Kind = CueKind.Text, Title = "Aviso sint\u00e9tico especial", Body = "Anuncio de prueba" });
            Call(window, "RefreshLibrary");
            var total = previousCount + 4;
            void CheckCount(int expected)
            {
                Check(library.Items.Count == expected, $"Expected {expected} library results, got {library.Items.Count}");
                Check(count.Text == $"{expected} de {total} contenidos", "Library count disagrees with visible results and imported total");
            }
            CheckCount(total);
            filter.SelectedIndex = 1; CheckCount(2);
            // Leading zeros make this a hymn-number lookup rather than a title substring match.
            search.Text = "00042"; CheckCount(1);
            Check(((Cue)library.Items[0]).Title == "042 Alabanza sintética", "Hymn number search selected the wrong item");
            search.Text = "zz-sin-resultados"; CheckCount(0);
            Check(((TextBlock)window.FindName("LibraryEmpty")).IsVisible, "Empty filtered search has no visible guidance");
            Check(clear.IsEnabled, "Cannot clear a nonempty search");
            window.Activate(); clear.Focus();
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(() => search.IsKeyboardFocusWithin, "Clear search did not return keyboard focus to the search field");
            Check(search.Text.Length == 0 && filter.SelectedIndex == 1, "Clear search changed the selected category");
            CheckCount(2);
            Check(!clear.IsEnabled, "Clear search remains enabled with an empty query");
            library.SelectedIndex = 0;
            Field<Dictionary<int, string>>(window, "_folderPaths")[3] = directory;
            var folders = (ListBox)window.FindName("MediaFolders");
            var media = (ListBox)window.FindName("MediaList");
            var oldFolder = folders.SelectedValue;
            try
            {
                Call(window, "SetLibraryFolder", -1);
                folders.SelectedValue = "3";
                Until(() => !Field<bool>(window, "_folderLoading"), "Pictures folder did not finish loading");
                Check(media.Items.Count == 1 && ((TextBlock)window.FindName("MediaCountText")).Text == "1 archivos", "Pictures source did not show its image/count");
                Check(((Cue)media.Items[0]).Kind == CueKind.Image, "Pictures source contains a different media type");
            }
            finally { folders.SelectedValue = oldFolder; }
            filter.SelectedIndex = 0; search.Text = "aviso sintético"; CheckCount(1);
            Check(((Cue)library.Items[0]).Title == "Aviso sintético especial", "Text title search missed the saved announcement");
            search.Clear(); filter.SelectedIndex = 0; CheckCount(total);
            Check(ReferenceEquals(live, session.Live) && session.Live.Body == liveBody, "Library browsing/import/search changed the live snapshot");
        });

        Test("Order numbers follow the visible rows after moving an item", () =>
        {
            var live = session.Live!.Id;
            var moved = (Cue)plan.Items[2];
            plan.SelectedIndex = 2;
            Call(window, "MoveUpClick", window, new RoutedEventArgs());
            window.UpdateLayout();
            Check(((Cue)plan.Items[1]).Id == moved.Id, "Move up did not reorder the selected cue");
            CheckOrderNumber(plan, 0, "01");
            CheckOrderNumber(plan, 1, "02");
            CheckOrderNumber(plan, 2, "03");
            var renderedSecond = (ListBoxItem)plan.ItemContainerGenerator.ContainerFromIndex(1)!;
            Check(((Cue)renderedSecond.DataContext).Id == moved.Id, "Visible second row does not represent the moved cue");
            Check(session.Live!.Id == live, "Reordering the plan changed live output");
        });

        Test("Empty order guidance disappears when content is added again", () =>
        {
            var live = session.Live!.Id;
            while (plan.Items.Count > 0)
            {
                plan.SelectedIndex = 0;
                Call(window, "RemoveCueClick", window, new RoutedEventArgs());
            }
            window.UpdateLayout();
            var empty = (TextBlock)window.FindName("PlanEmptyText");
            Check(empty.IsVisible && ((TextBlock)window.FindName("PlanCountText")).Text == "0", "Empty plan does not show guidance and zero count");
            filter.SelectedIndex = 0; search.Text = "aviso sintético";
            library.SelectedIndex = 0;
            Call(window, "AddLibraryToPlanClick", window, new RoutedEventArgs());
            window.UpdateLayout();
            Check(plan.Items.Count == 1 && empty.Visibility == Visibility.Collapsed, "Adding content did not restore the order list");
            CheckOrderNumber(plan, 0, "01");
            Check(session.Live!.Id == live, "Removing/adding prepared cues changed live output");
            search.Clear(); filter.SelectedIndex = 0;
        });

        Test("Virtualized deep order rows keep correct numbers after moving and removing", () =>
        {
            var live = session.Live!.Id;
            while (plan.Items.Count > 0)
            {
                plan.SelectedIndex = 0;
                Call(window, "RemoveCueClick", window, new RoutedEventArgs());
            }
            for (var number = 1; number <= 80; number++)
                Call(window, "AddToPlan", new Cue { Title = $"Momento sintético {number}", Body = "Texto sintético para probar el orden" });
            CheckOrderNumber(plan, 0, "01");
            CheckOrderNumber(plan, 70, "71");
            CheckOrderNumber(plan, 79, "80");
            Check(Descendants(plan).OfType<ListBoxItem>().Count() < 40, "Deep order created every row instead of virtualizing the list");
            var moved = (Cue)plan.Items[70];
            plan.SelectedIndex = 70;
            Call(window, "MoveUpClick", window, new RoutedEventArgs());
            Check(((Cue)plan.Items[69]).Id == moved.Id, "Deep selected cue did not move up");
            CheckOrderNumber(plan, 69, "70");
            plan.SelectedIndex = 0;
            Call(window, "RemoveCueClick", window, new RoutedEventArgs());
            Check(((Cue)plan.Items[68]).Id == moved.Id, "Removing an earlier cue misplaced the moved deep item");
            CheckOrderNumber(plan, 68, "69");
            CheckOrderNumber(plan, 78, "79");
            Check(session.Live!.Id == live, "Deep scrolling/reorder/removal changed live output");
        });

        Test("Preparing videos keeps live output and never creates preview playback", () =>
        {
            var live = session.Live!.Id;
            var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv");
            Call(window, "PrepareCue", new Cue { Title = "Prepared video", Kind = CueKind.Video, FilePath = fixture }, null);
            Check(window.FindName("PreviewPauseButton") is null, "Preview retains independent controls");
            var preview = Field<ProjectionSurface>(window, "_preview");
            Until(() => preview.IsPlaying, "Prepared video did not autoplay silently");
            Check(preview.Volume == 0, "Prepared video is audible");
            Check(((FrameworkElement)window.FindName("PreviewHost")).Visibility == Visibility.Visible, "Prepared video preview is hidden");
            Check(session.Live!.Id == live, "Preparing video changed live output");
        });

        Test("Compact docked console keeps both monitors and source controls reachable", () =>
        {
            var width = window.Width; var height = window.Height; var selectedTab = Field<int>(window, "_activeContentSource");
            var previousLive = session.Live!;
            var client = (FrameworkElement)window.Content;
            var preview = (FrameworkElement)window.FindName("PreviewHost");
            var manager = (AvalonDock.DockingManager)window.FindName("WorkspaceDock");
            var originalLayout = (string)Call(window, "SerializeDockLayout")!;
            try
            {
                Call(window, "PrepareCue", new Cue
                {
                    Kind = CueKind.Bible, Title = "Lectura sintética de varias líneas",
                    Body = string.Join("\n", Enumerable.Repeat("Pasaje sintético para comprobar que el resumen sigue visible al reducir la consola.", 8)),
                    Reference = "Lectura sintética 1:1-8", Version = "Versión sintética de prueba de diseño"
                }, null);
                Call(window, "ProjectPrepared");
                window.WindowState = WindowState.Normal; window.Width = Math.Max(width, 1600); window.Height = 900;
                window.Activate(); window.UpdateLayout(); PumpFor(50);
                Call(window, "ApplyWorkspacePreset", "compacta", false);
                Call(window, "SelectContentSource", 0);
                var orderColumn = (FrameworkElement)window.FindName("OrderPanel");
                var contentColumn = (FrameworkElement)window.FindName("SourcesPanel");
                var outputColumn = (FrameworkElement)window.FindName("PreviewPanel");
                var liveColumn = (FrameworkElement)window.FindName("LivePanel");
                var sourcePane = AvalonDock.Layout.Extensions.Descendents(manager.Layout).OfType<AvalonDock.Layout.LayoutAnchorable>().Single(p => p.ContentId == "sources").Parent as AvalonDock.Layout.LayoutAnchorablePane;
                var sourceWidth = contentColumn.ActualWidth;
                sourcePane!.DockWidth = new GridLength(1.2, GridUnitType.Star);
                window.UpdateLayout();
                Check(contentColumn.ActualWidth > sourceWidth, "Dock sizing did not enlarge the source panel");
                foreach (var compactHeight in new[] { 720, 680 })
                {
                    Call(window, "SelectContentSource", 0);
                    window.Width = 1280; window.Height = compactHeight;
                    window.UpdateLayout(); PumpFor(50);
                    var viewport = new Rect(client.RenderSize);
                    CheckWithin(orderColumn, client, viewport, $"Order column at height {compactHeight}");
                    CheckWithin(contentColumn, client, viewport, $"Content column at height {compactHeight}");
                    CheckWithin(outputColumn, client, viewport, $"Preview/live column at height {compactHeight}");
                    Check(BoundsWithin(outputColumn, client).Right <= BoundsWithin(liveColumn, client).Left + 1.5,
                        "Preview and live monitors overlap after resizing");
                    foreach (var name in new[] { "ProjectButton", "BlackoutButton", "FileMenuButton", "LibraryMenuButton", "SettingsMenuButton", "AddOrderButton" })
                    {
                        var button = (Button)window.FindName(name);
                        if (((ScrollViewer)window.FindName("LiveScroll")).IsAncestorOf(button))
                        { button.BringIntoView(); window.UpdateLayout(); }
                        CheckWithin(button, client, viewport, name);
                        var bounds = BoundsWithin(button, client);
                        var hit = client.InputHitTest(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)) as DependencyObject;
                        // Access-key labels can hit a text Run instead of its containing visual.
                        while (hit is FrameworkContentElement content) hit = content.Parent;
                        Check(hit is not null && (ReferenceEquals(hit, button) || button.IsAncestorOf(hit)), name + " is obscured by another control");
                    }
                    var liveBody = (TextBlock)window.FindName("LiveBodyText");
                    var outputStatus = (TextBlock)window.FindName("OutputStatusText");
                    var liveCard = Ancestors(liveBody).OfType<Border>().First(border => border.IsAncestorOf(outputStatus));
                    var liveViewport = BoundsWithin(liveCard, client);
                    foreach (var name in new[] { "LiveReferenceText", "LiveTitleText", "OutputModeText", "NextLiveText" })
                    {
                        var element = (FrameworkElement)window.FindName(name);
                        var scroll = (ScrollViewer)window.FindName("LiveScroll");
                        if (scroll.IsAncestorOf(element))
                        {
                            element.BringIntoView(); window.UpdateLayout();
                            CheckWithin(element, client, BoundsWithin(scroll, client), $"Scrollable {name} at height {compactHeight}");
                        }
                        else CheckWithin(element, client, liveViewport, $"{name} at height {compactHeight}");
                    }
                    Check(liveBody.Visibility == Visibility.Collapsed, "Duplicate live reading excerpt returned below the monitor");
                    for (var index = 0; index < 4; index++)
                    {
                        Call(window, "SelectContentSource", index); window.UpdateLayout(); PumpFor(25);
                        var page = (FrameworkElement)window.FindName(new[] { "SourcesPanel", "BiblePanel", "TextPanel", "YouTubePanel" }[index]);
                        var pageViewport = BoundsWithin(page, client);
                        CheckWithin(page, client, viewport, $"Source {index} page");
                        foreach (var control in Descendants(page).OfType<FrameworkElement>()
                            .Where(element => element.IsVisible && (element is Button or TextBox or ListBox or ComboBox)))
                            CheckWithin(control, client, pageViewport, $"Source {index}: {control.Name} {control.GetType().Name} at height {compactHeight}");
                    }
                    var video = MediaLibrary.FromFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv"));
                    Call(window, "PrepareCue", video, null); Call(window, "ProjectPrepared");
                    window.UpdateLayout(); PumpFor(25);
                    foreach (var name in new[] { "ProjectButton", "BlackoutButton", "PreviewLiveMonitor", "MediaPlaybackPanel", "PauseButton", "VolumeSlider", "LoopCheck", "PositionSlider" })
                        CheckWithin((FrameworkElement)window.FindName(name), client, viewport, $"Media layout: {name} at height {compactHeight}");
                    Check(((FrameworkElement)window.FindName("PreviewViewport")).ActualHeight >= 80, "Media controls squeeze the preview below its minimum");
                    Call(window, "PrepareCue", new Cue { Kind = CueKind.Bible, Title = "Lectura sintética", Body = "Texto sintético que conserva una vista previa legible.", Reference = "Lectura 1:1", Version = "Prueba" }, null);
                    Call(window, "ProjectPrepared");
                }
            }
            finally
            {
                Call(window, "RestoreDockLayout", originalLayout);
                window.Width = width; window.Height = height; Call(window, "SelectContentSource", selectedTab); window.UpdateLayout();
                Call(window, "PrepareCue", previousLive, null); Call(window, "ProjectPrepared");
            }
        });
    }
}
