using System.Windows;
using System.Windows.Controls;
using Culto.Desktop;

internal static partial class Program
{
    private static void RunBrowserSourcesTests(MainWindow window)
    {
        Test("Browser sources are reachable from scenes and the worship order", () =>
        {
            Check(window.FindName("SceneBrowserButton") is Button, "Scenes has no Browser source button");
            Check(window.FindName("AddOrderBrowser") is MenuItem, "Worship order cannot add a Browser source");
            Check(typeof(Culto.Desktop.Services.WorkspaceData).GetProperty("BrowserSources") is not null,
                "Browser sources cannot be saved between sessions");
        });
    }
}
