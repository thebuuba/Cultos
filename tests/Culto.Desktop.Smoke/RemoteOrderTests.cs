using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;
using Culto.Remote;

internal static partial class Program
{
    private static void RunRemoteOrderTests(MainWindow window)
    {
        var plan = Field<ObservableCollection<Cue>>(window, "_plan");
        var workspace = Field<WorkspaceData>(window, "_workspace");
        var session = Field<PresentationSession>(window, "_session");
        var oldPlan = plan.ToArray();
        var oldName = ((TextBox)window.FindName("PlanNameBox")).Text;
        var first = new Cue { Title = "Bienvenida remota", Body = "Cuerpo privado de prueba", Notes = "Notas privadas de prueba" };
        var second = new Cue { Title = "Lectura remota", Kind = CueKind.Bible, Body = "Lectura de prueba" };
        var third = new Cue { Title = "Despedida remota", Body = "Fin" };
        var libraryOnly = new Cue { Title = "Solo biblioteca", Body = "No pertenece al orden" };
        RemoteSnapshot State() => (RemoteSnapshot)Call(window, "BuildRemoteState")!;
        CommandResult Command(string action, string? cueId = null) =>
            (CommandResult)Call(window, "ExecuteRemote", new RemoteCommand(action, Guid.NewGuid().ToString("N"), CueId: cueId))!;
        void ResetPlan()
        {
            Call(window, "CloseOutput");
            plan.Clear(); plan.Add(first); plan.Add(second); plan.Add(third);
            Call(window, "UpdatePlanCount");
            Call(window, "PrepareCue", first, null);
        }

        try
        {
            workspace.Library.Add(libraryOnly);
            Test("Closing a blacked-out projection publishes closed rather than hidden output", () =>
            {
                ResetPlan(); Call(window, "ProjectPrepared"); Call(window, "ToggleBlackout");
                Check(State().IsBlackout, "Fixture did not enter blackout");
                Call(window, "CloseOutput");
                var closed = State();
                Check(!closed.HasOutput && !closed.IsBlackout && !closed.IsBackground && closed.LiveBody is null,
                    "Closed output retains a misleading blackout or live preview");
            });
            Test("Remote snapshot follows the actual edited order and exposes only cue summaries", () =>
            {
                ResetPlan();
                ((TextBox)window.FindName("PlanNameBox")).Text = "Culto remoto de prueba";
                plan.Move(2, 0);
                Call(window, "PrepareCue", second, null);
                var state = State();
                Check(state.PlanName == "Culto remoto de prueba", "Remote plan title is stale");
                Check(state.Order is not null && state.Order.Select(c => c.Id).SequenceEqual(new[]
                    { third.Id.ToString(), first.Id.ToString(), second.Id.ToString() }), "Remote order is stale or uses the library/deck");
                Check(state.Order![1].Title == "Bienvenida remota" && state.Order[2].Kind == "Bible", "Cue summaries lost title or kind");
                Check(state.PreparedCueId == second.Id.ToString() && state.LiveCueId is null, "Prepared and closed-output markers disagree with the console");
                Check(!state.HasOutput && !state.HasExternalOutput && !state.IsBackground, "Closed output is reported as active");
                var payload = JsonSerializer.Serialize(state.Order);
                Check(!payload.Contains("Cuerpo privado") && !payload.Contains("Notas privadas") && !payload.Contains("FilePath"), "Order payload leaks cue contents or local file paths");
                plan.Remove(first);
                Check(State().Order!.Select(c => c.Id).SequenceEqual(new[] { third.Id.ToString(), second.Id.ToString() }), "Remote snapshot retained a removed cue");
            });

            Test("Remote cue activation projects the chosen plan item without HDMI", () =>
            {
                ResetPlan();
                var result = Command("cue", second.Id.ToString());
                Check(result.Success && session.Live?.Id == second.Id && session.Prepared?.Id == second.Id, "Remote cue did not prepare and project the chosen item");
                Check(Field<ProjectionSurface?>(window, "_output") is not null && Field<ProjectionWindow?>(window, "_outputWindow") is null,
                    "Remote activation requires HDMI or opened an external window");
                var state = State();
                Check(state.HasOutput && !state.HasExternalOutput && state.LiveCueId == second.Id.ToString()
                    && state.PreparedCueId == second.Id.ToString() && !state.IsBackground && !state.IsBlackout,
                    "Remote live markers do not match the local projection");
            });

            Test("Remote next and previous follow reordered plan boundaries", () =>
            {
                ResetPlan(); plan.Move(2, 1); // first, third, second
                Check(Command("cue", third.Id.ToString()).Success, "Could not activate middle cue");
                Check(Command("next").Success && session.Live?.Id == second.Id, "Next ignored the edited order");
                var live = session.Live;
                Check(!Command("next").Success && ReferenceEquals(session.Live, live), "Next at the end changed output or reported success");
                Check(Command("previous").Success && session.Live?.Id == third.Id, "Previous ignored the edited order");
                Check(Command("previous").Success && session.Live?.Id == first.Id, "Previous missed the first item");
                live = session.Live;
                Check(!Command("previous").Success && ReferenceEquals(session.Live, live), "Previous at the start changed output or reported success");
            });

            Test("Remote invalid deleted and library-only cue IDs preserve live and prepared output", () =>
            {
                ResetPlan();
                Check(Command("cue", second.Id.ToString()).Success, "Could not activate fixture");
                plan.Remove(third);
                foreach (var id in new[] { (string?)null, "not-a-guid", Guid.NewGuid().ToString(), third.Id.ToString(), libraryOnly.Id.ToString() })
                {
                    var live = session.Live; var prepared = session.Prepared;
                    var output = Field<ProjectionSurface>(window, "_output");
                    Check(!Command("cue", id).Success, "Invalid or unavailable order cue was accepted");
                    Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared)
                        && ReferenceEquals(output, Field<ProjectionSurface>(window, "_output")), "Rejected cue changed live or prepared output");
                }
            });

