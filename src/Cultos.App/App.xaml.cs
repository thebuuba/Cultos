using System.Windows;
using System.Windows.Threading;

namespace Cultos.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            await _singleInstance.SignalPrimaryAsync(e.Args.FirstOrDefault());
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) AppLogger.Error("Excepción no controlada de AppDomain", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("Excepción no observada de Task", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        _singleInstance.StartListening(fileToOpen =>
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            if (!window.IsVisible) window.Show();
            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();

            if (!string.IsNullOrWhiteSpace(fileToOpen)) window.OpenCultosFile(fileToOpen);
        });

        window.Show();

        if (e.Args.FirstOrDefault() is { Length: > 0 } startupFile)
            window.OpenCultosFile(startupFile);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Excepción no controlada de interfaz", e.Exception);
        System.Windows.MessageBox.Show(
            "Cultos encontró un error inesperado. El detalle fue guardado en la carpeta de registros de la aplicación.",
            "Cultos",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
