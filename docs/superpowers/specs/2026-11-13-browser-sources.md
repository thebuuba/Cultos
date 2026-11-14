# Fuentes Navegador

Agregar fuentes HTML reutilizables a Culto Studio, inspiradas en las propiedades de Navegador de OBS. Autorizado por el usuario: archivo HTML local o URL para títulos. Se ofrece pantalla completa y superposición transparente; no se modifica ni captura el navegador personal.

## Comportamiento

- Administrar fuentes por nombre desde Escenas → Navegador y + Agregar → Navegador.
- Propiedades: archivo local/URL, ancho, alto, FPS, CSS personalizado y audio activado opcionalmente; vista previa muda.
- Guardar fuentes en workspace.json. Agregar una copia independiente al orden, editarla y proyectarla como los demás contenidos.
- Mostrar una o varias fuentes transparentes sobre la salida existente, conservando el video y audio actuales. Ocultar y recargar individualmente; negro/logo ocultan las superposiciones. No reactivar fuentes automáticamente al arrancar.
- Salida local, HDMI y reproductor flotante reflejan la misma composición, sin navegadores o audio duplicados.
- CSS predeterminado transparente, sin margen ni barras de desplazamiento. Conservar JavaScript, CSS, imágenes y rutas relativas de archivos locales.
- No prometer compatibilidad con plugins de OBS, DRM, códecs propietarios ni sesiones del Chrome personal.

## Arquitectura y contrato

CueKind.BrowserSource se añade al final del enum. Cue.Browser es un record BrowserSourceOptions inmutable con Location (string), IsLocalFile (bool), Width/Height (int, 1920/1080), FramesPerSecond (int, 30), CustomCss (string), AudioEnabled (bool, false). Normalize() valida límites y ubicación; GetAddress() devuelve URI file/http/https; Validate() valida existencia local. Copias, igualdad de sesión y JSON conservan Browser.

CefSharp.Wpf.NETCore ofrece Chromium renderizado en WPF y transparencia real; la proyección puede reflejarlo mediante su VisualBrush actual. BrowserSourceView : Viewbox, IDisposable expone SetSource(BrowserSourceOptions, bool muted), SetMuted(bool), Reload(), IsReady, event Action FrameReady y event Action<string> Error. Tamaño HTML fijo dentro de Viewbox escalado al lienzo. Inicialización CEF diferida y cierre de controles; no desactivar seguridad web ni certificados. CSS mediante JSON-escaped script en frame principal. Navegación fallida informa error, no deja carga infinita.

ProjectionSurface implementa BrowserSource como contenido y usa un contenedor de superposiciones por encima de su mixer. Public SetBrowserOverlays(IEnumerable<Cue>) y ReloadBrowserSource(Guid). Renderizar cada fuente una sola vez en salida. Preview mute; audio de fuentes retiradas debe detenerse; negro/logo silencia y oculta overlays. Mantener propiedades de audio/transport de archivos existentes.

## Validación

Pruebas de límites, URI local/HTTP, copias/JSON/ediciones independientes. Ensayo nativo con HTML local animado y transparente, CSS y recursos relativos, pantalla completa, superposición sobre color/video, negro, retirada/cierre y reflejo local. Pruebas de UI para guardar/editar/agregar al orden; compilación y suites Core/Remote existentes. Publicar una nueva carpeta versionada y actualizar iniciador sin cerrar la versión abierta.
