using System.Windows;
using System.Windows.Controls;
using Culto.Desktop;
using Culto.Desktop.Dialogs;
using Culto.Remote;

internal static partial class Program
{
    private static void RunRemoteDialogTests(MainWindow window)
    {
        Test("Remote launcher is circular and remote window supports native opening motion", () =>
        {
            var button = (Button)window.FindName("RemoteTopButton");
            Check(button.Width == 44 && button.Height == 44, "Remote launcher is not a compact circle");
            Check(window.FindName("RemoteConnectionText") is null, "Remote launcher still shows a text badge");
            var dialog = new RemoteDialog(null, [], []) { Owner = window };
            try
            {
                Check(!dialog.AllowsTransparency && dialog.WindowStyle == WindowStyle.SingleBorderWindow,
                    "Remote window does not use the player's native window surface");
            }
            finally { dialog.Close(); }
        });
        Test("Remote activity pulse survives header refresh and stops on deactivation", () =>
        {
            var pulse = (System.Windows.Shapes.Ellipse)window.FindName("RemotePulse");
            var scale = (System.Windows.Media.ScaleTransform)pulse.RenderTransform;
            var host = new RemoteHost();
            try
            {
                SetField(window, "_remote", host);
                Call(window, "UpdateWorkspaceHeader");
                Call(window, "UpdateWorkspaceHeader");
                Check(scale.HasAnimatedProperties == SystemParameters.ClientAreaAnimation,
                    "Active remote does not respect system animation preference");
                SetField(window, "_remote", null);
                Call(window, "UpdateWorkspaceHeader");
                Check(!scale.HasAnimatedProperties && pulse.Opacity == 0, "Inactive remote retained its pulse");
            }
            finally
            {
                SetField(window, "_remote", null);
                Call(window, "UpdateWorkspaceHeader");
                Wait(host.DisposeAsync().AsTask());
            }
        });
        Test("Refreshing remote connection while starting or stopping cannot duplicate operations", () =>
        {
            var host = new RemoteHost();
            var dialog = new RemoteDialog(null, [], []) { Owner = window };
            var startCompletion = new TaskCompletionSource<RemoteHost>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = 0; var stops = 0;
            dialog.StartRequested = () => { starts++; return startCompletion.Task; };
            dialog.StopRequested = () => { stops++; return stopCompletion.Task; };
            var start = Field<Button>(dialog, "_start");
            var stop = Field<Button>(dialog, "_stop");
            var copy = Field<Button>(dialog, "_copy");
            var addresses = Field<ComboBox>(dialog, "_addresses");
            var qr = Field<Image>(dialog, "_qr");
            var emptyQr = Field<TextBlock>(dialog, "_qrEmpty");
            var url = Field<TextBox>(dialog, "_url");
            Task? starting = null; Task? stopping = null;
            try
            {
                starting = (Task)Call(dialog, "Start")!;
                Check(starts == 1 && !starting.IsCompleted, "Start did not await the requested connection");
                // A deterministic address keeps QR assertions independent of the machine's network.
                // Changing selection also exercises the same refresh callback as the network selector.
                addresses.ItemsSource = new[] { "192.0.2.10" };
                addresses.SelectedIndex = 0;
                Call(dialog, "RefreshConnection");
                Check(!start.IsEnabled && !stop.IsEnabled && !copy.IsEnabled && !addresses.IsEnabled,
                    "Refreshing while activation is pending reenabled connection controls");
                Wait((Task)Call(dialog, "Start")!);
                Wait((Task)Call(dialog, "Stop")!);
                Check(starts == 1 && stops == 0, "Pending activation allowed a duplicate start or a competing stop");

                startCompletion.SetResult(host);
                Wait(starting);
                Check(!start.IsEnabled && stop.IsEnabled && copy.IsEnabled && addresses.IsEnabled,
                    "Completed activation did not restore the active connection controls");
                Check(qr.Source is not null && qr.Visibility == Visibility.Visible && emptyQr.Visibility == Visibility.Collapsed,
                    "Completed activation did not generate and reveal the QR");
                Check(url.Text == $"http://192.0.2.10:{host.Port}/#token={host.Token}", "Connection link does not match the active host and selected address");

                stopping = (Task)Call(dialog, "Stop")!;
                Check(stops == 1 && !stopping.IsCompleted, "Stop did not await the requested shutdown");
                Call(dialog, "RefreshConnection");
                Check(!start.IsEnabled && !stop.IsEnabled && !copy.IsEnabled && !addresses.IsEnabled,
                    "Refreshing while shutdown is pending reenabled connection controls");
                Wait((Task)Call(dialog, "Stop")!);
                Wait((Task)Call(dialog, "Start")!);
                Check(starts == 1 && stops == 1, "Pending shutdown allowed duplicate stop or competing start");

                stopCompletion.SetResult();
                Wait(stopping);
                Check(start.IsEnabled && !stop.IsEnabled && !copy.IsEnabled && !addresses.IsEnabled,
                    "Completed shutdown retained active connection controls");
                Check(qr.Source is null && qr.Visibility == Visibility.Collapsed && emptyQr.Visibility == Visibility.Visible,
                    "Completed shutdown retained the connection QR");
                Check(!url.Text.Contains(host.Token, StringComparison.Ordinal) && !url.Text.StartsWith("http", StringComparison.Ordinal),
                    "Completed shutdown retained the old pairing link");
            }
            finally
            {
                startCompletion.TrySetResult(host); stopCompletion.TrySetResult();
                if (starting is not null) Wait(starting);
                if (stopping is not null) Wait(stopping);
                dialog.Close();
                Wait(host.DisposeAsync().AsTask());
            }
        });
    }
}
