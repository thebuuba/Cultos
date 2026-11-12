using System.Text;
using Culto.Core;
using Xunit;

public sealed class WindowCaptureOptionsTests
{
    private static WindowCaptureOptions Source() => new() { WindowHandle = 123456789, ProcessId = 123,
        WindowTitle = "Video #1: culto — YouTube", WindowClass = "Chrome_WidgetWin_1", Executable = "chrome.exe",
        CropTop = 110, CropBottom = 40, ControlAudio = true };

    [Fact]
    public void ProtocolPreservesExactWindowIdentityAndCaptureChoices()
    {
        var expected = new ObsCaptureCommand(Source());
        Assert.Equal(expected, ObsCaptureCommand.Decode(ObsCaptureCommand.Encode(expected)));
    }

    [Theory]
    [InlineData(0, 123)]
    [InlineData(123, 0)]
    [InlineData(123, -1)]
    public void MissingNativeIdentityCannotStartACapture(long handle, int process)
    {
        Assert.Throws<InvalidDataException>(() => ObsCaptureCommand.Encode(new(Source() with { WindowHandle = handle, ProcessId = process })));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"source\":null}")]
    [InlineData("{\"source\":{\"windowHandle\":123,\"processId\":1,\"windowClass\":null,\"executable\":\"chrome.exe\"}}")]
    public void CorruptCommandsDoNotFallBackToAnotherWindow(string json)
    {
        Assert.Throws<InvalidDataException>(() => ObsCaptureCommand.Decode(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void NormalizationBoundsCropWithoutChangingWindowOrAudioIntent()
    {
        var source = Source() with { CropLeft = -20, CropTop = 9000, CaptureCursor = true, ControlAudio = false };
        var normalized = source.Normalize();
        Assert.Equal(0, normalized.CropLeft);
        Assert.Equal(8192, normalized.CropTop);
        Assert.Equal(source.WindowHandle, normalized.WindowHandle);
        Assert.Equal(source.ProcessId, normalized.ProcessId);
        Assert.True(normalized.CaptureCursor);
        Assert.False(normalized.ControlAudio);
        Assert.Throws<InvalidDataException>(() => source.ValidateTarget());
    }
}
