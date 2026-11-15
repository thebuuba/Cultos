using System.IO;
using System.Windows;
using System.Windows.Controls;
using Culto.Desktop;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunChurchSetupTests(MainWindow window)
    {
        var workspace = Field<WorkspaceData>(window, "_workspace");
        Test("Church setup saves its identity without changing the order or library", () =>
        {
            var order = workspace.Plan.Items.Select(c => c.Id).ToArray(); var library = workspace.Library.Select(c => c.Id).ToArray();
            Check((bool)Call(window, "ApplyChurchSetup", "  Iglesia de prueba  ")!, "Church setup was not saved");
            var loaded = new WorkspaceStore().Load();
            Check((string)typeof(WorkspaceData).GetProperty("ChurchName")!.GetValue(loaded)! == "Iglesia de prueba", "Church name did not persist trimmed");
            Check((bool)typeof(WorkspaceData).GetProperty("FirstRunCompleted")!.GetValue(loaded)!, "Completed first run was not saved");
            Check(window.Title.Contains("Iglesia de prueba"), "Church identity is absent from the window title");
            Check(order.SequenceEqual(workspace.Plan.Items.Select(c => c.Id)) && library.SequenceEqual(workspace.Library.Select(c => c.Id)), "Church setup replaced existing content");
        });
        Test("Church setup reverts its identity when workspace saving is blocked", () =>
        {
            var before = (string)typeof(WorkspaceData).GetProperty("ChurchName")!.GetValue(workspace)!;
            var blocked = Field<bool>(window, "_workspaceRecoveryBlocked");
            try
            {
                SetField(window, "_workspaceRecoveryBlocked", true);
                Check(!(bool)Call(window, "ApplyChurchSetup", "Unpersisted church")!, "Blocked save reports success");
                Check((string)typeof(WorkspaceData).GetProperty("ChurchName")!.GetValue(workspace)! == before, "Failed save retained an unpersisted church identity");
            }
            finally { SetField(window, "_workspaceRecoveryBlocked", blocked); }
        });
        Test("First-run screen is readable and can be reopened from configuration", () =>
        {
            var type = typeof(MainWindow).Assembly.GetType("Culto.Desktop.Dialogs.FirstRunDialog")!;
            var dialog = (Window)Activator.CreateInstance(type, new object[] { "Iglesia de prueba", true })!;
            dialog.Owner = window;
            try
            {
                dialog.Show(); dialog.UpdateLayout();
                Check(Descendants(dialog).OfType<TextBox>().Any(t => t.Name == "ChurchName" && t.Text == "Iglesia de prueba"), "Church field was not initialized");
                Check(Descendants(dialog).OfType<Button>().Any(b => b.Name == "CompleteChurchSetup"), "First run has no start action");
                Check(window.FindName("ChurchSettingsMenu") is MenuItem { Header: "Datos de la iglesia…" }, "Configuration cannot reopen church setup");
                CaptureDesignWindow(dialog, Path.Combine(Environment.CurrentDirectory, ".tools", "church-first-run.png"));
            }
            finally { dialog.Close(); }
        });
    }
}
