# Culto Studio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Construir una primera aplicación Windows ejecutable que gestione y proyecte un culto, con Biblia importable, videos MKV y mando local.

**Architecture:** Núcleo independiente de WPF, renderer nativo con LibVLC y servidor local Kestrel. El controlador WPF integra estado compartido y serializa todas las acciones en el Dispatcher. Archivos JSON atómicos en LocalAppData.

**Tech Stack:** .NET 10, WPF, LibVLCSharp.WPF, VideoLAN.LibVLC.Windows, ASP.NET Core, QRCoder y xUnit.

**Spec:** ../specs/2026-11-11-culto-studio-design.md

## Global Constraints

- Windows x64, aplicación nativa WPF, .NET 10.
- Interfaz y mensajes en español.
- Sin dependencia de OBS, EasyWorship, Deckboard ni TurboTop.
- Seleccionar o preparar contenido nunca modifica la salida en vivo.
- Vista previa de video siempre silenciosa; una única reproducción con audio corresponde a la salida.
- Salida en negro pausa el video para evitar audio residual; restaurar negro no reanuda automáticamente el audio.
- Datos del usuario en %LOCALAPPDATA%/CultoStudio; guardado JSON atómico con copia de recuperación.
- No cambiar firewall ni abrir puertos del router automáticamente. Mando local desactivado hasta activarlo.
- El teléfono debe autenticarse con un token aleatorio de sesión; el mando solo ejecuta acciones predefinidas.
- No incluir el texto completo RVR1960; importar una fuente autorizada y preservar su atribución.
- La desconexión del teléfono no altera la proyección; los controles locales permanecen disponibles.
- No proyectar automáticamente al arrancar ni ante cambios de monitores. Identificar la pantalla seleccionada explícitamente.

## Task 1: Núcleo y contenido

**Files:** src/Culto.Core/Culto.Core.csproj, Models.cs, PresentationSession.cs, BibleCatalog.cs, MediaLibrary.cs, JsonFileStore.cs; tests/Culto.Core.Tests/Culto.Core.Tests.csproj y CoreTests.cs.

**Interfaces:** implementa exactamente los contratos C# de la especificación. No depende de Desktop ni Remote.

- [x] Crear pruebas para preparación independiente, snapshot en vivo, navegación, Biblia, importación MKV y guardado recuperable. Ejemplo:
```csharp
var s = new PresentationSession();
var a = new Cue { Title = "Primero", Body = "Original" };
s.Prepare(a); s.GoLive(); a.Body = "Editado";
s.Prepare(new Cue { Title = "Segundo" });
Assert.Equal("Original", s.Live!.Body);
```
- [x] Ejecutar `.tools/dotnet/dotnet test tests/Culto.Core.Tests` y comprobar fallo de los comportamientos aún ausentes.
- [x] Implementar contratos; normalizar tildes/abreviaturas, mantener orden canónico, rechazar referencias incompletas, XML con DTD y duplicados. Reconocer MKV, MP4, MOV, WEBM, AVI, WMV, MP3, WAV, FLAC, OGG, JPG, JPEG, PNG y BMP.
- [x] Ejecutar pruebas completas y revisar cambios de este componente.

## Task 2: Mando local

**Files:** src/Culto.Remote/Culto.Remote.csproj, RemoteHost.cs, RemoteModels.cs, wwwroot/index.html; tests/Culto.Remote.Tests/Culto.Remote.Tests.csproj y RemoteTests.cs. Solo este componente posee esos archivos.

**Interfaces:**
```csharp
public sealed record DeckButton(string Id, string Label, string Color, string Action, string? CueId = null);
public sealed record RemoteSnapshot(string PlanName, string PreparedTitle, string LiveTitle,
 bool IsBlackout, bool IsPaused, int Volume, double Position, double Duration, IReadOnlyList<DeckButton> Buttons);
public sealed record RemoteCommand(string Action, string RequestId, string? ButtonId = null, double? Value = null);
public sealed record CommandResult(bool Success, string Message);
public sealed class RemoteHost : IAsyncDisposable {
 public string Token { get; }
 public int Port { get; }
 public Task StartAsync(int port, Func<RemoteSnapshot> snapshot,
   Func<RemoteCommand, Task<CommandResult>> command, bool loopbackOnly = false);
 public ValueTask DisposeAsync();
}
```
- [x] Crear pruebas HTTP reales con servidor loopback en puerto 0: sin token retorna 401, comando desconocido 400, duplicación de requestId ejecuta una sola vez y estado refleja callback.
- [x] Ejecutar filtro RemoteTests y observar fallo antes de implementar.
- [x] Implementar API GET /api/state y POST /api/command; serialización camelCase; protección de token, origen y tamaño; página móvil autónoma sin CDN; botones de estado y deck.
- [x] Ejecutar pruebas y revisar que ninguna respuesta revele rutas ni notas.

