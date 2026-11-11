# Culto Studio: diseño de la primera versión

Diseño confirmado en conversación el 11 de noviembre de 2026. Nombre provisional y editable en versiones futuras. Aplicación de escritorio Windows para una iglesia adventista. OBS se usa exclusivamente para proyección, sin transmisión ni grabación. Tres pantallas reciben la misma señal HDMI; existe una sola salida de congregación además del monitor del operador.

## Objetivo y alcance

Preparar un orden de culto, incorporar imágenes, videos e himnos MKV, preparar pasajes bíblicos, proyectar a una salida independiente y controlar el culto desde el teléfono en la red local. La aplicación debe funcionar sin internet después de instalarla. La primera versión es local y monousuario, con un único operador autorizado a compartir el mando.

Funciones incluidas: lista del culto editable/reordenable, notas privadas, biblioteca importable, búsqueda de himnos por número/título, reproducción integrada LibVLC, búsqueda bíblica por referencia/frase, importación JSON y Zefania XML, vista previa silenciosa, proyección explícita, negro, fondo, siguiente/anterior, controles de reproducción, pantalla de salida seleccionable, mando web local autenticado y botones personalizables. Las capturas iniciales serán imágenes estáticas de pantalla seleccionada; captura continua y presentaciones PowerPoint quedan como ampliaciones y no deben aparecer como controles simulados.

El módulo bíblico estará operativo, pero no se incluirá el texto completo de la Reina-Valera 1960 sin una fuente autorizada. El usuario debe poder importar un archivo propio autorizado. No se descargan textos de repositorios de procedencia desconocida. Las pruebas usan texto sintético explícitamente identificado; nunca se presenta como Biblia real.

## Requisitos globales

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

## Interfaz

Ventana oscura de 1400×900, mínimo 1100×720, escalable. Cabecera con nombre del culto, guardar/abrir, configuración de pantalla y mando. Columna izquierda con lista del orden, añadir, mover y quitar. Centro con pestañas Himnario/Archivos, Biblia y Texto; búsqueda y lista. Columna derecha con vista previa y resumen de salida. Franja inferior con Proyectar, anterior/siguiente, negro, fondo y reproducción. Tecla F5 proyecta y Escape cancela diálogos; los atajos de navegación no interceptan escritura.

Paleta navy #101722, superficies #192332, bordes #2B3A4D, texto #F0F4FA, secundario #A6B7CB, acento turquesa #43D9BE, ámbar #FFCF75 para preparado, verde para vivo. Estados comunicados con texto además de color. Tipografía Segoe UI, controles accesibles, foco visible. La proyección usa texto blanco sobre fondo azul muy oscuro y ajuste uniforme para evitar corte. El teléfono tiene botones grandes de al menos 48 px y ninguna dependencia de CDN.

## Arquitectura y contratos

Tres proyectos de producción: Culto.Core (modelos, Biblia, sesión y persistencia), Culto.Remote (servidor local y página móvil), Culto.Desktop (WPF, reproducción, pantallas e integración). Un proyecto de pruebas xUnit para lógica/servidor. No frameworks MVVM ni bases de datos externas para esta escala.

Namespace raíz por proyecto: Culto.Core, Culto.Remote, Culto.Desktop. Los tipos del núcleo están directamente en Culto.Core.

```csharp
public enum CueKind { Text, Bible, Image, Video, Audio }
public sealed class Cue {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public CueKind Kind { get; set; }
    public string Body { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Version { get; set; } = "";
    public string? FilePath { get; set; }
    public int? HymnNumber { get; set; }
    public string Notes { get; set; } = "";
}
public sealed class ServicePlan {
    public string Name { get; set; } = "Culto del sábado";
    public List<Cue> Items { get; set; } = [];
}
public sealed class PresentationSession {
    public Cue? Prepared { get; }
    public Cue? Live { get; }
    public bool IsBlackout { get; }
    public bool IsTextHidden { get; }
    public event Action? Changed;
    public void Prepare(Cue cue);
    public void GoLive(); // snapshot independiente del objeto preparado
    public void ToggleBlackout();
    public void ShowBackground(); // conserva Live, oculta su texto; pausa medios en UI
    public bool Move(ServicePlan plan, int delta); // basado en Id de Live, no selección
}
public sealed record BibleVerse(string Book, int Chapter, int Verse, string Text);
public sealed class BibleDocument {
    public string Name { get; set; } = "";
    public string Attribution { get; set; } = "";
    public List<BibleVerse> Verses { get; set; } = [];
}
public sealed class BibleCatalog {
    public BibleDocument Document { get; }
    public BibleCatalog(BibleDocument document);
    public static BibleCatalog Load(string path); // JSON o Zefania XML; DTD prohibido
    public IReadOnlyList<BibleVerse> FindReference(string query); // error español si inválida/incompleta
    public IReadOnlyList<BibleVerse> Search(string query, int limit = 100);
    public Cue ToCue(BibleVerse verse);
}
public static class MediaLibrary {
    public static bool IsSupported(string path);
    public static Cue FromFile(string path); // valida archivo y extensión
}
public static class JsonFileStore {
    public static void Save<T>(string path, T value);
    public static T? Load<T>(string path); // recupera .bak si JSON principal corrupto
}
```

Reproducción: ProjectionSurface es UserControl WPF con LibVLCSharp.WPF. Instancia independiente para la vista previa y otra para la salida. Previa silenciada y su volumen nunca se comparte. Video de salida controlado desde la ventana principal; el resumen en vivo indica título, tiempo y estado, no finge ser una segunda decodificación sincronizada. ProjectionWindow muestra la superficie en monitor elegido usando coordenadas físicas/DPI. Al cerrar salida se pausa y actualiza el estado. Las desconexiones ocultan la salida y notifican; no moverla al monitor del operador.

Mando: Kestrel embebido sin privilegios de administrador, puerto 18765 por defecto. Token aleatorio de 32 bytes cambia cada arranque del mando. La URL de vinculación lleva token en fragmento; JavaScript lo guarda en sessionStorage y lo elimina de la barra. Cada API requiere Authorization Bearer. Comandos serializados y con requestId para deduplicar toques repetidos; permiten solamente project/next/previous/blackout/background/pause/restart/volume/seek/deck. No comandos de shell ni rutas proporcionadas por clientes. Cada botón del deck apunta a acción conocida o Id de un elemento local. Tamaño de solicitud limitado, origen validado, sin CORS abierto. El servidor muestra solo estado y botones, nunca rutas locales ni notas privadas.

## Validación

Pruebas del núcleo: preparación no afecta a vivo; vivo es copia estable; navegación por elemento vivo aunque cambie selección; límites de navegación; referencias bíblicas con tildes/abreviaturas/rangos; rechazo de rangos incompletos y XML con entidades; orden canónico; persistencia recuperable; números de himno. Pruebas HTTP reales: autenticación obligatoria, acción inválida rechazada, deduplicación y estado actualizado. Prueba de escritorio: abrir aplicación, editar texto, preparar/proyectar en ventana de prueba, negro/fondo, importar archivo, reproducir MKV de prueba generado localmente, Biblia sintética y reinicio. Probar manualmente monitor HDMI y teléfono real queda pendiente de disponer de esos dispositivos.

## Fuentes técnicas

- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- https://docs.videolan.me/libvlcsharp/docs/getting_started.html
- https://www.americanbible.org/rights-and-permissions/
- https://dotnet.microsoft.com/en-us/download/dotnet/10.0

