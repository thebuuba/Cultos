using System.Text.Json;

namespace Culto.Core;

public sealed record WindowCaptureOptions
{
    public long WindowHandle { get; set; }
    public int ProcessId { get; set; }
    public long ProcessStartTimeUtcTicks { get; set; }
    public string WindowTitle { get; set; } = "";
    public string WindowClass { get; set; } = "";
    public string Executable { get; set; } = "";
    public bool CaptureCursor { get; set; }
    public bool ControlAudio { get; set; } = true;
    public int CropLeft { get; set; }
    public int CropTop { get; set; }
    public int CropRight { get; set; }
    public int CropBottom { get; set; }
    public WindowCaptureTransform? Transform { get; set; }

    public WindowCaptureOptions Normalize() => this with
    {
        WindowTitle = (WindowTitle ?? "").Trim(), WindowClass = (WindowClass ?? "").Trim(),
        Executable = (Executable ?? "").Trim(),
        CropLeft = Math.Clamp(CropLeft, 0, 8192), CropTop = Math.Clamp(CropTop, 0, 8192),
        CropRight = Math.Clamp(CropRight, 0, 8192), CropBottom = Math.Clamp(CropBottom, 0, 8192),
        Transform = Transform is { } transform && transform.IsValid() ? transform : null
    };

    public void ValidateTarget()
    {
        if (WindowHandle == 0 || ProcessId <= 0 || string.IsNullOrWhiteSpace(WindowClass)
            || string.IsNullOrWhiteSpace(Executable) || WindowClass.Length > 1024 || Executable.Length > 32768
            || (WindowTitle?.Length ?? 0) > 32768)
            throw new InvalidDataException("Selecciona una ventana disponible para capturar.");
        if (CropLeft is < 0 or > 8192 || CropTop is < 0 or > 8192 || CropRight is < 0 or > 8192 || CropBottom is < 0 or > 8192)
            throw new InvalidDataException("El recorte de la captura no es válido.");
        if (Transform is { } transform && !transform.IsValid())
            throw new InvalidDataException("La posición o el tamaño de la captura no es válido.");
    }
}

public sealed record ObsCaptureCommand(WindowCaptureOptions Source)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static byte[] Encode(ObsCaptureCommand command)
    {
        ArgumentNullException.ThrowIfNull(command.Source);
        command.Source.ValidateTarget();
        return JsonSerializer.SerializeToUtf8Bytes(command, JsonOptions);
    }
    public static ObsCaptureCommand Decode(ReadOnlySpan<byte> payload)
    {
        var command = JsonSerializer.Deserialize<ObsCaptureCommand>(payload, JsonOptions)
            ?? throw new InvalidDataException("Falta la ventana de captura.");
        if (command.Source is null) throw new InvalidDataException("Falta la ventana de captura.");
        command.Source.ValidateTarget();
        return command;
    }
}
