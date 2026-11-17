using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Desktop.Services;

internal static partial class Program
{
    private sealed class UpdateHandler(string text) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) });
    }

    private static void RunUpdateTests(MainWindow window)
    {
        Test("Update checks expose a discrete offer without changing projection or opening a dialog", () => {
            var json = JsonSerializer.Serialize(new { version = "99.0.0.0", installerUrl = $"https://github.com/{AppUpdateClient.Repository}/releases/download/v99.0.0.0/CultoStudio-99.0.0.0-Windows-x64-Setup.exe", sha256 = new string('A',64), size = 5 });
            using var http = new HttpClient(new UpdateHandler(json));
            SetField(window, "_updateClient", new AppUpdateClient(http));
            var beforeOutput = Field<object?>(window, "_output");
            var session = Field<PresentationSession>(window, "_session");
            var beforeCue = session.Live;
            var windows = Application.Current.Windows.Count;
            Wait((Task)Call(window, "CheckUpdatesAsync", false)!);
            var offer = (Button)window.FindName("UpdateAvailableButton");
            Check(offer.Visibility == Visibility.Visible && offer.Content.ToString()!.Contains("99.0.0.0"), "Update offer not visible");
            Check(ReferenceEquals(beforeOutput, Field<object?>(window, "_output")) && ReferenceEquals(beforeCue, session.Live), "Update interrupted projection");
            Check(Application.Current.Windows.Count == windows, "Automatic check opened a dialog");
        });
        Test("Update startup preference persists and malformed automatic responses stay silent", () => {
            var menu = (MenuItem)window.FindName("AutomaticUpdatesMenu");
            menu.IsChecked = false;
            Call(window, "ToggleAutomaticUpdatesClick", menu, new RoutedEventArgs());
            Check(!new WorkspaceStore().Load().CheckForUpdatesOnStartup, "Startup preference was not saved");
            using var http = new HttpClient(new UpdateHandler("not json"));
            SetField(window, "_updateClient", new AppUpdateClient(http));
            Wait((Task)Call(window, "CheckUpdatesAsync", false)!);
            Check(((MenuItem)window.FindName("CheckUpdatesMenu")).IsEnabled, "Update action stayed disabled after failure");
        });
        Test("Failed workspace save cancels update installation and keeps the console open", () => {
            SetField(window, "_pendingUpdateInstaller", "not-an-installer.exe");
            SetField(window, "_workspaceRecoveryBlocked", true);
            try {
                var closing = new System.ComponentModel.CancelEventArgs();
                Call(window, "OnWindowClosing", window, closing);
                Check(closing.Cancel && !Field<bool>(window, "_closing"), "Update closed the console despite failed save");
                Check(Field<string?>(window, "_pendingUpdateInstaller") is null, "Unsuccessful update remained scheduled");
            }
            finally { SetField(window, "_workspaceRecoveryBlocked", false); }
        });
    }
}
