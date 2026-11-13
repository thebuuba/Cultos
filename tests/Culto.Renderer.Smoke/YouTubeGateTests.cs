using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Culto.Core;
using Culto.Desktop.Projection;
using Culto.Desktop.Services;

internal static partial class Program
{
    private static async Task RunYouTubeGateChecks()
    {
        var start=new ProcessStartInfo(Path.GetFullPath(".tools/dotnet/dotnet.exe"))
            {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Environment.CurrentDirectory};
        start.Environment["CULTO_CAPTURE_SOURCE_SILENT"]="1";
        start.Environment["CULTO_CAPTURE_SOURCE_TITLE"]="Fixture - YouTube";
        start.ArgumentList.Add(typeof(Program).Assembly.Location);start.ArgumentList.Add("--capture-source");
        using var source=Process.Start(start)!;
        using var surface=new ProjectionSurface {Volume=60};
        var host=new Window {Title="Owned YouTube gate test",Content=surface,Width=480,Height=300,Left=600,Top=40};
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object? Field(object instance,string name)=>instance.GetType().GetField(name,flags)!.GetValue(instance);
        int Pixel()
        {
            if(Field(surface,"_captureBitmap") is not BitmapSource bitmap)return -1;
            var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth/2,bitmap.PixelHeight/2,1,1),pixel,4,0);
            return pixel[0]|pixel[1]<<8|pixel[2]<<16;
        }
        async Task Until(Func<bool> condition,string message)
        {
            var watch=Stopwatch.StartNew();while(!condition() && watch.ElapsedMilliseconds<18000)await Task.Delay(20);
            Check(condition(),message);
        }
        try
        {
            host.Show();
            await Until(()=>WindowCaptureCatalog.List().Any(t=>t.Options.ProcessId==source.Id),"Owned source is discovered");
            var selected=WindowCaptureCatalog.List().Single(t=>t.Options.ProcessId==source.Id).Options;
            var restriction=typeof(WindowCaptureOptions).GetProperty("YouTubeOnly");
            Check(restriction is not null,"Capture supports a persistent YouTube-only restriction");
            restriction!.SetValue(selected,true);
            surface.ShowCue(new Cue {Kind=CueKind.WindowCapture,Capture=selected});
            await Until(()=>Pixel() is 0x0000FF or 0x00FF00,"YouTube source delivers animated pixels");
            var client=Field(surface,"_captureClient")!;
            var worker=(Process)Field(client,"_worker")!;
            var lease=(CapturedApplicationAudio.Lease)Field(surface,"_captureAudio")!;
            surface.RefreshWindowCapture();
            await Until(()=>(int)Field(client,"_refreshCount")!>0,"Native capture acknowledges a refreshed session");
            Check(ReferenceEquals(lease,Field(surface,"_captureAudio")) && !worker.HasExited && lease.Gain>0,
                "Refreshing capture preserves the worker and active audio lease");
            GateSetTitle((nint)selected.WindowHandle,"WhatsApp - Google Chrome");
            await Until(()=>Pixel()==0 && lease.Gain==0,"Leaving YouTube blacks picture and silences capture audio");
            await Task.Delay(16000);
            Check(Pixel()==0 && lease.Gain==0 && !worker.HasExited,"Non-YouTube tab stays blocked without timing out");
            GateSetTitle((nint)selected.WindowHandle,"Another song - YouTube - Google Chrome");
            await Until(()=>Pixel() is 0x0000FF or 0x00FF00 && lease.Gain>0,"Returning to YouTube resumes picture and audio");
            Check(ReferenceEquals(client,Field(surface,"_captureClient")) && !worker.HasExited,"Tab change resumes the same capture without a picker");
        }
        finally
        {
            surface.Dispose();host.Close();
            if(!source.HasExited)source.Kill();
        }
    }
    [DllImport("user32.dll",EntryPoint="SetWindowTextW",CharSet=CharSet.Unicode)]
    private static extern bool GateSetTitle(nint window,string title);
}
