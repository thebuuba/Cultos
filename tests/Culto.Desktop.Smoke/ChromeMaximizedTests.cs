using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static void RunChromeMaximizedTests(MainWindow window)
    {
        var folder=Path.GetFullPath(Path.Combine(".tools","chrome-probe-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        var page=Path.Combine(folder,"probe.html");
        File.WriteAllText(page,"""
            <!doctype html><title>Culto Chrome probe - YouTube</title>
            <body style="margin:0;overflow:hidden;background:black"><video id="v" autoplay muted playsinline
            style="width:100vw;height:100vh;object-fit:fill"></video>
            <script>
            const canvas=document.createElement('canvas');canvas.width=640;canvas.height=360;
            const context=canvas.getContext('2d');let tick=0;
            function paint(){context.fillStyle=(tick++%2)?'red':'lime';context.fillRect(0,0,640,360);}
            paint();v.srcObject=canvas.captureStream(30);v.play();
            setInterval(()=>{paint();document.title="Culto Chrome probe - YouTube "+v.readyState+" "+v.currentTime+" "+(v.error?.message||"ok");},500);
            </script>
            """);
        var start=new ProcessStartInfo(@"C:\Program Files\Google\Chrome\Application\chrome.exe")
            {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Normal};
        foreach(var arg in new[]{"--user-data-dir="+Path.Combine(folder,"profile"),"--no-first-run","--no-default-browser-check",
            "--autoplay-policy=no-user-gesture-required","--allow-file-access-from-files","--window-position=30,30","--window-size=600,500","--app="+new Uri(page).AbsoluteUri})start.ArgumentList.Add(arg);
        window.Topmost=false;window.Left=700;window.Top=60;window.Width=1100;window.Height=760;
        using var chrome=Process.Start(start)!;
        var intervening=new Window {Title="Owned opaque window between Chrome and console",Left=0,Top=0,Width=700,Height=600,
            ShowInTaskbar=false,Background=System.Windows.Media.Brushes.Black};
        try
        {
            Test("Real Chrome capture keeps advancing under a maximized console",()=>
            {
                Until(()=>WindowCaptureCatalog.List().Any(t=>t.Options.WindowTitle.Contains("Culto Chrome probe")),"Owned Chrome window did not open");
                window.Topmost=false;
                var loading=WindowCaptureCatalog.List().Single(t=>t.Options.WindowTitle.Contains("Culto Chrome probe"));
                ChromeProbeSetWindowPos((nint)loading.Options.WindowHandle, (nint)(-1), 0, 0, 0, 0, 0x13);
                ChromeProbeSetForegroundWindow((nint)loading.Options.WindowHandle);
                Until(()=>WindowCaptureCatalog.List().Any(t=>t.Options.WindowTitle.Contains("YouTube 4 ")),"Visible Chrome video did not load");
                ChromeProbeSetWindowPos((nint)loading.Options.WindowHandle, (nint)(-2), 0, 0, 0, 0, 0x13);
                var target=WindowCaptureCatalog.List().Single(t=>t.Options.WindowTitle.Contains("Culto Chrome probe"));
                var cue=new Cue {Kind=CueKind.WindowCapture,Title=target.Options.WindowTitle,
                    Capture=target.Options with {YouTubeOnly=true,ControlAudio=false}};
                window.Show();window.WindowState=WindowState.Maximized;window.Activate();PumpFor(2500);
                Call(window,"PrepareCue",cue,null);Call(window,"ProjectPrepared");
                var output=Field<ProjectionSurface>(window,"_output");
                int Pixel()
                {
                    var bitmap=Field<BitmapSource?>(output,"_captureBitmap");if(bitmap is null)return -1;
                    var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth/2,bitmap.PixelHeight/2,1,1),pixel,4,0);
                    return pixel[0]|pixel[1]<<8|pixel[2]<<16;
                }
                void Changing(string stage)
                {
                    var first=Pixel();
                    Until(()=>Pixel()>0 && Pixel()!=first,stage+" stopped updating, pixel="+Pixel().ToString("X"));
                }
                Until(()=>!output.IsTransitioning && Pixel()>0,"Chrome did not deliver source pixels");
                Changing("Initial maximized console");
                window.WindowState=WindowState.Normal;PumpFor(800);
                var client=Field<object>(output,"_captureClient");
                var refreshCount=Field<int>(client,"_refreshCount");
                window.Topmost=true;window.WindowState=WindowState.Maximized;window.Activate();PumpFor(2500);
                Until(()=>Field<int>(client,"_refreshCount")>refreshCount,"Maximizing did not recreate the native capture session");
                Check(ReferenceEquals(client,Field<object>(output,"_captureClient")),"Resize replaced the capture worker");
                Changing("Maximized console");
                CaptureDesignWindow(window,Path.Combine(Environment.CurrentDirectory,".tools","chrome-maximized.png"));
                window.WindowState=WindowState.Normal;PumpFor(800);Changing("Restored console");
                window.WindowState=WindowState.Maximized;PumpFor(1500);Changing("Maximized again");
                window.Topmost=false;
                intervening.Show();intervening.WindowState=WindowState.Maximized;
                intervening.Topmost=true;intervening.Activate();PumpFor(2000);
                Check(!window.IsActive,"Owned other application did not take focus");
                client=Field<object>(output,"_captureClient");
                refreshCount=Field<int>(client,"_refreshCount");
                intervening.Topmost=false;window.Activate();PumpFor(1000);
                Check(window.IsActive && window.WindowState==WindowState.Maximized,"Console did not return maximized");
                Until(()=>Field<int>(client,"_refreshCount")>refreshCount,"Returning from another application did not renew the native capture session");
                Check(ReferenceEquals(client,Field<object>(output,"_captureClient")),"Returning replaced the capture worker");
                Changing("Returning from another application");
                Check(window.IsActive,"Capture recovery stole focus from the console");
                Check((BrowserGetWindowLong((nint)target.Options.WindowHandle,-20)&8)==0,"Capture recovery made Chrome topmost");
            });
        }
        finally
        {
            Console.WriteLine("CHROME "+string.Join(" | ",WindowCaptureCatalog.List().Where(t=>t.Options.WindowTitle.Contains("Culto Chrome probe")).Select(t=>t.Options.WindowTitle)));
            intervening.Close();Call(window,"CloseOutput");window.WindowState=WindowState.Normal;
            if(!chrome.HasExited)chrome.Kill(entireProcessTree:true);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint="SetForegroundWindow")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ChromeProbeSetForegroundWindow(nint window);
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint="SetWindowPos")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ChromeProbeSetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