            Test("Remote unavailable media fails without replacing current projection", () =>
            {
                ResetPlan();
                Check(Command("cue", first.Id.ToString()).Success, "Could not activate fixture");
                var missing = new Cue { Title = "Archivo ausente", Kind = CueKind.Video,
                    FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.mp4") };
                plan.Add(missing);
                var live = session.Live; var prepared = session.Prepared;
                Check(!Command("cue", missing.Id.ToString()).Success, "Missing media was reported as projected");
                Check(ReferenceEquals(live, session.Live) && ReferenceEquals(prepared, session.Prepared), "Missing media changed output or preview");
            });

            Test("Existing deck cue buttons still project library content outside the order", () =>
            {
                ResetPlan();
                var oldDeck = workspace.Deck;
                try
                {
                    workspace.Deck = [new DeckButton("remote-library-test", "Biblioteca", "#43D9BE", "cue", libraryOnly.Id.ToString())];
                    var command = new RemoteCommand("deck", Guid.NewGuid().ToString("N"), ButtonId: "remote-library-test");
                    var result = (CommandResult)Call(window, "ExecuteRemote", command)!;
                    Check(result.Success && session.Live?.Id == libraryOnly.Id, "Order-only validation broke an existing deck button");
                    Check(State().Order!.All(cue => cue.Id != libraryOnly.Id.ToString()), "Deck activation inserted library content into the order");
                }
                finally { workspace.Deck = oldDeck; }
            });

            Test("Remote background blackout and closed-output states match the console", () =>
            {
                var oldImages = workspace.BackgroundImages; var oldImage = workspace.BackgroundImagePath;
                var image = Path.Combine(Field<WorkspaceStore>(window, "_store").DirectoryPath, "remote-logo.png");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 30, 80, 40, 255 }, 4)));
                using (var stream = File.Create(image)) encoder.Save(stream);
                workspace.BackgroundImages = [image]; workspace.BackgroundImagePath = image;
                try
                {
                ResetPlan();
                Check(Command("cue", first.Id.ToString()).Success, "Could not activate fixture");
                Check(Command("background").Success, "Background command failed");
                var state = State();
                Check(state.HasOutput && state.IsBackground && !state.IsBlackout && state.LiveCueId == first.Id.ToString(), "Background state lost active cue or output status");
                Check(Command("blackout").Success && State().IsBlackout, "Blackout was not reflected in snapshot");
                Check(Command("cue", second.Id.ToString()).Success && !State().IsBackground && !State().IsBlackout, "New cue did not clear background and blackout flags");
                Call(window, "CloseOutput");
                state = State();
                Check(!state.HasOutput && !state.HasExternalOutput && state.LiveCueId is null && !state.IsBackground, "Closed output retained a live marker");
                }
                finally { workspace.BackgroundImages = oldImages; workspace.BackgroundImagePath = oldImage; }
            });

            Test("Authenticated HTTP commands dispatch to local video playback and publish native state", () =>
            {
                ResetPlan();
                var video = new Cue { Title = "Video remoto de prueba", Kind = CueKind.Video,
                    FilePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "test.mkv") };
                plan.Insert(1, video);
                var volume = (Slider)window.FindName("VolumeSlider");
                var oldVolume = volume.Value;
                var host = new RemoteHost();
                T Receive<T>(Task<T> task) { Wait(task); return task.GetAwaiter().GetResult(); }
                try
                {
                    volume.Value = 0;
                    Wait(host.StartAsync(0,
                        () => (RemoteSnapshot)Call(window, "GetRemoteState")!,
                        command => (Task<CommandResult>)Call(window, "HandleRemote", command)!, loopbackOnly: true));
                    using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{host.Port}"), Timeout = TimeSpan.FromSeconds(10) };
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.Token);
                    void Post(string action, string? id = null)
                    {
                        using var response = Receive(client.PostAsJsonAsync("/api/command",
                            new RemoteCommand(action, Guid.NewGuid().ToString("N"), CueId: id)));
                        Check(response.IsSuccessStatusCode, $"HTTP {action} failed with {response.StatusCode}");
                        Check(Receive(response.Content.ReadFromJsonAsync<CommandResult>())?.Success == true, $"Native {action} reported failure");
                    }
                    RemoteSnapshot ReadState() => Receive(client.GetFromJsonAsync<RemoteSnapshot>("/api/state"))!;

                    Post("cue", video.Id.ToString());
                    Check(session.Live?.Id == video.Id, "Authenticated cue did not reach the native session");
                    var output = Field<ProjectionSurface>(window, "_output");
                    Until(() => output.IsPlaying && output.Duration > 0 && !output.IsTransitioning,
                        "HTTP cue did not start native video playback and finish the dissolve");
                    var state = ReadState();
                    Check(state.HasOutput && !state.HasExternalOutput && state.LiveCueId == video.Id.ToString()
                        && state.PreparedCueId == video.Id.ToString() && state.Duration > 0,
                        "HTTP state does not report the playing local video");
                    Post("pause");
                    Until(() => !output.IsPlaying, "HTTP pause did not pause the native video");
                    Check(ReadState().IsPaused, "HTTP state did not report native pause");
                    Post("next");
                    Check(session.Live?.Id == second.Id && ReadState().LiveCueId == second.Id.ToString(),
                        "HTTP next did not advance from video to the following actual plan item");
                }
                finally
                {
                    Wait(host.DisposeAsync().AsTask());
                    Call(window, "CloseOutput");
                    volume.Value = oldVolume;
                }
            });
        }
        finally
        {
            Call(window, "CloseOutput");
            workspace.Library.Remove(libraryOnly);
            plan.Clear(); foreach (var cue in oldPlan) plan.Add(cue);
            ((TextBox)window.FindName("PlanNameBox")).Text = oldName;
            Call(window, "UpdatePlanCount");
            if (plan.Count > 0) ((ListBox)window.FindName("PlanList")).SelectedIndex = 0;
        }
    }
}
