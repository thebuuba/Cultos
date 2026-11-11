using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static class Program
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int failures;
    private static int closePromptsObserved;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object? Call(object instance, string method, params object?[] args) => instance.GetType().GetMethod(method, Private)!.Invoke(instance, args);
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private)!.GetValue(instance)!;
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
    private static int Main()
    {
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

            var app = new App();
            app.InitializeComponent();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow();
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                window.Show();
                app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try { RunWindowTests(window, directory); }
                    finally
                    {
                        if (!Field<bool>(window, "_closing"))
                        {
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
}
