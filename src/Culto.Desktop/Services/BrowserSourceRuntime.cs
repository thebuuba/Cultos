using System;
using CefSharp;
using CefSharp.Wpf;

namespace Culto.Desktop.Services;

public static class BrowserSourceRuntime
{
    private static bool _initialized;
    private static bool _shutdown;
    public static void EnsureInitialized()
    {
        if (_shutdown) throw new InvalidOperationException("El motor navegador ya está cerrado.");
        if (_initialized) return;
        var settings = new CefSettings { BackgroundColor = 0, WindowlessRenderingEnabled = true };
        // Application owns shutdown after disposing every output and preview view.
        CefSharpSettings.ShutdownOnExit = false;
        if (!Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
            throw new InvalidOperationException("No se pudo inicializar Chromium.");
        _initialized = true;
    }
    public static void Shutdown()
    {
        if (!_initialized || _shutdown) return;
        _shutdown = true;
        Cef.Shutdown();
    }
}
