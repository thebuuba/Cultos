using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Handler;
using CefSharp.Wpf;
using Culto.Core;
using Culto.Desktop.Services;

namespace Culto.Desktop.Projection;

public sealed class BrowserSourceView : Viewbox, IDisposable
{
    private ChromiumWebBrowser? _browser;
    private BrowserSourceOptions? _options;
    private bool _muted = true, _disposed;
    private volatile bool _cssReady;
    private readonly DispatcherTimer _timeout;
    public bool IsReady { get; private set; }
    public event Action? FrameReady;
    public event Action<string>? Error;

    public BrowserSourceView()
    {
        Stretch = Stretch.Uniform;
        IsHitTestVisible = false;
        _timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timeout.Tick += (_, _) => Fail("La fuente navegador no entregó imagen a tiempo.");
    }

    public void SetSource(BrowserSourceOptions options, bool muted)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        options = options.Normalize(); options.Validate();
        if (_browser is not null && _options == options) { SetMuted(muted); return; }
        CloseBrowser(); _options = options; _muted = muted;
        try
        {
            BrowserSourceRuntime.EnsureInitialized();
            var browser = new ChromiumWebBrowser
            {
                Width = options.Width, Height = options.Height,
                Background = Brushes.Transparent, Focusable = false,
                BrowserSettings = new BrowserSettings { BackgroundColor = 0, WindowlessFrameRate = options.FramesPerSecond },
                LifeSpanHandler = new OutputLifeSpanHandler(), JsDialogHandler = new OutputDialogHandler(),
                DownloadHandler = new OutputDownloadHandler(), RequestHandler = new OutputRequestHandler(Fail),
                Address = options.GetAddress().ToString()
            };
            _browser = browser;
            browser.IsBrowserInitializedChanged += (_, _) => { if (!_disposed && ReferenceEquals(browser, _browser)) SetMuted(_muted); };
            browser.FrameLoadStart += (_, args) => { if (args.Frame.IsMain) _cssReady = false; };
            browser.FrameLoadEnd += async (_, args) =>
            {
                if (!args.Frame.IsMain || _disposed || !ReferenceEquals(browser, _browser)) return;
                if (args.HttpStatusCode >= 400) { Fail("La página devolvió HTTP " + args.HttpStatusCode + "."); return; }
                try
                {
                    var css = JsonSerializer.Serialize(options.CustomCss);
                    var result = await args.Frame.EvaluateScriptAsync("(()=>{let s=document.getElementById('culto-source-css');if(!s){s=document.createElement('style');s.id='culto-source-css';(document.head||document.documentElement).appendChild(s)}s.textContent=" + css + ";return true})()");
                    if (!result.Success) { Fail("No se pudo aplicar el CSS de la fuente. " + result.Message); return; }
                    _cssReady = true;
                    browser.GetBrowser().GetHost().Invalidate(PaintElementType.View);
                }
                catch (Exception ex) { if (!_disposed) Fail(ex.Message); }
            };
            browser.LoadError += (_, args) => { if (!_disposed && ReferenceEquals(browser, _browser) && args.Frame.IsMain && args.ErrorCode != CefErrorCode.Aborted) Fail(args.ErrorText); };
            browser.Paint += (_, args) =>
            {
                if (args.IsPopup || !_cssReady || _disposed) return;
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    if (_disposed || !ReferenceEquals(browser, _browser) || !_cssReady || IsReady) return;
                    IsReady = true; _timeout.Stop(); SetMuted(_muted); FrameReady?.Invoke();
                }));
            };
            Child = browser;
            _timeout.Start();
        }
        catch (Exception ex) { CloseBrowser(); Fail(ex.Message); }
    }

    public void SetMuted(bool muted)
    {
        _muted = muted;
        if (_browser is { IsBrowserInitialized: true } browser && !browser.IsDisposed)
            browser.GetBrowser().GetHost().SetAudioMuted(muted || !IsReady || _options?.AudioEnabled != true);
    }
    public void Reload()
    {
        if (_disposed || _browser is null) return;
        IsReady = false; _cssReady = false; SetMuted(_muted); _timeout.Start();
        _browser.Reload(ignoreCache: true);
    }
    private void Fail(string message)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_disposed) return;
            _timeout.Stop(); IsReady = false;
            SetMuted(_muted); Error?.Invoke("Navegador: " + message);
        }));
    }
    private void CloseBrowser()
    {
        _timeout.Stop(); IsReady = false; _cssReady = false;
        var browser = _browser; _browser = null; Child = null;
        if (browser is not null) { if (browser.IsBrowserInitialized) browser.GetBrowser().GetHost().SetAudioMuted(true); browser.Dispose(); }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; CloseBrowser(); FrameReady = null; Error = null; }

    private sealed class OutputDownloadHandler : DownloadHandler
    { protected override bool CanDownload(IWebBrowser control, IBrowser browser, string url, string requestMethod) => false; }
    private sealed class OutputDialogHandler : JsDialogHandler
    {
        protected override bool OnJSDialog(IWebBrowser control, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        { suppressMessage = true; return false; }
        protected override bool OnBeforeUnloadDialog(IWebBrowser control, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        { callback.Continue(true); return true; }
    }
    private sealed class OutputLifeSpanHandler : LifeSpanHandler
    {
        protected override bool OnBeforePopup(IWebBrowser control, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        { newBrowser = null!; return true; }
    }
    private sealed class OutputRequestHandler(Action<string> fail) : RequestHandler
    {
        protected override void OnRenderProcessTerminated(IWebBrowser control, IBrowser browser, CefTerminationStatus status, int errorCode, string errorMessage)
            => fail("El proceso de renderizado se cerró. " + errorMessage);
    }
}