## Task 3: Proyección y reproducción

**Files:** src/Culto.Desktop/Projection/ProjectionSurface.cs, ProjectionWindow.cs; Services/DisplayService.cs, CaptureService.cs.

**Interfaces:** en Culto.Desktop.Projection, `ProjectionSurface(bool preview = false)` con `ShowCue(Cue? cue, bool blackout = false, bool hideText = false)`, `SetPaused(bool)`, `Restart()`, `Seek(double fraction)`, `int Volume {get;set;}`, `bool Loop {get;set;}`, `double Position {get;}`, `double Duration {get;}`, `bool IsPlaying {get;}`, `event Action<string>? PlaybackError`, `Dispose()`. En Culto.Desktop.Services, `DisplayInfo(string DeviceName,string Label,int Left,int Top,int Width,int Height,bool IsPrimary)`; `DisplayService.GetDisplays()`, `PlaceWindow(Window,DisplayInfo,bool fullscreen)`; `CaptureService.CaptureDisplay(DisplayInfo,string outputPath)` devuelve ruta PNG.

- [x] Preparar prueba manual reproductible: texto largo, imagen panorámica, archivo MKV de 3 segundos con sonido, negro y restauración pausada. La prueba falla inicialmente al no haber renderer.
- [x] Implementar Surface con vídeo silencioso en previa, texto ajustable, errores visibles y evento para operador, liberación de recursos, pausa segura al poner negro/fondo.
- [x] Implementar posicionamiento DPI correcto y ventana de salida que reporta cierre/desconexión. Permitir ventana de ensayo cuando solo hay un monitor.
- [x] Compilar y ejecutar prueba de texto/imagen/MKV; comprobar duración/pausa/posición mediante API del reproductor y captura visual.

## Task 4: Consola del operador e integración

**Files:** src/Culto.Desktop/Culto.Desktop.csproj, App.xaml(.cs), MainWindow.xaml(.cs), Services/WorkspaceStore.cs, Dialogs/*.cs, scripts/*.ps1, README.md, docs/formato-biblia.md.

**Interfaces:** consume las APIs anteriores. `MainWindow` posee PresentationSession, plan y biblioteca; todos los comandos móviles pasan por Dispatcher.InvokeAsync. Preferencias locales serializan plan, biblioteca, deck y dispositivo seleccionado.

- [x] Construir shell con nombre de culto, orden, biblioteca, Biblia, texto libre, previa, resumen en vivo y barra de acciones. Usar recursos XAML de color/tipografía y etiquetas accesibles.
- [x] Conectar importar/abrir/guardar, orden, búsqueda, notas, preparar/proyectar, navegación, reproducción, pantalla y captura estática. Guardar tras cambios; mostrar error recuperable si falla escritura.
- [x] Conectar importación de Biblia JSON/XML, citas y frases, división por versículos y transición al siguiente versículo durante lectura espontánea. Nunca etiquetar datos sintéticos como RVR1960.
- [x] Conectar QR de mando, inicio/cierre explícitos y editor de botones con acciones predefinidas.
- [x] Compilar Release, ejecutar todas las pruebas y comprobar la aplicación con uso de escritorio; corregir fallos reales.
- [x] Publicar carpeta autónoma Windows x64 en artifacts/CultoStudio y un iniciador fácil de encontrar. Documentar instalación, uso, formato bíblico y limitaciones reales.
- [x] Revisión final independiente y repetir solo las comprobaciones afectadas por correcciones.

## Aceptación

La entrega debe incluir un ejecutable nativo y fuentes; ninguna función visible será simulada. Se informará con precisión que RVR1960 requiere importación y que teléfono físico/HDMI real no se han validado si no están disponibles. No se afirma lista para un culto real sin esas comprobaciones.
