# Tarjeta de producción y escenas

> Ejecución: seguir las tareas en orden con pruebas antes de cambiar comportamiento; conservar el motor y los controles existentes.

**Objetivo:** reunir escenas/transiciones, Vista previa y En pantalla en una tarjeta común, con transporte local superpuesto al pasar el ratón.

**Diseño confirmado:** monitores lado a lado; escenas pequeñas a su izquierda; controles semitransparentes al pasar el ratón, nunca en HDMI. Una sola entrega al terminar el conjunto.

**Arquitectura:** mantener las instancias y los IDs de los ocho paneles actuales. Añadir Escenas como panel acoplable y compartir el contorno del grupo de tres. Migrar los diseños anteriores añadiendo Escenas junto a los monitores. Mover el transporte existente entre dos anfitriones locales según el monitor bajo el ratón, reutilizando el mismo reproductor.

**Tecnología:** WPF, AvalonDock y LibVLC actuales; sin dependencias nuevas.

## 1. Composición y compatibilidad

- [x] Añadir `ProductionCardTests.cs` y ruta `--production-card`. Verificar primero que faltan Escenas y el transporte superpuesto.
- [x] En `MainWindow.xaml`, añadir `ScenesPanel` a la izquierda de los monitores y mover ahí Logo/Negro. En `WorkspaceLayouts.cs`, incluir `scenes` en los tres presets y migrar distribuciones de ocho paneles sin recrear contenido.
- [x] En `MainWindow.Docking.cs`, compartir el contorno de Escenas/Previa/Salida y registrar Escenas en el menú de visibilidad. Conservar la migración que retira Secuencia.
- [x] Actualizar las comprobaciones que enumeran los paneles y probar presets, visibilidad, colocación y migración.

## 2. Escenas y transiciones

- [x] Crear `MainWindow.Scenes.cs`: `SceneMediaClick`, `ChooseSceneMediaClick`, `SceneYouTubeClick`, `SceneTransitionsChanged` y `RefreshSceneChoices`.
- [x] Guardar `QuickSceneImage` y `QuickSceneVideo` en `WorkspaceData`. Un clic usa la imagen/video preparado si coincide con el tipo de escena; en otro caso recupera la elección guardada. El menú derecho permite cambiar el archivo. Conservar la elección si se cancela o falla la apertura.
- [x] YouTube reutiliza `YouTubeDiscovery` y la selección existente; una ventana disponible se proyecta con un clic, y el menú permite elegir otra. Actualizar fuentes aunque su pestaña esté inactiva cuando Escenas esté visible.
- [x] El selector de transición escribe las mismas opciones globales; el botón de ajustes abre el editor actual de duración y audio. No introducir otro temporizador de transición.
- [x] Probar que las escenas recuerdan el contenido, que preparar otra cosa no lo reproduce y que el estado guardado se recupera.

## 3. Transporte dentro de los monitores

- [x] En `MainWindow.xaml`, colocar `LiveOverlayHost` y `PreviewOverlayHost` dentro de los recuadros locales. Mover `MediaPlaybackPanel` al primero, conservar sus nombres, botones y eventos.
- [x] En `MainWindow.Player.cs`, mover el único transporte al anfitrión bajo el ratón cuando Vista previa siga la salida. Mostrarlo con `UiMotion.Fade`, mantenerlo durante arrastre/foco y ocultarlo al salir. Si se prepara otro contenido, devolverlo a En pantalla.
- [x] Mantener pausa/avance deshabilitados para captura de YouTube, y conservar volumen, repetición, atajos, reproductor ampliado y audio retenido.
- [x] Probar pausa/avance/posición sobre el reproductor existente y comprobar que `ProjectionSurface` no contiene botones ni el transporte.
- [x] Revisar capturas en tamaños amplio y compacto, ejecutar pruebas pertinentes, documentar evidencia y preparar una sola versión después de terminar el conjunto.

## Evidencia final

136 comprobaciones generales sin fallos; suites de produccion, tamanos, migracion, visibilidad, YouTube y arranque correctas. Revision nativa con un himno real y datos temporales: pausa compartida, traslado del transporte al monitor bajo el raton y ocultamiento al salir. El arranque nativo sin ventana proporcionada crea una sola consola. Compilacion y publicacion 0.34.0 correctas, 232 Core y 64 Remote. Logs en `.tools/production-*.log`.
