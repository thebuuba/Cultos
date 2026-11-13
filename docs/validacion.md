# Validación de Culto Studio

Comprobación actualizada el 13 de noviembre de 2026 en esta PC Windows. Entrega actual Windows x64 en `artifacts/CultoStudio-0.35.3`; iniciador `Abrir Culto Studio.cmd`. Las secciones siguientes distinguen las comprobaciones de cada versión.

## Compilación y pruebas

### Cola directa de reproducción ? 0.35.3

Añadir a lista de reproducción pasa a ser una acción directa sin submenú: agrega al final de la cola actual sin preguntar nombres ni crear una lista cada vez. Si no existe una cola, la crea automáticamente y la reutiliza. + Videos y la acción desde Himnario comparten este flujo. El selector y Nueva lista desaparecen del panel; las colecciones anteriores se conservan y pueden recuperarse opcionalmente en Listas guardadas. ActiveVideoListId persiste la cola elegida.

Actualizar los elementos tras agregar no cancela la continuación armada después de un video externo. Se mantienen la validación de archivos, copias independientes, rollback al fallar guardado, eliminación, orden y reglas de avance. La prueba --video-lists exige acción directa sin hijos y verifica que dos adiciones sin listas previas crean una sola cola guardada; también conserva los escenarios de reproducción real y cancelación. Evidencias .tools/direct-queue-red.log y direct-queue-final.log del worktree. Captura .tools/video-list.png.

### Submenús visibles ? 0.35.2

La plantilla global de MenuItem carecía de PART_Popup y ItemsPresenter para sus hijos. Los elementos internos existían y sus acciones funcionaban, pero no se mostraban al abrir Añadir a lista de reproducción. Se incorpora el popup enlazado a IsSubmenuOpen y una flecha para HasItems, manteniendo el tema. La prueba ampliada abre el menú real, exige popup abierto y elección visible con ancho positivo y verifica la persistencia al pulsarla. Falló antes del cambio y pasa después, junto con avance/cancelación de listas. Evidencias .tools/playlist-submenu-red.log y playlist-submenu-final.log del worktree.

### Medios a lista de reproducción ? 0.35.1

El clic derecho sobre una fila o miniatura de Medios ofrece Añadir a lista de reproducción, con las listas guardadas y Nueva lista. Usa la fila pulsada, valida que sea video y guarda una copia independiente sin proyectarla. El fallo de guardado revierte la adición. La prueba --video-lists comprueba el menú, la copia, persistencia, exclusión de imágenes y conservación del contenido preparado/en pantalla. Evidencias: .tools/media-playlist-final.log y media-playlist-publish.log del worktree.

### Lista de videos ? 0.35.0

Las colecciones existentes se muestran en una pestaña adicional de las fuentes, denominada Lista de videos. Conservan sus elementos; se pueden crear listas, agregar archivos de video, eliminar con el botón o Delete y reordenar con las flechas. Las modificaciones se guardan con rollback ante error. La reproducción por doble clic, Enter o botón activa una continuación ligada a la identidad de la lista y del video.

El avance usa el final natural del reproductor real, sin comparar posiciones aproximadas. Al proyectar otro contenido se cancela la continuación. La opción explícita para continuar tras un video externo toma el elemento seleccionado de la lista como punto de entrada y se consume una vez. Otro cambio de proyección la cancela. Iniciar la lista desactiva la repetición individual para permitir el avance. El último elemento deja de avanzar; archivos no disponibles detienen la lista con el error habitual.

Pruebas: 234 Core correctas y ensayo nativo --video-lists con guardado, orden, eliminación, reproducción real A a B, fin de lista, cancelación por video externo y continuación explícita. Se mantiene --fixed-monitors para el reproductor compartido. Evidencias en .tools/video-lists-*.log del worktree; captura .tools/video-list.png. Los ensayos usan datos temporales. No se declara validada la antigua suite que enumera los paneles anteriores sin la pestaña nueva.

### Altura natural de los monitores ? 0.34.2

La tarjeta y sus dos monitores se alinean arriba; las filas de imagen usan altura automática en lugar de llenar el espacio vertical disponible. El contenedor nativo deja de pintar un fondo largo detrás de la tarjeta. Se conserva libre el espacio inferior. Se revisó `.tools/compact-monitors.png` y pasaron las comprobaciones `--fixed-monitors`, incluyendo altura acotada, pie junto a la imagen y controles de reproducción. Evidencias en `.tools/compact-card-final.log` y `.tools/compact-card-publish.log` del worktree.

### Monitores unidos de verdad ? 0.34.1

Vista previa y En pantalla son hijos del mismo ProductionPanel y un solo contenido acoplado, identificado como preview. Se elimina el contenido acoplable output, se oculta la cabecera nativa de Monitores y se deshabilitan flotado, ocultado y colocaci?n del conjunto. Los r?tulos internos no tienen botones de cierre ni asas de arrastre. Proyectar, selector de transici?n y ajustes est?n entre ambos monitores. Las escenas mantienen su panel lateral.

Los dise?os antiguos se convierten retirando output sin recrear los controles del reproductor. La prueba --fixed-monitors fall? antes del cambio y pasa despu?s: contenedor ?nico, orden izquierda-centro-derecha, bloqueo de colocaci?n, migraci?n de salida antigua y controles reales de pausa, avance, posici?n y volumen. Se revis? .tools/fixed-monitors.png. Compilaci?n/publicaci?n correctas, 232 Core y 64 Remote. No se declara la antigua suite general de acoplamiento como validada: sus expectativas de dos monitores separables corresponden al dise?o retirado. Evidencias: .tools/fixed-monitor-red.log, fixed-monitor-final.log y fixed-monitor-publish.log en el worktree.


### Tarjeta de producción, escenas y transporte local — versión 0.34.0

Escenas queda a la izquierda de Vista previa y En pantalla, dentro del mismo contorno y sombra. Los tres paneles conservan su acoplamiento, tamaño y visibilidad independientes. Principal, Compacta y Proyección incluyen el nuevo panel; los diseños anteriores completos de ocho paneles añaden Escenas sin reemplazar sus controles. La migración mantiene el marcador adaptativo de Principal y continúa retirando Secuencia. Los controles de transición permanecen debajo del área desplazable de escenas.

Logo y Fondo negro reutilizan sus acciones anteriores. Imagen y Video pueden proyectar el medio preparado o recuperar su elección guardada; elegir un archivo desde el menú derecho lo valida, guarda y prepara sin proyectarlo todavía. Los errores de apertura o guardado conservan la elección anterior. YouTube comparte las fuentes y el selector de captura existentes, se actualiza mientras Escenas o su pestaña estén visibles y permite elegir otra ventana desde el menú. El selector de transición modifica la misma configuración global y conserva duración y fundidos de audio.

El transporte existente se mueve entre los dos anfitriones locales, dentro de sus recuadros. Vista previa lo recibe solamente cuando está siguiendo la salida. Se mantiene un solo reproductor. `ProjectionSurface`, el contenido entregado a HDMI, no contiene los botones ni la barra. La barra se desvanece al salir con el ratón, se mantiene al arrastrar la posición y reaparece al usar el teclado. Se corrigió que el foco de un clic en Pausa la dejara fija. La captura de YouTube sigue sin pausa o avance locales que congelen solamente su imagen.

Se confirmó mediante inventario nativo que el anfitrión de pruebas abría una segunda consola por `StartupUri`, interfiriendo con el foco y el cierre. El arranque ahora crea una consola únicamente si el anfitrión no ha proporcionado una. Una prueba global verificó el fallo antes del cambio y pasa con una sola ventana. La revisión general final completa 136 comprobaciones sin fallos, incluidas las búsquedas, atajos y protección de cambios al cerrar que habían fallado en ensayos anteriores.

Pasaron nueve comprobaciones de producción, cuatro de tamaño compacto —incluido transporte dentro de un monitor de 220 DIP—, diez de distribuciones, once de colocación y tamaños, siete de visibilidad, seis de YouTube, 16 visuales y nueve de arranque. Se verificaron capturas amplias y compactas. En una consola aislada, con un himno real y volumen cero, se comprobó el gesto de entrada/salida con el ratón, Pausa y el traslado de la barra entre ambos monitores. La revisión de código final no dejó hallazgos pendientes en estos cambios. Las pruebas emplean datos temporales y no cambian el culto del usuario.

Compilación y publicación sin errores ni advertencias; 232 Core y 64 Remote correctas. El ensayo `--native-startup`, que deja crear la ventana al arranque normal de App en vez de proporcionarla, también pasa con una sola consola visible y cierre correcto.

Evidencias en `.tools/worktrees/culto-polymet/.tools/production-desktop-final.log`, `production-final-*.log`, `production-host-red.log`, `production-keyboard-red.log`, `production-demo-final.log`, `production-native-startup.log`, `production-publish.log` y `.tools/visual-theme/production-controls.png` / `production-preview-controls.png`.

### Preparación de la primera reproducción — versión 0.33.3

El usuario precisa que el problema aparece solamente la primera vez. Una ejecución inicial de `--cold-play` no llegó a la reproducción dentro de los ocho segundos; tras añadir diagnóstico y repetir, quince arranques independientes con un himno real y una copia temporal de la configuración sí reprodujeron con una única orden. Eso confirma la intermitencia, pero no identifica por sí solo la causa exacta del fallo observado en la interfaz del usuario.

Antes el motor nativo se inicializaba al solicitar el primer archivo o una captura. Ahora un compositor de reserva prepara el motor al terminar de abrir la app; permanece sin contenido, reproductores de archivo ni ventana de salida. La primera proyección adopta esa misma instancia. Se conserva la petición mientras se espera al motor y se libera la reserva al cerrar. Además, una inicialización anticipada fallida se descarta al recibir la primera orden real. Si falla mientras la orden ya está pendiente, se vuelve a inicializar una sola vez conservando esa orden y su estado de pausa; no se reintentan archivos incompatibles ni se sobreescribe una petición posterior.

La prueba de preparación falló antes del cambio. La recuperación de una preparación fallida también se verificó en rojo retirando temporalmente su protección: la primera orden se perdía. Una prueba adicional reprodujo la pérdida cuando la inicialización fallaba después de pedir el video. Evidencias iniciales en `.tools/worktrees/culto-polymet/.tools/reliable-projection-cold.log`, `reliable-projection-red.log`, `reliable-warmup-failure-red.log`, `reliable-pending-failure-red.log` y `reliable-first-*.log`. Los ensayos usan un directorio temporal y no guardan cambios sobre el culto del usuario.

La versión final pasa ocho comprobaciones de arranque y 41 del compositor/transiciones. El primer himno real alcanzó reproducción en movimiento con una sola orden y sin bloquear la interfaz; la prueba conserva la instancia preparada al abrir la app. Compilación sin errores ni advertencias, 232 Core y 64 Remote correctas. Evidencias finales en `reliable-projection-final-cold.log`, `reliable-projection-smooth.log` y `reliable-projection-publish.log`.

Límite de la revisión general: una ejecución previa al último ajuste del reintento pasó sus 135 comprobaciones. En la versión final, una ejecución se detuvo después de 134 comprobaciones al cerrar por `ResourceReferenceKeyNotFoundException: PanelBorderBrush` y se terminó solamente el proceso de ensayo aislado. La repetición completó el cierre, con 127 comprobaciones correctas y ocho fallos ligados al foco de teclado (búsquedas, atajos y Delete); no es una validación general limpia. No se cambiaron los recursos del tema ni el foco en esta corrección. Evidencias en `reliable-projection-desktop.log`, `reliable-projection-desktop-retry.log` y el registro existente `errors.log`. Estas incidencias quedan pendientes de diagnóstico independiente.

### Botones compactos de la Biblia — versión 0.33.2

Los botones de estilo, limpiar búsqueda y capítulo anterior/siguiente dejan de heredar la altura de 38 DIP con anchuras automáticas estrechas. Usan 32 × 32 DIP, sin relleno interior, y conservan la plantilla circular, foco, accesibilidad y eventos existentes. El selector de capítulo comparte el borde derecho con las flechas; el espacio entre ambas sigue siendo de 4 DIP.

Pasaron 15 comprobaciones visuales y 17 de herramientas bíblicas, sin fallos. Se comprobaron dimensiones, contención y alineación de los cuatro botones y se revisó la captura `bible-card.png`. Compilación sin errores ni advertencias, 232 pruebas Core y 64 Remote correctas y publicación local completada. Evidencias en `.tools/worktrees/culto-polymet/.tools/bible-buttons-visual.log`, `bible-buttons-bible.log` y `bible-buttons-publish.log`. Las pruebas usan datos sintéticos y no modifican la Biblia ni el culto del usuario.

### Retirada de Secuencia — versión 0.33.1

Se eliminan el panel Secuencia, su menú, controles, plantilla y eventos exclusivos. Se conserva el estilo compartido por las tarjetas de Medios con un nombre propio. El orden del culto mantiene el arrastre vertical, la selección, los clics y su contenido; ocupa el espacio liberado en Principal y Compacta. Los ocho paneles restantes siguen disponibles para mostrar, ocultar, agrupar y mover.

Antes de validar una distribución antigua completa, se retira el identificador `sequence`, esté acoplado, oculto o flotante. Se eliminan sus contenedores vacíos salvo los referenciados por otros paneles ocultos. La conversión ocurre después de las migraciones de cinco/seis paneles; los IDs desconocidos o duplicados siguen rechazándose. Las pruebas verifican que los controles conservados mantienen su identidad y que un guardado posterior no vuelve a introducir Secuencia.

Pasaron tres comprobaciones de retirada, 135 generales, nueve de distribuciones, 14 visuales y seis de visibilidad. Se retiraron tres escenarios exclusivos de Secuencia y se mantienen las pruebas de arrastre/reordenación del orden. Compilación sin errores ni advertencias, 232 Core y 64 Remote correctas. Evidencias en `.tools/worktrees/culto-polymet/.tools/remove-sequence-*.log`. Los datos reales del usuario permanecen sin modificaciones durante las pruebas.

### Ocultar y mostrar paneles — versión 0.33.0

Espacio de trabajo permite marcar o desmarcar los nueve paneles. La marca indica que el panel pertenece al espacio mostrado, aunque su pestaña esté inactiva. El menú contextual de la cabecera añade Ocultar panel y el menú principal ofrece Mostrar todos los paneles. Se usa Hide/Show de AvalonDock y se mantienen las mismas instancias de contenido; CanClose y el autoocultado siguen deshabilitados. Se excluyen los paneles ocultos de los destinos de colocación.

La visibilidad se guarda en `paneles-v3.xml`. La restauración deja de forzar Show sobre los paneles ocultos, y cambiar visibilidad convierte la distribución en personalizada para que el ajuste por anchura no los vuelva a abrir. La activación explícita de una fuente oculta desde un atajo la recupera y guarda. Ocultar la tarjeta En pantalla no elimina el reproductor ni cambia el contenido de la sesión.

Las cinco pruebas específicas y la comprobación inicial pasan: ocultar/mostrar todos los identificadores conservando los controles, restaurar XML con paneles ocultos, redimensionar sin restablecerlos, marcas correctas para pestañas inactivas, recuperación tras ocultar todo y conservación de video en marcha al ocultar/mostrar la salida. Pasaron también las 138 comprobaciones generales y los nueve escenarios de distribuciones. Evidencias: `.tools/worktrees/culto-polymet/.tools/panel-visibility-red.log`, `panel-visibility-final.log`, `panel-visibility-desktop.log`, `panel-visibility-presets.log` y `panel-visibility-publish.log`.

### Legibilidad del texto — versión 0.32.4

Se encontraron rótulos de 9–11 DIP en la consola y letras de miniatura dibujadas a 8/9 DIP antes de ampliarlas. Los rótulos pequeños de MainWindow pasan a 12 DIP. Las miniaturas dibujan los glifos a su tamaño final con formato Display, conservando el escalado uniforme de imágenes e iconos. Los monitores usan filtrado de alta calidad al reducir el contenido local. No cambia la tipografía elegida para la proyección.

Se habilita la indicación ClearType en los cuerpos opacos de las tarjetas y sus cabeceras, después de los efectos y recortes exteriores. La fuente Lexend sigue incluida y comprobada. La referencia de Microsoft advierte que los efectos, recortes y VisualBrush pueden deshabilitar ClearType si el destino intermedio admite transparencia: https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.cleartypehint .

El usuario confirmó que el problema se notaba en menús/listas y monitores. Se revisaron capturas a 1900×1000, 1480×900, 1280×680 y 1200×680; pasaron las 14 comprobaciones visuales y 138 generales. Evidencias en `.tools/worktrees/culto-polymet/.tools/text-clarity-visual.log`, `text-clarity-desktop.log` y `text-clarity-publish.log`. La mejora percibida en el monitor físico y en otras escalas DPI queda para comprobación del usuario.

### Salida sin fila Anterior/Siguiente — versión 0.32.3

Se retiró la fila de botones Anterior y Siguiente de la tarjeta de salida y el código que ajustaba su disposición. La comprobación visual exige que el control ya no exista. Se conserva la navegación del resto del sistema. Evidencias en `.tools/worktrees/culto-polymet/.tools/remove-navigation-visual.log` y `remove-navigation-publish.log`.

### Paneles neutros — versión 0.32.2

Por petición del usuario se retiran los tonos rojo y amarillo de YouTube y del Himnario. Sus superficies, cabeceras y contenedores usan el mismo gris `#2A2B2F` que los demás paneles. Se conservan las pestañas superiores y la tarjeta compartida de los monitores. Evidencias en `.tools/worktrees/culto-polymet/.tools/neutral-panels-visual.log` y `neutral-panels-publish.log`.

### Pestañas superiores y tarjeta de monitores — versión 0.32.1

La franja nativa de pestañas pasa a la fila superior y se elimina la curva del borde superior de las fuentes. Se conserva la plantilla de AvalonDock, incluidos los presentadores de contenido, la selección y el arrastre. La tarjeta compartida de Vista previa y En pantalla se dibuja en su grupo contenedor, con un solo contorno y sombra; los paneles internos conservan sus controles y separador, sin márgenes ni contornos individuales. Si se separan, recuperan sus tarjetas individuales.

`CueThumbnail` escala uniformemente en ambos ejes y centra el contenido. YouTube usa una caja 16:9 de hasta 260 DIP de ancho en vez de ocupar todo el ancho con una altura fija. Se revisó `youtube-card.png` con un título largo de prueba y las capturas de consola. Pasaron las 14 comprobaciones visuales, incluidas dos nuevas sobre pestañas superiores/tarjeta compartida y miniatura proporcional; también pasaron las 138 comprobaciones generales. Evidencias en `.tools/worktrees/culto-polymet/.tools/card-refinement-final.log`, `card-refinement-desktop.log`, `card-refinement-presets.log`, `card-refinement-panels.log` y `card-refinement-publish.log`.

### Fuentes en pestañas — versión 0.32.0

Principal agrupa Himnario, Biblia, YouTube, Medios y Texto en un mismo panel. El orden del culto, la vista previa, la salida y Secuencia permanecen separados y visibles. Compacta conserva las cinco fuentes a la izquierda, ajustando sus proporciones para ventanas pequeñas. La adaptación por tamaño conserva la pestaña activa. Proyección continúa como alternativa con más espacio para los monitores. Las pestañas conservan los controles existentes, el arrastre de cabeceras, los colores del Himnario y YouTube y las búsquedas.

El nuevo diseño se guarda en `paneles-v3.xml`, por lo que la primera apertura adopta la distribución aprobada. `paneles-v2.xml` permanece intacto para volver al diseño anterior con Recuperar distribución anterior; si no existe se ofrece `paneles.xml`. No se modifica biblioteca, Biblia ni orden. Las pruebas de restauración y guardado usan el nuevo archivo.

Se verificaron los nueve paneles y sus instancias a través de presets, las cinco pestañas y la visibilidad de orden/monitores al cambiar entre ellas. Las diez pruebas de paneles comprueban también que recorrer las fuentes conserva la búsqueda y el reproductor en marcha. Pasaron las 138 comprobaciones generales y las 12 de tema visual; después del último ajuste de Compacta se repitieron los nueve escenarios de distribuciones y las 12 comprobaciones visuales. Se revisaron las capturas a 1900×1000 y 1200×680. Evidencias en `.tools/worktrees/culto-polymet/.tools/tabbed-*.log` y `.tools/visual-theme/`. La publicación incluye 232 Core y 64 Remote correctas, sin errores ni advertencias.

### Destino inferior y grupos redundantes — versión 0.31.3

El destino del arrastre usa la tarjeta completa, con cabecera, pestañas y margen inferior. Antes se medía solo el cuerpo y las esquinas podían seleccionar el lateral por sus distancias proporcionales. Ahora las bandas superior e inferior tienen prioridad en las esquinas y una altura máxima de 64 DIP; los laterales usan hasta 48 DIP. Soltar recalcula el destino con la posición final del ratón.

Después de colocar un panel se eliminan los grupos de un solo hijo conservando sus proporciones exteriores. Esto evita acumular divisiones dentro de divisiones al mover repetidamente los mismos paneles. Se conserva la distribución personalizada, sin aplicar un preset nuevo. Dos regresiones fallaron antes y pasaron después: detección inferior en esquinas/pestañas y seis reorganizaciones verticales sin grupos redundantes ni desbordamiento. Las diez comprobaciones de paneles pasan. Evidencias: `.tools/worktrees/culto-polymet/.tools/drop-fit-red.log`, `drop-fit-final.log`, `drop-fit-presets.log`, `drop-fit-desktop.log` y `drop-fit-publish.log`.

### Color de fuentes — versión 0.31.2

YouTube usa un fondo rojo tenue `#3B2B30` y el Himnario un amarillo tenue `#393529`, tanto en el cuerpo como en el título y contenedor. El contenedor toma el color del panel seleccionado al agrupar pestañas. Se revisó la captura 1900×1000 y pasaron las 12 comprobaciones de `--visual-theme`, incluidas ventanas compactas y contraste de controles existentes. Evidencias: `.tools/worktrees/culto-polymet/.tools/card-tints-visual.log` y `card-tints-publish.log`. No cambia el contenido proyectado.

### Arrastre directo de cabeceras — versión 0.31.1

La corrección anterior verificaba colocación mediante menú, pero no solucionaba el gesto que pidió el usuario. Las cabeceras ahora capturan el arrastre y muestran una zona verde sobre el panel de destino. Los bordes dividen arriba, abajo o a los lados; el centro agrupa en pestañas. La ventana flotante no se crea durante este gesto. Soltar aplica la colocación existente y guarda la distribución; Esc o perder la captura cancela sin cambios. Los botones de la cabecera conservan su menú.

Prueba con ratón mediante CUA sobre una consola de ensayo y datos temporales: se arrastró Himnario desde su título hasta la parte inferior de Biblia; la captura confirmó Biblia arriba e Himnario debajo. Después se arrastró el separador horizontal hacia arriba y se observó el cambio de altura de ambos paneles. No se modificó la consola real abierta del usuario. El modo `--panel-drag-demo` permite repetir este ensayo durante 55 segundos.

La regresión automatizada falló antes del cambio y pasó después: verifica destino resaltado, colocación superior/inferior sin flotación y cancelación. Pasaron las ocho comprobaciones de `--panel-sizing`, nueve de `--workspace-layouts` y tres de `--compact-output`. Publicación Release sin errores ni advertencias, 232 Core y 64 Remote correctas. Evidencias en `.tools/worktrees/culto-polymet/.tools/panel-drag-*.log`. Se conserva 0.31.0; entrega en `artifacts/CultoStudio-0.31.1`.

### Paneles unidos y contenido remoto — versión 0.31.0

Los separadores convierten los pesos proporcionales a los tamaños reales de todas las celdas antes de intercambiar espacio entre dos vecinos. La regresión inicial reprodujo un grupo inmóvil (626 DIP) y el desplazamiento de un tercer panel; despu?s el grupo recibe los 20 DIP solicitados sin mover al tercero. Cancelar restaura todos los pesos. Las alturas mínimas permiten apilar las fuentes; su contenido conserva un tama?o finito y desplazamiento cuando hace falta. Los monitores mantienen sus ?reas de imagen y controles existentes. El men? contextual de los títulos coloca un panel encima, debajo, a los lados o como pesta?a de otro. Se conservan los controles y la sesión. El guardado personalizado elimina la adaptaci?n automática del preset. Antes de restaurar se separa la ventana nativa incrustada para conservarla al cerrar contenedores flotantes.

Logo y Fondo negro remotos usan las mismas acciones de salida que la PC y pueden iniciarla cerrada. La prueba de Logo falló antes del arreglo. El mando incorpora Contenido de la PC con Biblia, Himnario y Videos. El endpoint autenticado `/api/catalog` limita consulta a 200 caracteres y resultados a 100; no publica rutas ni notas privadas. Los resultados bíblicos incluyen el texto y los dem?s solo res?menes. Los comandos usan identificadores previamente resueltos en la PC, nunca rutas recibidas del teléfono. Se conservan autenticación, origen e idempotencia. Las carpetas de video se enumeran fuera del hilo de interfaz.

Comprobaciones específicas: `--panel-sizing` cubre arrastre restringido, cancelación, apilado, accesibilidad de controles, cinco posiciones, persistencia, pestañas, flotación y conservación del reproductor durante un video. `--remote-order` cubre búsqueda de himnos y Biblia, proyección por ID, rechazo de ID desconocido, Logo con salida cerrada y comandos HTTP. Las pruebas del servidor cubren autenticación del cat?logo, validación, l?mite de consulta y comando de biblioteca idempotente. Se comprobó la sintaxis JavaScript con Node. Evidencias: `.tools/worktrees/culto-polymet/.tools/panels-final.log`, `remote-deck-final.log`, `presets-final.log`, `deck-visual-final.log`, `deck-desktop-full.log` y `deck-publish.log`.

Release sin errores ni advertencias: 232 Core, 64 Remote y 138 comprobaciones de escritorio general correctas; paneles 7, presets 9, mando 12 y tema visual 12 correctas.

Las pruebas usan datos temporales. No se sustituyen los datos del usuario ni se cierra su programa en ejecución. No se ha probado este cambio desde un teléfono físico; el render del navegador móvil, HDMI y PowerPoint físico siguen pendientes. La versión 0.30.0 se conserva en su carpeta e iniciador expl?cito.

### Tarjetas, distribuciones y mando — versión 0.30.0

El usuario amplió el alcance con dos capturas: pidió reproducir formas, tarjetas, sombras, bordes, cabecera y mando remoto, además de fuentes visibles a la vez, paneles movibles, tres distribuciones y reset. La implementación parte de `eb9d6a6`, conserva 0.29.0 y se preparó en `codex/polymet-cards-and-remote`. El plan está en `docs/superpowers/plans/2026-11-12-culto-studio-tarjetas-y-mando.md`.

Hay nueve paneles AvalonDock reutilizando los controles existentes. Principal separa Medios, Himnario, Biblia y YouTube; Texto queda inicialmente junto a Himnario. Compacta agrupa las fuentes a la derecha. Proyección las agrupa a la izquierda y aumenta el área de los monitores. Se verificaron identidad de controles/sesión, agrupación y flotación, guardado/restauración, rechazo de XML corrupto y reset. Principal se adapta a ventanas pequeñas únicamente mientras no esté personalizada. Las posiciones nuevas usan `paneles-v2.xml`; `paneles.xml` queda intacto y se puede recuperar desde el menú. Una distribución completa antigua de seis paneles incorpora las tres fuentes nuevas conservando sus grupos y geometría.

Cada panel tiene un contorno continuo, título con separación, esquinas y sombra discreta; los monitores y portadas se recortan visualmente con su radio, sin alterar el contenido enviado. La cabecera muestra nombre y guardado reales, posición actual en el orden y Control remoto. El área de imagen aumenta al ampliar el panel; el desplazamiento secundario conserva al menos 44 DIP para mostrar los controles completos. La búsqueda bíblica conserva la cita completa en las consultas por texto; las tarjetas estrechas ajustan sus acciones sin recortar las etiquetas.

El diálogo de mando usa QR/enlace reales, lista de navegadores autenticados y preview de teléfono. La página local tiene el mismo lenguaje visual en móvil y PC: tarjetas, navegación y accesos con iconos SVG y color de superficie. Conserva los comandos, orden, autenticación Bearer, comprobación de origen e idempotencia. Los dos pesos web de Lexend se sirven desde el propio ejecutable. El texto bíblico/anuncio proyectado se publica en la preview autenticada; los videos y capturas muestran su título/estado, sin añadir streaming de video al teléfono. No se inventan PIN, batería ni nombres personales.

La conexión distingue sesiones autenticadas mediante `X-Culto-Client`, conserva nombres generales del navegador/dispositivo y considera activo un cliente con solicitud en los últimos 15 segundos. Se verificó que solicitudes anónimas y de otro origen no registran clientes. El estado de salida cerrada tiene prioridad sobre un negro anterior, tanto en el snapshot como en la página. El editor nativo conserva la creación y guardado de accesos.

Resultados finales:

| Verificación | Resultado |
|---|---|
| Release | 0 errores/advertencias; 232 Core y 63 Remote correctas |
| Escritorio general completo | 137 comprobaciones, 0 fallos; proceso terminado normalmente |
| `--workspace-layouts` | 9 correctas, incluyendo fixture inicial y ocho pruebas de distribuciones |
| `--visual-theme` | 12 correctas; capturas 1900×1000, 1480×900, 1280×680 y 1200×680 DIP, diálogo y editor |
| `--compact-output`, `--order`, `--content-drag` | 3, 11 y 12 correctas |
| `--bible-tools`, `--youtube-controls`, `--remote-order` | 17, 5 y 11 correctas |
| `--player`, `--output-player`, `--saved-sets`, `--backdrops` | 4, 6, 3 y 8 correctas |
| Layouts sin ventanas de Medios y captura | 6 y 2 correctas |
| Renderer `--capture-to-file` | 19 comprobaciones nativas correctas |
| `--cold-play` | 4 correctas; imagen en 785 ms y mayor intervalo de UI de 36 ms en esa ejecución |
| Script de la web | Sintaxis JavaScript válida; API y fuentes locales verificadas por HTTP |

Se corrigieron tres problemas de los ensayos anteriores: Imágenes seguía comprobándose en el filtro oculto del Himnario en vez de Medios; el escenario remoto de Logo no cargaba ninguna imagen; y el fixture de fondos reutilizaba un `BitmapEncoder` en dos guardados, lo cual lanzaba una excepción fuera de las comprobaciones. Ahora usa un encoder por archivo, el callback registra excepciones como fallo y el ensayo general ejecuta también los bloques posteriores de fondos y medios. No se presentan estos cambios de pruebas como arreglos del motor de reproducción. La excepción reveló además una búsqueda tardía de recursos al desmontar los títulos de panel: los recursos se conservan y las actualizaciones se detienen al cerrar.

Evidencias en `.tools/worktrees/culto-polymet/.tools/`: `reference-desktop-complete.log`, `reference-release-build.log`, `reference-workspace-layouts.log`, `reference-visual-theme.log`, `reference-*.log` de cada selector y `reference-capture-to-file.txt`. Las capturas están en `reference-final`. Se conservan los resultados iniciales y los stacks `reference-desktop-hang-stack.txt` y `reference-desktop-teardown-stack.txt`. Las pruebas usan datos temporales y fuentes propias; no reemplazan biblioteca, Biblia ni orden reales.

Límites: **la revisión automática bloqueó la inspección de Chrome porque no pudo verificar la URL de su ventana**. No se eludió el bloqueo; el render visual real de la web y el teléfono físico siguen pendientes. Las capturas del teléfono son del componente WPF del diálogo, no de un navegador móvil. No se ensayaron HDMI físico, PowerPoint real, presentador USB ni DPI 125 %/150 %. Los motores de audio, video, captura y Move se conservaron.

### Adaptación visual carbón y verde — versión 0.29.0

Se ejecutó el plan aprobado de adaptación a la referencia de Polymet. La recuperación anterior es `c13da65`; el plan se guardó en `c337534` antes de cambiar código. La implementación se preparó en la rama aislada `codex/culto-studio-adaptacion-visual`. Las pruebas usan datos temporales y fuentes sintéticas; no modifican la biblioteca, Biblia, orden ni distribución reales del usuario.

La consola usa Lexend estática 400/500/600/700 incluida como recurso WPF, fondos carbón y verde esmeralda, paneles de 22 DIP y controles redondeados. La procedencia, licencia OFL y SHA-256 de las fuentes figuran en `THIRD-PARTY-NOTICES.md`. Se comprobó primero el fallo de la prueba al exigir Lexend a la versión Poppins, y después la resolución real de las cuatro caras mediante URI `pack`, sin pesos sintetizados. La tipografía y los colores configurados para los textos proyectados permanecen independientes.

Se conservaron las plantillas de AvalonDock, los seis `ContentId`, controles, manejadores y formatos de datos. Se redondea el contorno de cada panel completo sin sustituir sus plantillas de arrastre. La prueba general verifica flotación y reacoplamiento de los seis paneles, agrupación, persistencia de la distribución y conservación de la sesión. Durante la validación se descartó un intento de sustituir el estilo de pane que anulaba el presentador nativo; esa implementación no forma parte de la entrega.

Las capturas de la base y del resultado se guardan en `.tools/worktrees/culto-polymet/.tools/visual-before` y `visual-final`. Se revisó la consola a 1480 × 900, 1280 × 680 y 1200 × 680 DIP. La base también recortaba el pie del orden y reducía demasiado la salida al bajar la altura. Ahora los mínimos de los monitores son 260 DIP y los ajustes antiguos se normalizan antes de crear los paneles; Medios mantiene su mínimo de 260. La salida reserva una fila de al menos 80 DIP para la imagen y desplaza navegación y controles secundarios. Se comprobó además una salida flotante de 240 × 260 DIP, cuyo panel interior mide 230 × 229: imagen, Logo y Negro quedan dentro. El título se abrevia en una línea, con el nombre completo en su tooltip.

Las comprobaciones visuales verifican el encaje de Agregar/Al orden, la proporción 16:9, Lexend, foco y controles nativos, pestañas sin herencia incorrecta de color y contraste del texto preparado/en vivo. El texto sobre verde usa carbón; los metadatos usan un gris más claro y el foco permanece ámbar. Se corrigió también la opacidad del atajo F5 y la prueba calcula su contraste efectivo. En la salida flotante mínima se comprueba cada botón, slider y casilla dentro del viewport real después de desplazarlo; este conserva al menos 44 DIP para mostrar completo el botón de pausa de 40 DIP.

Resultados de escritorio (los recuentos incluyen la comprobación inicial cuando existe):

| Comando/selector | Resultado |
|---|---|
| `scripts/build.ps1` | Compilación sin errores ni advertencias; 232 Core y 61 Remote correctas |
| `--visual-theme` | 11 comprobaciones correctas y tres capturas comparables |
| Ensayo general de escritorio | 101 correctas y 2 fallos preexistentes descritos abajo |
| `--compact-output` | 3 correctas, incluidos controles estrechos y ventana flotante mínima |
| `--order`, `--content-drag` | 11 y 12 correctas |
| `--media-layout-headless`, `--capture-layout-headless` | 6 y 2 correctas |
| `--bible-tools`, `--youtube-controls` | 17 y 4 correctas |
| `--player`, `--output-player`, `--saved-sets` | 4, 6 y 3 correctas |
| `--remote-order` | 9 correctas y el mismo fallo previo de Logo vacío |
| `--backdrops` | Terminó con código 0, pero el registro solo contiene la comprobación inicial; no se considera validación de sus acciones |
| Renderer `--capture-to-file` | 19 comprobaciones nativas correctas |
| `--cold-play` | 4 correctas; primera imagen en 784 ms y mayor intervalo de interfaz de 36 ms en esta ejecución |

El ensayo general de la base 0.28.3, sin cambios, produjo cinco fallos: fixture de carpeta Imágenes, desbordamiento compacto, arranque del MKV sintético al restaurar negro, monitor demasiado pequeño y comando remoto de Logo sin imágenes. En la ejecución final permanecen **Imágenes no muestra su archivo/recuento de prueba** y **Logo remoto sin imágenes falla**. Ambos se reprodujeron antes del rediseño; no se ocultan ni se atribuyen al tema. El fallo de arranque nativo no reapareció en la ejecución final y no se declara corregido por este trabajo visual.

Evidencias en el worktree: `.tools/design-baseline.log`, `design-release029.log`, `design-visual-final.log`, `design-desktop-final.log`, `design-compact-minimum.log` (fallo de Logo recortado), `design-compact-player-red.log` (controles más altos que su scroll), `design-compact-minimum-final.log`, `design-*.log` de cada selector, `design-capture-to-file.txt` y `design-cold-play.log`. La comparación sin cambios está en `.tools/design-original-desktop-baseline.log` del checkout principal. Los tiempos son observaciones de una ejecución, no una garantía de rendimiento.

Límites: no se controló Chrome ni se verificó el YouTube modificado del usuario. No se ensayaron HDMI físico, teléfono, presentador USB, PowerPoint real con animaciones/videos ni las escalas de Windows 125 % y 150 %. La captura nativa usa una ventana sintética propia. Los motores de reproducción, audio, Move, proyección, Core y Remote no se cambiaron en este rediseño. La versión 0.28.3 se conserva por separado para recuperación; no hay migración de datos.

### Primer cambio de captura a archivo — versión 0.28.3

El usuario confirmó que la espera se producía solo en el primer cambio después de abrir el programa. Se reprodujo con una ventana animada propia y salida local fuera del árbol visual: el primer cambio a MKV tardó 19.566 ms; los siguientes necesitaron una sola orden. Una ejecución posterior con archivos ya cargados tardó 580 ms, de los cuales 154 ms correspondieron a crear el motor; estas cifras de distintas ejecuciones no constituyen una comparación controlada de rendimiento.

Ahora una captura visible inicia la preparación de VLC en segundo plano, compartiendo su tarea con la futura solicitud de archivo. La captura continúa mientras tanto; no se abre un decoder ni se reproduce audio adicional. El cierre libera el motor incluso si la preparación termina después. Repetir una solicitud equivalente de video o audio pendiente conserva su decoder y actualiza la intención de pausa. Un archivo distinto o una configuración diferente siguen sustituyendo la solicitud anterior.

Se comprobaron dos regresiones rojas antes de corregirlas: repetir una solicitud retiraba el decoder que esperaba su primer fotograma y la captura no preparaba el motor antes del primer archivo. El selector `--capture-to-file` pasa 19 comprobaciones con captura real de un proceso sintético separado, primera carga, tres cambios posteriores y repetición de órdenes durante la espera del primer fotograma. Pasaron también las 41 comprobaciones de transiciones y las cuatro del ensayo de arranque, pausa, cierre y última solicitud. El ensayo de arranque agotó inicialmente su espera de ocho segundos en la prueba de pausa; una segunda ejecución completa pasó, con 47 ms de intervalo máximo entre actualizaciones de interfaz y video en 869 ms. Se conservan ambos resultados. Falta ensayar el cambio con el navegador y archivos concretos del usuario; no se controló Chrome ni se reinició su aplicación.

Evidencias: `.tools/capture-to-file-baseline.txt`, `.tools/capture-to-file-red.txt`, `.tools/capture-to-file-warmup-red.txt`, `.tools/capture-to-file-final.txt`, `.tools/capture-switch-smooth-regression.txt`, `.tools/capture-switch-cold-initial.log` y `.tools/capture-switch-cold-regression.log`. La compilación y las pruebas del núcleo y mando quedan registradas en `.tools/release0283.log`.

### Recorte persistente de YouTube — versión 0.28.2

El recorte y la transformación visual se guardan como ajuste de YouTube, sin identidad de ventana ni opciones de audio o cursor. Las ventanas nuevas y los elementos antiguos sin recorte heredan este ajuste; se respetan los recortes propios de cada fuente o elemento del orden. Al cargar datos antiguos se recupera un único ajuste visual existente en las fuentes o el orden; si hay ajustes distintos no se elige uno arbitrariamente. Guardar en el editor y añadir al orden persisten el ajuste, con reversión si falla el guardado. No se modificaron los datos reales del usuario.

La lista recupera una única fuente guardada tras cerrar Chrome y abrir un video de título distinto, únicamente si hay una sola ventana compatible y se confirma que desapareció la identidad original. Una ventana oculta, en otro escritorio o cuyo proceso no puede consultarse no se considera cerrada. Las fuentes ambiguas se eligen manualmente. El editor conserva el recorte al asociarlo a la identidad actual y no lo restablece en la siguiente actualización. La salida, vista previa y restauración desde negro/Logo aplican el mismo ajuste.

Pasaron 12 pruebas sin ventanas de descubrimiento, recortes independientes, reconexión, identidad oculta, migración y persistencia en disco, además de dos pruebas del editor con controles desconectados y sin captura. La regresión de identidad oculta falló antes de aplicar la protección. Compilación sin errores ni advertencias, 232 pruebas del núcleo y 61 del mando correctas. Evidencias: `.tools/youtube-layout-red.log`, `.tools/youtube-hidden-red.log`, `.tools/youtube-layout-final.log`, `.tools/youtube-layout-editor.log` y `.tools/release0282.log`. Esta entrega se preparó sin controlar el escritorio ni reiniciar la aplicación; falta comprobar el recorte real del navegador en la nueva versión. El recorte previamente perdido no puede recuperarse de datos que contienen todos sus valores en cero.

### Compatibilidad de captura al maximizar — versión 0.28.1

Se observó con el navegador real que al maximizar Culto Studio la región de video de Chrome quedaba negra, mientras su interfaz seguía capturándose; al restaurar Culto volvía el video. Se añadió una compatibilidad temporal en la ventana de la consola durante capturas de Chromium para que no se considere completamente opaca en la detección de oclusión. Conserva el marco, los controles nativos y la recepción de clics; se retira al terminar la captura. Incluye notificación de cambio de estado para invalidar el cálculo de oclusión del navegador.

Las cuatro comprobaciones nativas del estilo, entrada, restauración y transición pasaron, junto con un ensayo de captura de una ventana sintética durante tres ciclos de maximizar/restaurar. Evidencias: `.tools/browser-compat-final.log`, `.tools/browser-compat-publish-final.log` y `.tools/local-capture-baseline.txt`. El usuario interrumpió el control con Esc antes de terminar el ensayo final con YouTube: **sigue pendiente confirmar que esta compatibilidad resuelva el negro del navegador real**. La corrección se conserva en 0.28.2.

### Punto de recuperación antes del rediseño

Se conserva la versión 0.28.0 con sus funciones actuales como base para los próximos cambios de diseño y organización. Antes del commit se ejecutó `scripts/build.ps1`: compilación sin errores ni advertencias, 232 pruebas del núcleo y 61 del mando correctas. Evidencia local: `.tools/pre-redesign-checkpoint.log`.

Pendiente conocido: el usuario sigue observando que la captura de YouTube queda negra al maximizar Culto Studio. Se comprobó una captura negra en Chrome, pero no se completó el diagnóstico ni se aplicó una corrección. Las pruebas con ventanas sintéticas de versiones anteriores no validan este caso específico del navegador.

### Arrastre visual y reordenación — versión 0.28.0

Al arrastrar contenido aparece una tarjeta junto al cursor con título, miniatura y acción Añadir/Mover. Reutiliza la caché de miniaturas sin abrir un reproductor. La tarjeta no toma foco ni intercepta el destino y se retira al soltar, cancelar o cerrar. Esc deja el orden y la salida intactos.

Orden de culto y Secuencia admiten arrastre para mover la misma instancia, conservar su identificador y guardar el nuevo orden. Secuencia acepta también contenido de Himnario, Biblia, Medios y YouTube, con marcas de inserción entre tarjetas y en filas envueltas. Añadir desde una fuente crea una copia independiente. El desplazamiento al acercarse a los extremos permite alcanzar otros elementos. Soltar tras mover no reproduce ni abre el menú del elemento; se conservan el clic de previa y el doble clic de proyección.

La prueba nueva de Secuencia falló antes de implementar el cambio: «Sequence press did not arm its card for dragging». Después pasan las 12 comprobaciones de `--content-drag` y las 11 de `--order`, contando la comprobación inicial de cada ejecución. Cubren movimientos en ambas direcciones, persistencia e identidad, sincronización de paneles, geometría de las tarjetas, arrastre de fuentes, tarjeta con título/miniatura, Escape y reproducción posterior. Evidencias: `.tools/drag-red.log`, `.tools/drag-preview.log` y `.tools/drag-order-regression.log`. Los ensayos usan datos temporales.

Compilación y publicación 0.28.0 sin errores ni advertencias; 232 pruebas del núcleo y 61 del mando correctas. Se abrió la distribución publicada y se probó el arrastre real en Orden del culto y Secuencia, en ambos sentidos, sin proyectar contenido. Al terminar se verificaron los 11 identificadores y posiciones originales del culto. El programa quedó abierto en 0.28.0 y los iniciadores habitual, 0.25, 0.26 y 0.28 apuntan a esta entrega. Evidencia adicional: `.tools/release028.log`.

### YouTube, reconexión y audio conservado — versión 0.27.0

La captura y sus acciones pasan a una pestaña YouTube. Al abrir, enumera ventanas de Chrome cuyo título contiene YouTube, sin iniciar captura, preparar contenido, proyectar ni modificar audio. Entrar en la pestaña actualiza la lista; mientras está abierta se actualiza cada 2,5 s. Las fuentes guardadas conservan recorte y transformación al recuperar una identidad exacta o un título único compatible; las ambiguas permanecen independientes. Las capturas antiguas de biblioteca migran a YouTubeSources conservando sus identificadores y sin vincular las copias del orden. Se elimina la pausa de capturas en consola y reproductor flotante; el teléfono publica canPause=false y rechaza esos comandos. Los archivos conservan sus controles.

Se reprodujo el fallo permanente al minimizar/restaurar una fuente propia: el motor trataba la falta temporal de ventana/WGC como fatal y la superficie anulaba el cliente, quedando pausada. Ahora se retiene la última imagen y se recupera el trabajador con espera acotada entre intentos, manteniendo la misma ventana/proceso/arranque/clase/ejecutable. La pérdida definitiva de identidad sigue siendo terminal. Un audio ya establecido mantiene su ganancia durante la recuperación visual. Maximizar una ventana sintética que cubre la fuente ya pasaba antes; se distingue ese caso del fallo de minimización reproducido.

Mantener audio es optativo y persistente. Conserva la misma fuente en ejecución al mostrar texto, Biblia, imagen, Logo o negro. Volver al video y F9 son una acción de regreso, sin alternancia: reusan la fuente y su posición. Otro archivo o captura sustituye la fuente conservada mediante fundido; desactivar la opción retira el fondo sonoro con fundido sin bloquear. Las comparaciones de fuente ignoran únicamente los cambios de título de la ventana, conservando los demás ajustes. Con la opción desactivada, las capturas restauradas de negro/Logo vuelven a imagen y audio automáticamente, ya que no existe una falsa pausa en la interfaz; los archivos de video mantienen su restauración pausada anterior.

Verificación: siete pruebas sintéticas de descubrimiento/migración, tres pruebas WPF de controles YouTube/audio/regreso más la comprobación inicial, 50 comprobaciones nativas de medios conservados, 67 de captura/reconexión/retención, 41 de mezcla de videos y 14 de fundido. La captura nativa incluye 17 s minimizada, cierre del trabajador propio, cambio de título sin reemplazo y cambio de recorte con reemplazo, cancelación, retirada y restauración de negro/Logo. Pasaron también las pruebas de panel estrecho, reproductor flotante y arranque asíncrono. El ensayo de persistencia usa un video sintético extendido temporalmente mediante ffmpeg para que el final del archivo no invalide la medición de volumen; la aplicación no incorpora esa dependencia.

Evidencias: `.tools/youtube-sources.log`, `.tools/youtube-controls.log`, `.tools/youtube-cold-regression.log`, `.tools/youtube-compact-regression.log`, `.tools/youtube-player-regression.log`, `.tools/persistent-result.txt`, `.tools/capture-recovery-result.txt`, `.tools/capture-title-red.txt`, `.tools/capture-backdrop-red.txt` y `.tools/capture-restore-video-regression.txt`. Los ensayos usan fuentes propias y datos temporales. La suite antigua de fondos conserva un fallo previo de color neutro cuando falta la imagen de Logo; no se cambió ese comportamiento en esta entrega.

### Transformación visual de capturas — versión 0.26.0

Los campos de recorte se sustituyen por un lienzo 16:9 con ocho tiradores. Arrastrar mueve o escala conservando proporciones; Alt recorta en píxeles de la fuente, Shift permite escala independiente y Ctrl desactiva el ajuste a bordes y centro. Los botones y el menú contextual permiten ajustar, estirar, centrar y restablecer; los atajos Ctrl+F/S/D/R actúan con el foco en el lienzo. Las flechas permiten mover con teclado y Esc cancela un arrastre. Se siguió la referencia oficial https://obsproject.com/kb/keyboard-shortcuts.

Durante la edición se mantiene una sola captura sin recorte: el lienzo aplica la transformación localmente y no reinicia el trabajador al mover el ratón. La posición y el tamaño se guardan como coordenadas normalizadas, junto al recorte original, y el motor gráfico aplica ambos al producir la salida. Se conservan las capturas antiguas sin transformación, ajustadas automáticamente. El editor de biblioteca abre ahora el mismo editor visual para fuentes de ventana.

Pasaron cinco pruebas geométricas de recorte, recuperación del borde, escala, proporción, Shift, Ctrl, límites y serialización. El selector WPF aprobó sus cinco comprobaciones más la inicial de independencia; incluye controles dentro de 760 × 540, ausencia de campos de píxeles y transformación sin reiniciar la captura. El ensayo nativo aprobó quince comprobaciones: además de oclusión y recorte, verifica posición, escala independiente y negro fuera del rectángulo de salida. Evidencias: `.tools/capture-transform-tests.log`, `.tools/capture-canvas-picker.log`, `.tools/capture-canvas-native-result.txt` y `.tools/capture-canvas-native.log`.

Compilación y publicación completadas con cero advertencias y errores; pasaron las 232 pruebas de Core y 60 de Remote (`.tools/release026.log`). Se abrió 0.26 y se eligió una ventana sintética propia, animada y silenciosa, en Capturar ventana. Se observaron los ocho tiradores, se redujo el tamaño con arrastre físico y se probaron Ctrl+S y Ctrl+R; la captura continuó cambiando de color durante la edición. Los modificadores Alt/Shift/Ctrl se verificaron mediante la geometría automatizada; no se simuló físicamente su combinación con arrastre. Se canceló sin añadir la prueba al orden y se conservaron sus once elementos y los 620 contenidos actuales de biblioteca.

### Primera reproducción sin bloquear la interfaz — versión 0.25.2

Se reprodujo en la distribución 0.25.1: tras abrir y proyectar el primer himno, Windows marcó el proceso como no respondiente y se recuperó después. Las muestras de dotnet-stack 7–14 mantuvieron el hilo de interfaz dentro de LibVLC..ctor → ProjectionSurface.CreatePlaybackEngine → ProjectionMixer.Load. El lector de medios y la mezcla no habían comenzado: la espera estaba en la inicialización nativa de VLC. Evidencias locales: `.tools/cold-live-stack-*.txt`. Las pruebas desde el directorio de compilación no reprodujeron esa espera larga, por lo que se comprobó también el ejecutable publicado.

El mezclador inicializa su motor compartido mediante Task.Run y continúa la petición en el Dispatcher al terminar. Comprueba si la solicitud sigue vigente antes de reproducir; conserva cambios de selección, pausa y cierre durante la espera. Un fallo permite reintentar y conserva la salida anterior. Cerrar durante la inicialización libera el motor cuando termina sin esperar desde la interfaz. La barra de estado indica Preparando reproductor durante esa carga.

La prueba `--cold-play` fuerza una inicialización nativa pendiente mediante una puerta de prueba y verifica respuesta del Dispatcher, descarte de una solicitud reemplazada, pausa durante la carga y cierre sin esperar. El ensayo de primera reproducción también mide los latidos del Dispatcher: antes registró 396–413 ms de pausa desde el directorio de compilación; después registró 34–45 ms. La reproducción real del himno 001 con copia temporal de la configuración se verificó en tres procesos independientes antes del cambio. Evidencias: `.tools/cold-async-red.log`, `.tools/cold-async-tests.log`, `.tools/cold-order-regression.log`.

Pasaron las 41 comprobaciones nativas de `--smooth-transitions`, las once del selector de orden, 227 pruebas de Core y 60 de Remote. El ensayo de mezcla ahora espera la inicialización nativa por separado: su plazo de diez segundos para video comienza después de preparar el motor. La primera ejecución agotó ese plazo porque incluía también la carga nativa que antes bloqueaba de forma síncrona fuera de la medición; la ejecución final completa pasó. Evidencias: `.tools/cold-smooth-result.txt` y `.tools/release0252.log`.

Se abrió la nueva distribución 0.25.2 y se proyectó el primer himno desde el orden. La consulta de interfaz volvió en menos de un segundo mientras la salida todavía esperaba imagen; posteriormente se observó el video avanzar a 00:09 con duración 01:59 y los controles respondieron. Se conservan los diez elementos actuales del orden y la salida HDMI permanece desactivada. Los iniciadores general y 0.25 apuntan al parche 0.25.2. Esta comprobación visual no equivale a una prueba de HDMI físico ni de todos los equipos y archivos.

### Tecla Supr en el orden — versión 0.25.1

Supr/Delete elimina el elemento seleccionado cuando el foco está dentro de la lista del orden. Reutiliza la acción de quitar del orden y su persistencia; conserva biblioteca, archivos y salida actual. La repetición automática de una tecla mantenida no elimina elementos sucesivos. Los campos de búsqueda y edición conservan su comportamiento de teclado.

Se verificó primero el fallo por ausencia del atajo y después pasó el selector WPF `--order`, incluida la nueva comprobación de eliminación persistida y aislamiento del campo de búsqueda, con cero fallos. En la primera ejecución también falló una comprobación previa de reproducción de video; la ejecución final completa aprobó las once comprobaciones del selector. Evidencias: `.tools/order-delete-red.log`, `.tools/order-delete-tests.log` y `.tools/release0251.log`. Los ensayos usaron datos temporales.

### Reproductor de salida flotante — versión 0.25.0

El doble clic en el monitor En pantalla abre una única ventana redimensionable. Usa el mismo compositor mediante OutputMonitor y no crea otro decodificador ni otra sesión de audio. Sus controles actúan sobre la salida de la consola: pausa, reinicio, avance, posición, volumen, silencio y repetición. La captura en directo expone congelar/continuar y deshabilita la búsqueda temporal. F11 o doble clic alterna pantalla completa en el monitor donde está el reproductor; Esc restaura los límites anteriores. Fijar controla si permanece encima. La ventana no depende de la minimización de la consola y se cierra con la aplicación.

Se ejecutó primero una prueba que falló por la ausencia de OpenOutputPlayer. Las cinco pruebas posteriores de `--output-player` pasaron: instancia única y compositor compartido, pausa/búsqueda nativa sin reanudar, volumen y pantalla completa, controles dentro de 480 × 320 y cambio de contenido/cierre sin detener salida. Pasaron también las tres regresiones de `--player`. Ambos selectores incluyen además la comprobación inicial de independencia entre biblioteca y orden. Evidencias: `.tools/output-player-red.log`, `.tools/output-player-tests.log` y `.tools/output-player-regression.log`. Las pruebas usan un video sintético y un directorio de datos temporal, conservando el orden del usuario.

La compilación y publicación terminaron correctamente con 227 pruebas de Core y 60 de Remote aprobadas (`.tools/release025.log`). Se abrió 0.25 y se reprodujo el himno 001 del orden real, sin activar HDMI. El doble clic físico sobre En pantalla abrió el reproductor; se observó el avance del video, sus proporciones y las franjas negras. Otro doble clic amplió a pantalla completa y Esc restauró el tamaño anterior. El reproductor quedó abierto en pausa, con los 12 elementos del orden conservados. No se probaron nuevas funciones de PowerPoint ni salida física HDMI en esta entrega.

### Captura de ventana y audio de aplicación — versión 0.24.0

Se incorpora una fuente de ventana al orden y a la biblioteca. El selector permite elegir explícitamente una ventana abierta, comprobar su imagen y configurar recorte, cursor y audio. No se selecciona ni captura ninguna ventana automáticamente. El motor aislado usa Windows Graphics Capture mediante libobs-winrt del runtime OBS incluido; el compositor recibe fotogramas BGRA y comparte el resultado entre los monitores del programa y la salida.

Pasaron 227 pruebas de Core y 60 de Remote. El ensayo nativo de captura verificó 12 condiciones con una ventana sintética propia: fotogramas cambiantes, captura al cubrir la fuente con otra ventana, recorte, franjas negras, cancelación, cierre de fuente y retirada del proceso auxiliar. El ensayo de integración verificó 26 condiciones con una fuente animada y audible en otro proceso: preparación después de un fallo, coincidencia de los monitores, audio sin duplicación, congelar/continuar, negro/restaurar, cambio a video, retirada silenciosa y conservación de la salida anterior cuando falla una captura entrante. También pasó la regresión nativa `--smooth-transitions` de videos.

El controlador de audio actúa sobre las sesiones Windows del proceso seleccionado y sus descendientes; no reproduce una segunda copia ni cambia el volumen maestro. Sus 13 comprobaciones nativas verificaron ganancia, suma de capturas del mismo proceso, silencio al retirar, restauración y conservación del volumen de otra aplicación. NAudio.Wasapi 2.2.1 se incorpora ahora a producción, con su licencia MIT. El ensayo WPF del selector pasó sus cuatro comprobaciones y la comprobación inicial de independencia de biblioteca y orden.

Evidencias locales: `.tools/window-capture-native.log`, `.tools/capture-integration-result.txt`, `.tools/capture-integration-native.log`, `.tools/captured-application-audio.log`, `.tools/window-capture-picker.log`, `.tools/capture-video-regression.log` y `.tools/release024.log`. Las capturas WASAPI sintéticas se excluyen de Git. La publicación completa terminó correctamente; el ajuste visual de contraste se publica por separado en `.tools/release024-visual.log`.

Se abrió la distribución 0.24 y se comprobó visualmente Biblioteca → Capturar ventana: lista sin selección inicial, vista previa vacía, controles de recorte, audio activado y botón de añadir deshabilitado hasta recibir imagen. Se conservan los 12 elementos del orden, los 619 contenidos y los 603 archivos de Medios. No se añadió ninguna ventana personal al orden para la prueba.

Límites: la fuente debe permanecer abierta y sin minimizar. Congelar captura detiene su imagen y silencia su audio, pero no pausa el reproductor de YouTube. El audio abarca otras pestañas o ventanas del mismo proceso del navegador. Al retirar una captura, ese navegador permanece silenciado hasta volver a usarla, devolver el audio desde Biblioteca o cerrar Culto Studio. Las transiciones que involucran captura y tienen Move seleccionado usan disolución para mantener imagen en movimiento. Se probó el motor con fuentes sintéticas propias; no se realizó una reproducción directa de YouTube ni una prueba en las pantallas HDMI o la consola Yamaha.

### Audio independiente y fundido continuo — versión 0.23.0

Se reprodujo la interferencia mediante captura WASAPI del dispositivo «Altavoces (4- Realtek(R) Audio)», sin micrófono. Dos tonos sintéticos independientes permiten medir cada reproductor. Antes de corregir, precargar el segundo a volumen cero redujo la amplitud del primero al 71,5 % y volver a silenciarlo la redujo al 61,3 %, con fluctuaciones y fuga del entrante. La causa es el control compartido de volumen/silencio del módulo mmdevice de VLC. La fábrica de reproductores ahora selecciona DirectSound, cuyo volumen se aplica al búfer individual, sin guardar ese volumen entre reproductores.

La segunda regresión capturada fue una caída de la suma de amplitudes al 25,9 % durante el fundido, incluso con los reproductores ya aislados. Se compensa la curva cúbica nativa de VLC únicamente en la ganancia del fundido, conservando el volumen maestro. Las capturas finales de 250 y 1200 ms mantuvieron la suma normalizada entre 0,94 y 1,05. El ensayo incluye la carga del entrante, el solapamiento y la retirada del saliente; comprueba también la cobertura temporal para no ocultar silencios por paquetes ausentes. No equivale a certificar todos los archivos ni otros dispositivos de audio.

Pasaron las suites nativas `--audio-isolation`, `--audio-crossfade`, `--audio-fades` y `--smooth-transitions`, además de 216 pruebas de Core y 60 de Remote. El ensayo de video verifica fotogramas y tiempo avanzando durante la mezcla, negro/fondo/restauración, fallo de carga, cambios rápidos, pausa, reinicio y fin natural. La dependencia NAudio.Wasapi se limita al proyecto de pruebas; no se incorpora a la aplicación.

Evidencias locales: `.tools/audio-isolation-before.json`, `.tools/audio-crossfade-before.json`, `.tools/audio-isolation-final.txt`, `.tools/audio-crossfade-final.txt`, `.tools/audio-fades-final.txt`, `.tools/smooth-transitions-final.txt` y `.tools/release023.log`. Las grabaciones y mediciones finales se generan en `tests/Culto.Renderer.Smoke/fixtures/audio-*-loopback.wav` y `audio-*-measurements.json`, excluidas de Git. Los selectores WASAPI se ejecutan explícitamente porque necesitan salida de audio y las demás aplicaciones en silencio.

Referencias de implementación: [VLC mmdevice](https://raw.githubusercontent.com/videolan/vlc/3.0.x/modules/audio_output/mmdevice.c) y [VLC DirectSound](https://raw.githubusercontent.com/videolan/vlc/3.0.x/modules/audio_output/directsound.c). Pendiente ensayo en la salida Yamaha y pantallas físicas de la iglesia.

Se cerró normalmente la instancia 0.21 y se abrió la distribución 0.23. En su interfaz se reprodujo el himno 001, se cambió al 002 mediante doble clic, después a un MP4 desde Medios y finalmente a Negro. Se observó la reproducción progresar y el nuevo contenido en ambos monitores. Se conserva el orden de 12 elementos, la biblioteca de 619 contenidos y los 603 archivos de Medios; la aplicación quedó abierta, en negro y pausada, con HDMI desactivado. Esta comprobación visual de archivos del usuario se distingue de la medición WASAPI con señales sintéticas descrita arriba.

### Logo con imágenes automáticas — versión 0.22.0

Fondo iglesia se renombra Logo. Según la aclaración del usuario, clic izquierdo muestra inmediatamente la presentación con disolución y clic derecho abre su editor. La lista anterior se conserva. El editor trabaja con copias y permite agregar, quitar, ordenar, elegir imagen inicial y configurar permanencia (1–3600 s) y transición (100–3000 ms), inicialmente 10 s y 300 ms. Guardar persiste las opciones; Cancelar no modifica la configuración.

`LogoSlideshow` es independiente de WPF y recibe el reloj monótono del controlador. Espera la finalización de cada transición antes de medir la permanencia, repite en orden, conserva una imagen fija y descarta muestras atrasadas. El controlador detiene el timer al proyectar otro contenido, poner negro o cerrar; conserva los medios pausados para restaurarlos. También contempla el primer envío que espera un fundido de PowerPoint, clics repetidos durante esa espera y archivos ausentes. Las franjas de las imágenes del logo son negras.

Se añadieron **12 pruebas puras** de opciones, ciclo, tiempo de transición, snapshots y parada. Las pruebas WPF de Logo, editor y controles se actualizaron y compilaron, pero **la presentación real, los clics y la apariencia del editor siguen pendientes de ensayo visual**. No se abrió ni reinició la aplicación. Evidencia de compilación, pruebas sin ventanas y publicación: `.tools/release022-headless.log`.

### Explorador de medios inferior — versión 0.21.0

Se trasladaron las carpetas de Windows a un panel Medios junto a Secuencia. Incorpora lista, galería, búsqueda, orden por nombre, carpeta personalizada y preferencias persistentes. Comparte la preparación, proyección y arrastre al orden existentes; PowerPoint no recibió cambios. El layout anterior migra de cinco a seis paneles sin sustituir posiciones ni tamaños guardados. La galería reduce sus tarjetas y oculta la ruta cuando falta altura. La consulta de diseño no proporcionó una referencia específica de explorador WPF; se siguieron los estilos existentes y controles nativos.

Las miniaturas se obtienen en un hilo STA independiente mediante Windows Shell, con cola de 64 y caché de 128 imágenes. La ausencia de un proveedor de miniaturas deja icono y título; los controles reciclados cancelan solicitudes anteriores y los bitmaps se congelan antes de usarlos en WPF. No se crean reproductores extra.

Se añadieron seis pruebas puras de migración y seis de geometría. El arnés **`--media-layout-headless`** mide y organiza controles desconectados, sin crear Application, Window, fuentes HWND ni un bucle de interfaz. Sus **seis comprobaciones** verifican 10.000 elementos, número acotado de tarjetas, reutilización real de contenedores, scroll al final, panel estrecho, altura compacta, ScrollIntoView y cambios de colección. Detectó y permitió corregir un fallo de inicialización del generador en el primer Measure.

Compilación y publicación completadas sin errores ni advertencias. Pasaron **204 pruebas del núcleo, 60 del mando y seis del arnés de galería: 270 comprobaciones sin ventanas**. Las pruebas WPF de carpetas, preferencias, arrastre, distribución y Secuencia se actualizaron y compilaron. **Quedan pendientes los clics físicos, la apariencia completa y la extracción real de miniaturas de los archivos de esta PC**; no se abrió ni reinició el programa. Evidencia de compilación y publicación: `.tools/release021-headless.log`. Evidencia del arnés de controles sin ventanas: `.tools/media-layout-headless.log`.

### Franjas negras en videos — versión 0.20.1

La superficie de video usaba el fondo general RGB(16,23,34), visible en las franjas de los archivos que no llenan la salida. Ahora usa negro puro en la reproducción de video y conserva `Stretch.Uniform`. El monitor y la vista previa reflejan esa misma superficie. Los fondos elegidos para Biblia y Fondo iglesia conservan sus estilos. Compilación y publicación sin interfaz registradas en `.tools/release0201-headless.log`; la comprobación visual sigue pendiente, sin abrir ni reiniciar el programa.

### Mezcla continua y tiempos rápidos — versión 0.20.0

La salida utiliza un compositor estable con dos superficies temporales durante el cambio. La anterior continúa reproduciéndose mientras la entrante carga sin sonido; el primer fotograma inicia el reloj compartido de disolución y fundidos de audio. Corte, Disolución, Desplazamiento y Zoom usan esta ruta. La liberación del reproductor retirado se hace fuera del hilo de interfaz, tras retirarlo de la composición y detener sus temporizadores; el buffer se libera después del reproductor nativo. Vista previa, monitor local y HDMI siguen compartiendo la misma salida.

Los valores iniciales son **300 ms visuales y 250 ms para salida/entrada de audio**, editables en pasos de 50 ms con botón Restablecer. Se migra una sola vez la combinación antigua predeterminada 500/1500/1500; otras combinaciones se conservan. Cada solicitud captura sus ajustes. Las duraciones se ejecutan simultáneamente, y la mezcla termina cuando termina la más larga, sin sumar los tiempos. Negro y fondo conservan el reproductor pausado para restaurarlo; los cambios rápidos conservan la última solicitud. Un error al cargar mantiene el contenido anterior y recupera la identidad de salida en la sesión sin borrar la selección preparada.

La solución compila sin errores ni advertencias. **192 pruebas del núcleo y 60 del mando aprobadas: 252 en total**. Las nuevas pruebas puras cubren temporización simultánea, duración cero, límites, migración de ajustes y recuperación de sesión. La publicación autónoma y las comprobaciones sin interfaz se registran en `.tools/release020-headless.log`.

Se compiló un nuevo ensayo nativo `Culto.Renderer.Smoke --smooth-transitions` para comprobar fotogramas y tiempo del video saliente durante la mezcla, volúmenes simultáneos, restauración tras fondos, carga fallida, reemplazos rápidos y controles pendientes. Se actualizaron las pruebas del diálogo de ajustes y el acceso a la superficie activa. **No se ejecutaron estos ensayos con ventanas, no se reinició el programa y no se verificó aún la fluidez visual ni audible en esta PC**, respetando la restricción del usuario.

**Límites:** Move continúa usando capturas estáticas con su complemento nativo; no recibe fotogramas vivos de ambos videos. PowerPoint conserva su ruta propia de reproducción y audio. Estas rutas no se presentan como verificadas por las pruebas de la nueva mezcla VLC.

### Pasajes agrupados y edición de referencias — versión 0.19.0

Los rangos escritos seleccionan todos sus versículos; los resultados usan `SelectionMode.Extended` para Shift+clic y Shift+flechas. El grupo se convierte en un único `Cue`, con el texto íntegro del catálogo, numeración y referencia compacta. La selección conserva orden canónico e identidad estable mientras no cambie; la navegación reemplaza su tramo en la lectura por el contenido agrupado. Añadir o arrastrar copia un solo elemento al orden. Los gestos con Shift/Ctrl no activan la captura de arrastre; pulsar un miembro de un grupo conserva su selección para arrastrarlo o proyectarlo con doble clic.

El editor reconoce las posiciones de libro, capítulo y versículos de una cita, incluidos alias, nombres numerados y rangos. Clic, foco y Tab seleccionan las secciones sin cambiar la consulta ni proyectar. Las consultas por palabras conservan el comportamiento normal.

Se añadieron **28 pruebas puras**: siete de composición de pasajes y 21 de análisis de referencias; aprobaron tras comprobar fallos previos a la implementación. La suite del núcleo aprobó **166 pruebas**, y el mando **60: 226 en total**. La solución compila sin errores ni advertencias y la publicación autónoma terminó con código 0. Se añadieron cuatro pruebas WPF de integración para selección, proyección, navegación y copia al orden, pero **los gestos físicos de Shift, el editor y la proyección visual quedan pendientes de ejecución** por la restricción de control de escritorio. El registro de compilación, pruebas sin interfaz y publicación está en `.tools/release019-headless.log`.

### Proyección bíblica con Enter y doble clic — versión 0.18.1

Se conectaron Enter en el buscador, Enter en los resultados y doble clic izquierdo sobre un versículo con la misma acción de proyectar que utiliza el resto de la consola. Se comprueba que el resultado pertenezca a la búsqueda vigente, conservando la salida ante consultas inválidas o sin resultados. La escritura, selección simple y navegación entre capítulos siguen preparando sin proyectar. Se conserva la lectura bíblica completa para Siguiente/Anterior y se ignora la repetición automática de Enter.

Se actualizaron y ampliaron las pruebas WPF para comprobar el versículo realmente pulsado aunque otra fuente esté preparada, búsquedas inválidas, doble clic en espacio vacío/derecho y Enter en la lista. Estas pruebas de interfaz quedan pendientes de ejecución para respetar la restricción de control de escritorio. La compilación, pruebas sin interfaz y publicación se registran en `.tools/release0181-headless.log`.

### Vista previa compartida y acciones del orden — versión 0.18.0

La vista previa comparte el compositor de salida cuando no hay otro contenido preparado. Preparar video/audio muestra una portada sin crear un reproductor VLC; se retiraron todos los controles independientes. «Ver salida» recupera el reflejo sin enviar de nuevo ni reiniciar. Preparar contenido editado con el mismo ID conserva la distinción frente a la copia que está en vivo. El clic izquierdo muestra Reproducir/Eliminar debajo del orden, reservando espacio para no desplazar filas durante un doble clic.

Compilación de la solución sin errores ni advertencias. **138 pruebas del núcleo y 60 del mando aprobadas: 198 en total**. Las cinco pruebas nuevas cubren seguimiento, preparación, cambio del orden, negro/fondo, regreso sin reproyectar e independencia de las copias. Se actualizaron las comprobaciones WPF de orden, reproductor, fondos y controles; se compilaron, pero no se ejecutaron ventanas ni controles de escritorio en esta entrega. La publicación autónoma finalizó con código 0 mediante `scripts/build.ps1 -Publish -OutputDirectory artifacts\CultoStudio-0.18.0`; registro en `.tools/release018-headless.log`.

PowerPoint sigue usando una sola presentación. La vista previa local intenta un thumbnail DWM del HWND raíz recortado a la región del monitor de salida, porque DWM no permite un HWND hijo como origen. Se verifican los resultados nativos, los cambios de ventana al desacoplar y el solapamiento para evitar realimentación; ante rechazo se muestra «PowerPoint · ver En pantalla». **El recorte local, la imagen de Office y la interacción real siguen pendientes de validación visual.** El espejo de videos/textos usa directamente el mismo `ProjectionSurface` mediante `VisualBrush`, sin capturar otras aplicaciones.

Esta entrega conserva las limitaciones nativas de Move y audio de PowerPoint enumeradas para 0.17. El programa abierto no se reinició ni se modificaron los datos habituales para verificar 0.18.

### Motor Move y PowerPoint — versión 0.17.0, verificación sin interfaz

Se preparó `artifacts/CultoStudio-0.17.0` por separado, sin reemplazar ni reiniciar la aplicación abierta. `scripts/build.ps1 -Publish -OutputDirectory artifacts\CultoStudio-0.17.0` terminó con código 0, sin advertencias ni errores. Pasaron **133 pruebas del núcleo y 60 del mando: 193 en total** (`.tools/release017-headless.log`). Los proyectos de escritorio y reproductor se compilaron, pero sus pruebas con ventanas no se ejecutaron por indicación del usuario.

La entrega contiene un host privado sin ventana para el complemento **Move Transition 3.2.1** y **libobs 33.0.1**. Se verificaron los hashes de los ZIP oficiales, los 265 archivos extraídos, arquitectura AMD64 y exports de todos los enlaces nativos usados por el host, leyendo bytes sin cargar DLLs (`.tools/obs-move-static-exports.log`). Las pruebas puras cubren framing del pipe, lecturas parciales, límites de tamaños, escrituras concurrentes, estructura ABI y barreras de timestamp que descartan fotogramas anteriores.

El fundido de PowerPoint usa una envolvente sobre `MediaFormat.Volume` de los medios de la copia propia. Las pruebas cubren cancelación, restauración, solicitudes reemplazadas y conservación de pausa/reinicio/negro y de ajustes capturados durante el cambio. La supresión de barras solo acepta una ventana atribuible al HWND propio por su cadena de propietarios; su política se prueba sin llamadas Win32.

**Pendiente de ejecución:** carga/renderizado real de Move con la GPU, efecto audible de `MediaFormat.Volume` durante reproducción y propiedad/desaparición de la barra flotante de PowerPoint. El host no se ejecutó, las DLLs no se cargaron y no se abrió ni controló ninguna aplicación para esta verificación. Move trabaja con dos capturas completas del compositor; no se presenta como un editor de escenas OBS ni como compatibilidad con todos los filtros del paquete. Detalles: [integración nativa y límites](obs-move-native.md).

### Biblia, transiciones, cultos guardados y PowerPoint — versión 0.16.0

El editor bíblico permite elegir imagen o color de fondo, oscurecimiento, fuente, tamaño, color, negrita/cursiva, alineación y sombra/brillo. Su vista previa usa el mismo renderizador y cancelar no modifica la salida. Guardar mantiene el contenido en vivo hasta el siguiente envío. La búsqueda admite abreviaturas, citas separadas por espacios, capítulos y frases, con actualización automática y navegación por teclado.

La configuración global conserva Corte, Disolución, Desplazamiento o Zoom y duraciones independientes para imagen y audio. Los valores iniciales de la aplicación son 500 ms visuales y 1.500 ms de salida/entrada de audio. Los cambios de video a texto, Biblia, imagen o fondo pasan por el mismo fundido del reproductor; F8 también lo utiliza. La Biblia sigue la transición global salvo una elección explícita propia.

Colecciones conserva grupos de videos y Cultos guardados conserva órdenes completos. Añadir y reemplazar crean identificadores independientes; reemplazar archiva el orden anterior y conserva la salida actual. Las pruebas usan carpetas temporales y verifican persistencia, cancelación y recuperación ante fallo de guardado.

PowerPoint usa Microsoft PowerPoint instalado y abre una copia sin título y de solo lectura. La prueba nativa crea un PPTX temporal con dos diapositivas, una animación por clic y un video MP4 incrustado. Además de comprobar los índices y tiempos internos de Office, compara píxeles reales del monitor local para detectar animaciones o videos congelados. También verifica la navegación sin cambiar el foco, la pausa del video y el negro, y conserva tanto el hash del PPTX original como la cantidad de presentaciones preexistentes. Las transiciones y medios incrustados siguen el motor de PowerPoint; no se convierten en imágenes. El fundido de audio de VLC no modifica el audio interno de PowerPoint.

El presentador registra Av Pág/Re Pág o flechas izquierda/derecha como teclas globales opcionales. Las pruebas comprueban despacho, conflictos, cambio de asignación y liberación al desactivar/cerrar. No identifican un dispositivo USB particular: esas teclas del teclado también quedan reservadas. El presentador físico y el HDMI real aún requieren ensayo en el equipo de la iglesia.

La revisión final compiló sin advertencias ni errores y aprobó **84 pruebas del núcleo, 60 del mando y 113 comprobaciones de escritorio** (`.tools/release016-final.log`). La suite adicional de PowerPoint aprobó **10 comprobaciones** (`.tools/powerpoint-final-chrome.log`): avance y retroceso visibles de la animación, video en movimiento, controles sin tomar el foco, negro, pausa y preservación de documentos. El panel oculta únicamente el dock de la presentación propia y ajusta el área de contenido usando sus medidas reales; la prueba verifica que no quede una barra de Office en la imagen.

El reproductor aprobó **127 comprobaciones** en `.tools/renderer-release-final-result.txt`. En la ejecución conjunta anterior hubo un timeout al reiniciar el video después de restaurar desde negro; no se reprodujo en el ensayo focalizado ni al repetir únicamente el reproductor completo. Se añadió diagnóstico del estado nativo de VLC sin cambiar el producto, los tiempos límite ni las aserciones. Las suites aprobadas suman **394 comprobaciones**, incluyendo las 10 de PowerPoint.

Se publicó la distribución autónoma **0.16.0** con `scripts/build.ps1 -Publish`, código 0 (`.tools/publish016-final.log`). Antes de que el usuario detuviera el control de la PC, la revisión manual de esa distribución, usando `.tools/qa-016`, mostró el rectángulo animado y el video incrustado dentro de En pantalla, sin barra inferior, y abrió el editor bíblico con su vista previa. La sesión de ensayo quedó abierta al detenerse la revisión; no se cerró ni se sustituyó por la sesión habitual después de la indicación del usuario. Al volver a abrir normalmente el iniciador se usan los datos habituales, no los del ensayo.

Durante esa revisión apareció una pequeña barra flotante de Office «Presentación / Reanudar presentación» después de pausar. Su pertenencia a la copia propia o a la presentación del usuario que también estaba abierta no se pudo confirmar; queda pendiente de identificar, sin modificar barras globales de PowerPoint. La revisión manual de los otros diálogos quedó incompleta. Los hashes de `workspace.json` y `biblia.json` habituales permanecieron iguales durante las pruebas.

### Arrastre al orden y carpetas de Windows — versión 0.15.0

La biblioteca y los resultados bíblicos permiten arrastrar contenido al orden. Las fuentes se copian con identificadores independientes; arrastrar dentro del orden mueve el elemento existente. La inserción usa la mitad superior/inferior de cada fila y admite orden vacío o espacio final, con línea violeta y desplazamiento en los bordes. El guardado conserva texto bíblico, referencia, versión, número de himno y ruta multimedia. Un clic prepara; doble clic proyecta el elemento añadido.

Videos e Imágenes leen las carpetas conocidas de Windows; Descargas resuelve su ubicación mediante `SHGetKnownFolderPath`. Incluyen subcarpetas y omiten enlaces de directorio que podrían formar ciclos. La lectura ocurre fuera del hilo de la interfaz, admite cancelación y conserva identificadores por ruta. Un observador actualiza los archivos; el botón circular permite actualizar manualmente. Cambiar de categoría cancela lecturas anteriores. La actualización conserva la preparación y la salida, incluso si se había preparado otro elemento del orden o de la Biblia.

Las pruebas de fuentes usan archivos temporales y ubicaciones de carpetas sustituidas. Cubren persistencia, copias independientes, actualizaciones por creación/borrado, cancelación, recuperación de una carpeta ausente y reproducción MKV/versículo desde el orden.

`scripts/build.ps1 -Smoke` terminó con código 0: **60 núcleo + 60 mando + 94 escritorio + 65 reproductor = 279 comprobaciones**, sin errores ni advertencias. Evidencia: `.tools/folders-full-test.log`.

La verificación con ratón real detectó que cargar la vista previa de un video durante mouse-down podía perder un arrastre rápido. La carga se difiere hasta soltar el clic; el arrastre captura el origen y transporta un identificador de texto mediante OLE, manteniendo los objetos dentro de la aplicación. Se verificó después el arrastre rápido de «002 Da gloria al Señor» al orden y su reproducción con doble clic en la salida local. También se arrastró Juan 3:16. Se retiraron el himno 002 y el versículo duplicado usados en esa comprobación, y se dejaron pausadas previa/salida. Descargas mostró sus cinco archivos multimedia reales. La biblioteca conservó 619 elementos y el orden quedó con cinco, incluido el himno 001 incorporado durante la revisión.

Tras esas correcciones se repitió toda la suite de escritorio: **94 comprobaciones correctas, código 0** (`.tools/folders-desktop-final.log`). Se publicó la distribución autónoma 0.15.0 y quedó abierta en Descargas.

### Fondos de salida y lista de imágenes — versión 0.14.0

Debajo del reproductor de salida aparecen Fondo iglesia y Fondo negro, fijos fuera del desplazamiento de controles. Fondo iglesia abre una lista con miniaturas; elegir una imagen la envía con disolución. La lista permite agregar varios archivos, quitar referencias sin borrar archivos, elegir fondo sencillo y volver al contenido anterior. La lista y la última imagen elegida se guardan con el espacio de trabajo. Fondo negro activa negro y, al pulsarlo otra vez, devuelve el contenido anterior pausado.

La salida baja el audio durante 180 ms y disuelve la imagen durante 400 ms. Conserva el mismo medio y su posición, sin reemplazar el elemento en vivo ni la vista previa. Restaurar durante una transición conserva la pausa; las órdenes explícitas de reproducir o reiniciar prevalecen sobre una restauración pendiente. F8 y los comandos inmediatos del mando conservan su corte instantáneo. Una imagen de fondo ausente o dañada deja el fondo sencillo con la reproducción pausada.

`scripts/build.ps1 -Smoke` terminó con código 0: **60 núcleo + 60 mando + 76 escritorio + 65 reproductor = 261 comprobaciones**, sin errores ni advertencias. Los ensayos de fondos comprueban píxeles reales de negro/imagen/fallback, audio nativo, posición pausada, cambios rápidos, restauración y órdenes de transporte durante una transición. Las pruebas de escritorio pulsan los botones reales, eligen una miniatura en el diálogo y verifican el guardado y la retirada de una imagen sin borrar su archivo. Continúan pasando las pruebas de paneles estrechos, HDMI, Biblia y mando. `mobile-smoke.cjs` también pasó. Evidencia: `.tools/backdrops-final.log`.

Se publicó y abrió 0.14.0 conservando los últimos datos del usuario: cuatro elementos en el orden y 619 contenidos. En la consola normal se proyectó Bienvenidos, se activó Fondo negro y se comprobó el monitor totalmente negro con su botón activo; una segunda pulsación restauró el contenido. Se abrió Fondo iglesia y se verificó la lista vacía con Agregar imágenes, Sin imagen y Cerrar. Quedó abierta para que el usuario agregue sus imágenes; no se incorporaron imágenes de prueba a sus datos.

### Reproductores y desvanecimiento de audio — versión 0.13.0

Se rediseñaron los transportes de previa y salida con controles circulares, barra de avance completa, tiempo transcurrido y duración separados, saltos de diez segundos y reinicio. La salida añade silencio con recuperación del último volumen y repetición. La barra conserva el punto elegido durante el arrastre y los clics prolongados, incluso cuando WPF marca el evento como atendido. La altura mínima de la previa pasó a 340 píxeles para conservar al menos 80 píxeles de imagen con el nuevo transporte.

El motor conserva un solo reproductor de salida: baja el volumen durante 180 ms antes de sustituir contenido y lo sube durante 350 ms cuando el reproductor nativo confirma el inicio. La disolución visual sigue durando 400 ms. Cambiar de volumen modifica el destino del desvanecimiento; los cambios rápidos conservan la última petición. Pausa, Negro, Fondo y cierre cancelan las acciones pendientes. La previa mantiene la pista de audio desactivada. El estado de pausa refleja la intención de reproducción durante la carga y permite volver a reproducir después del final.

La regresión inicial falló porque el contenido saliente se reemplazaba inmediatamente. Después del cambio, `scripts/build.ps1 -Smoke` terminó con código 0, sin errores ni advertencias: **60 núcleo + 60 mando + 73 escritorio + 46 reproductor = 239 comprobaciones**. Las pruebas muestrean el volumen del reproductor nativo durante la bajada y subida, comprueban cambios de volumen, sustituciones rápidas, interrupción y reanudación, previa silenciosa y cierre con transición pendiente. También verifican arrastre, clic atendido en la barra, pausa conservada, controles independientes, restauración de volumen y ajuste de paneles. `mobile-smoke.cjs` continúa pasando. Evidencia: `.tools/player-full-final.log`.

Se publicó y abrió la distribución autónoma 0.13.0. Se conservó el orden actualizado del usuario (cinco elementos y 617 contenidos en biblioteca). En la consola abierta se observaron los controles nuevos con un video real del orden: previa reproduciéndose, salida pausada y tiempos independientes, sin recortes ni superposición de controles. No se realizó una evaluación auditiva con las pantallas de la iglesia; las comprobaciones del desvanecimiento se hicieron sobre el volumen real del reproductor nativo.

### Orden del culto desde el teléfono — versión 0.12.0

El mando incorpora el orden real, números, títulos y tipos, con marcas de preparado/en vivo y estado Local/HDMI/Negro/Fondo. Un toque envía el identificador del elemento; la PC lo busca nuevamente en el orden antes de proyectar. El servidor rechaza identificadores eliminados o ajenos al orden y conserva autenticación, control de origen y deduplicación. El mando espera la confirmación de la consola y no reenvía automáticamente una acción perdida. Se mantienen los accesos personalizados a la biblioteca.

Las pruebas verifican HTTP autenticado → dispatcher de WPF → reproducción MKV local, estado, pausa y siguiente. La conexión prioriza direcciones con puerta de enlace y permite actualizar la red; no ofrece QR de bucle local ni dirección automática sin red. Una regresión adicional comprueba que actualizar la red durante activación/desactivación no duplica operaciones y que el QR se genera y se elimina correctamente. Las comprobaciones de un teléfono físico por Wi-Fi y de HDMI físico siguen pendientes; no se modificó el firewall ni el router.

### Panel de salida compacto — versión 0.11.1

Se reprodujo el recorte de la captura: una distribución guardada con cuatro columnas heredaba mínimos de 390/390/360/360 y situaba el video fuera del área visible de la consola. La regresión falló con límites 1186,135,337,190 en una ventana de 1280×900. Ahora se calculan los mínimos de cada contenedor según sus paneles actuales, también al restaurar o reagrupar. En pantalla admite 220 píxeles de ancho; por debajo de 320 reorganiza título, selección de HDMI, navegación y reproducción. El video conserva 16:9 y los controles secundarios tienen desplazamiento vertical si falta altura. Negro, Fondo y el video quedan fuera del área desplazable.

La prueba dirigida pasó después del cambio. La suite de escritorio completa terminó con código 0 y **61 comprobaciones correctas**, incluyendo restauración de cuatro columnas a 1280 píxeles, salida a 240/280/420/240 píxeles, acceso a todos sus botones/barras, proporción del video y conservación de la sesión. También se comprobó la consola a 1280×680 y 1280×720. No se modificó el motor de video ni se repitieron las suites de núcleo/mando/reproductor en este ajuste visual.

Se publicó y abrió 0.11.1 en una ventana normal, conservando las cuatro columnas del usuario. Se envió el himno 014, se pausaron salida y previa y se estrechó En pantalla mediante su separador hasta unos 240 píxeles. La captura mostró todo el video, título, navegación en dos filas, botones HDMI, reproducción, tiempo, posición, volumen y Repetir dentro del panel. La salida siguió pausada a 00:04 y la previa a 00:09. Quedó abierta en esa distribución compacta.

### Reproducción local sin HDMI — versión 0.11.0

El envío al panel En pantalla ya no depende de que Windows detecte un monitor externo. Doble clic o Proyectar crean un único reproductor de salida dentro del programa, con disolución, pausa, posición, negro, fondo y control remoto. El indicador LOCAL distingue esta reproducción de HDMI. La vista previa permanece independiente y silenciosa.

Activar HDMI coloca ese mismo reproductor en la ventana externa; no lo reconstruye ni reinicia el video. Desactivar HDMI, cerrar esa ventana o desconectar el cable lo devuelve al monitor local, conservando la reproducción. Sin ventana externa, el monitor dibuja el visual WPF vivo; con ventana externa mantiene su reflejo DWM. No se abre ninguna ventana de ensayo automáticamente. La conexión física debe activarse expresamente desde el panel o el selector de pantalla.

La prueba dirigida falló antes del cambio con «Missing HDMI blocked sending the hymn to En pantalla» y pasó después. `scripts/build.ps1 -Smoke` terminó con código 0, sin advertencias ni errores: **60 núcleo + 39 mando + 60 escritorio + 32 reproductor = 191 comprobaciones**. Las nuevas pruebas comprueban reproducción sin HWND de salida, píxeles del video dentro del monitor local, fin de disolución, independencia de la previa, pausa/restauración, y la misma instancia/posición/reproducción al abrir y cerrar una ventana externa explícita de ensayo. Esa última prueba verifica la transferencia entre ventanas, no un HDMI físico.

Se publicó y abrió 0.11.0. En la aplicación normal se buscó 12 y se hizo doble clic sobre «012 Todos juntos reunidos». Se observó el video reproduciéndose dentro de En pantalla, con estado LOCAL y duración 01:21, al mismo tiempo que la previa silenciosa. La salida se pausó a 00:09 y la previa siguió hasta pausarla a 00:13; ambos quedaron visibles, con sus botones Continuar independientes. No apareció una ventana de proyección aparte. Se conservaron los cuatro elementos del orden, 616 contenidos y distribución de paneles.

### Disolución de himnos y videos — versión 0.10.0

Al proyectar un video desde el orden, la secuencia o la biblioteca se conserva una imagen del contenido saliente hasta recibir el primer fotograma nuevo; después esa imagen se desvanece durante 400 ms. El video entrante se reproduce durante la mezcla. El botón Proyectar usa la misma transición. Se añadió doble clic a las filas de biblioteca, ignorando su espacio vacío. Negro, Fondo, un nuevo contenido y el cierre cancelan la transición; una animación anterior no puede borrar la nueva. La transición es visual: no mantiene dos videos ni mezcla dos pistas de audio.

La salida entrega los fotogramas decodificados por VLC a un WriteableBitmap de WPF; la previa conserva su VideoView nativo. Esto permite mezclar los píxeles dentro de la misma ventana que refleja DWM. El puente sincroniza el búfer nativo y su liberación, alinea planos/paso de fila a 32 bytes y limita a una entrega pendiente al hilo gráfico, descartando fotogramas atrasados. Conserva la relación de aspecto y limita la composición de salida a 1920×1080. Referencia técnica: [callbacks de video de LibVLCSharp](https://docs.videolan.me/libvlcsharp/api/LibVLCSharp.Shared.MediaPlayer.html).

`scripts/build.ps1 -Smoke` terminó con código 0, sin advertencias ni errores: **60 núcleo + 39 mando + 58 escritorio + 32 reproductor = 189 comprobaciones**. La regresión del doble clic falló antes de implementar la disolución. Las nuevas pruebas verifican mezcla de píxeles rojo/azul en la salida, finalización, espera de imagen del MKV, cambios consecutivos y negro durante animación/carga. Las pruebas previas cubren reproducción, pausa, repetición, posición, DWM, paneles y salida sin HDMI. La nueva prueba de biblioteca necesitó activar su pestaña para generar la fila visible antes de simular el doble clic.

En un arnés aislado, sin tocar el catálogo del usuario y con volumen cero, el archivo local «012 Todos juntos reunidos.mkv» completó la transición y presentó **450 fotogramas en 15 segundos**, imagen 1902×1080, reproducción activa y monitor DWM conectado, sin errores. El proceso consumió 5,05 segundos de CPU en ese intervalo en esta PC de 20 procesadores lógicos. No es una garantía de rendimiento para todos los videos o equipos. La prueba física HDMI sigue pendiente.

Se publicó la distribución autónoma 0.10.0 y se abrió maximizada con la distribución personalizada conservada, cuatro elementos del orden, 616 contenidos y Bienvenidos en previa. La salida quedó cerrada. La comprobación visual de la consola confirma la apertura y conservación del espacio de trabajo; la mezcla se verificó mediante los píxeles del reproductor y las pruebas nativas descritas arriba.

### Control de salida externa — versión 0.9.0

Se eliminó la apertura automática de ensayo en el monitor de la consola. La selección excluye el monitor donde está la ventana de control, respeta una salida externa guardada y admite que la pantalla principal de Windows sea la de la iglesia. En pantalla incorpora Pantalla… y Activar/Desactivar salida. La salida externa es completa, sin bordes ni entrada propia en la barra de tareas, y no solicita el foco al abrirse. Al cerrar, el monitor DWM se desconecta y desaparece el título en vivo. Al activar se recupera el último contenido enviado, sin proyectar el preparado; los videos quedan pausados.

La prueba de regresión falló antes del cambio porque Proyectar abría una ventana en el único monitor. Después, `scripts/build.ps1 -Smoke` terminó con código 0, sin errores ni advertencias: **60 núcleo + 39 mando + 57 escritorio + 24 reproductor = 180 comprobaciones**. Se verificaron selección con monitores sintéticos (incluida consola secundaria), salida guardada desconectada, activación sin HDMI, cierre desde el botón, conservación de la sesión y desconexión del monitor real. Los ensayos nativos que necesitan una salida en esta PC la abren explícitamente mediante el arnés de pruebas; esa opción ya no está en el diálogo del usuario.

Windows detecta solamente DISPLAY1, principal, 1920×1080. La proyección física HDMI en modo extendido sigue pendiente; los escenarios de selección con varias pantallas son pruebas sintéticas.

Se publicó y abrió 0.9.0 conservando la distribución personalizada de paneles. En la interfaz se pulsaron Proyectar y Activar salida: permaneció una sola consola, con «Sin salida» y el aviso de pantalla externa ausente. El selector mostró «Sin pantalla externa disponible» y Abrir salida deshabilitado, sin opción de ensayo. Se cerró el diálogo y quedó la consola maximizada con Bienvenidos preparado, los cuatro elementos del orden y los 616 contenidos.

### Ajuste continuo de paneles — versión 0.8.1

Los separadores actualizan las dimensiones de los paneles durante cada movimiento del ratón, en horizontal y vertical. El contenido WPF y los monitores nativos reciben sus dimensiones reales en el mismo ajuste. Se conservan los tamaños mínimos, el guardado de proporciones y la cancelación del arrastre. La extensión gestiona los eventos del separador antes de los manejadores de AvalonDock que normalmente muestran una guía hasta soltar el ratón; no modifica la dependencia.

Se añadieron dos pruebas: cambio visible antes de soltar, conservación de la sesión y cancelación; y movimientos verticales sucesivos, límite mínimo y persistencia del tamaño confirmado. Pasaron **60 pruebas del núcleo, 39 del mando y 54 de escritorio**, con compilación sin advertencias ni errores. El ensayo del reproductor falló dos veces en la comprobación de bucle basada en una espera fija de 3,4 segundos. Se sustituyó esa comprobación por la observación del fin real del video y un segundo evento Playing, con límite de 10 segundos. Sin cambios en el reproductor, pasó sus **24 comprobaciones**, observando un final y dos inicios: **177 verificaciones correctas entre las ejecuciones finales**.

Se publicó 0.8.1 y se recuperó la distribución personalizada del usuario (biblioteca, orden, previa y salida en columnas, secuencia debajo). Se comprobó con el ratón el aumento de ancho de la previa mediante su separador. La aplicación quedó abierta y maximizada, con los cuatro elementos del orden y 616 contenidos, sin salida activa.

### Dos monitores y paneles acoplables — versión 0.8

Vista previa y En pantalla son monitores independientes, a izquierda y derecha. En pantalla muestra una miniatura DWM de la ventana real de proyección, incluidos sus hijos nativos de video; no crea otro reproductor ni otra pista de audio. Al separar el monitor en una ventana, se vuelve a enlazar al HWND superior correcto, incluso cuando AvalonDock aloja el contenido en un HWND intermedio. Se libera la miniatura al cerrar la salida, ocultar su pestaña o descargar el control.

AvalonDock 5.0.1 permite mover, redimensionar, agrupar y separar los cinco paneles. El menú Espacio de trabajo ofrece activación, guardado y restablecimiento. `paneles.xml` se guarda aparte de `workspace.json`, con sustitución atómica; la restauración conserva las mismas instancias de controles. Se validan los cinco identificadores y se recuperan posiciones flotantes fuera del escritorio virtual. Se incluyen las licencias de las cuatro dependencias nuevas.

`scripts/build.ps1 -Smoke` terminó con código 0, sin advertencias ni errores: **60 núcleo + 39 mando + 52 escritorio + 24 reproductor = 175 comprobaciones**. Las cinco pruebas añadidas verifican ambos monitores, el enlace y cierre de la salida, flotación/acoplamiento de todos los paneles, agrupación y persistencia sin modificar la sesión, rechazo de distribuciones incompletas y recuperación de ventanas flotantes fuera de pantalla. Las pruebas de geometría siguen cubriendo 1280×680 y 1280×720, las tres fuentes y sus controles.

En una instancia aislada con copia de los datos se reprodujo el himno 012 en previa y salida de ensayo. Se observó el video real dentro del monitor En pantalla; al pausarlo a 00:26, la previa siguió avanzando. Se arrastró la cabecera En pantalla hasta separarla en una ventana flotante: siguió mostrando la misma imagen pausada. La copia de prueba se cerró sin sustituir los datos del usuario. La Biblia local mantiene su SHA-256 y sus 31.102 versículos. La prueba de HDMI y teléfono físico continúa pendiente.

Se publicó y abrió 0.8.0 con los datos normales. Se comprobaron visualmente ambos marcos alineados, las fuentes a la derecha y el orden y secuencia debajo. Quedó abierta con Bienvenidos en previa, sin salida ni reproducción activas. Se conservan los cuatro elementos del orden, 616 contenidos y seis botones del mando.

### Composición de la consola — versión 0.7

Se rehízo la estructura con una barra superior integrada, filas compactas en el orden, una vista previa central 16:9 y controles de reproducción y salida agrupados. El marco ajusta las dimensiones reales del reproductor nativo, sin transformaciones visuales sobre su ventana. El botón de proyectar queda junto al título del contenido.

La tira inferior muestra el orden real y sincroniza su selección: un clic prepara y doble clic proyecta. Texto y Biblia muestran el contenido reducido; las imágenes se decodifican de forma asíncrona, liberando el archivo y descartando cargas de elementos reciclados. Video y audio usan portadas con título, sin reproductores adicionales.

`scripts/build.ps1 -Smoke` terminó con código 0: 60 núcleo, 39 mando, 46 escritorio y 24 reproductor. Después de añadir la comprobación de imágenes, el ensayo final de escritorio pasó sus 47 pruebas: **170 comprobaciones en total**. Una ejecución intermedia falló en el foco de Ctrl+1/2/3; la repetición sin cambios pasó. No se determinó la causa de ese fallo transitorio. Las pruebas nuevas cubren formato 16:9 a 1480×900 y 1280×680, clics y reordenación en la tira, carga de imágenes y archivos inválidos. Compilación y publicación Windows x64 sin errores ni advertencias.

En el ejecutable publicado se comprobó la nueva composición, la selección de Juan 3:16 desde su miniatura y la reproducción del MKV 012 dentro del marco. Se dejó pausado a 00:23, con la búsqueda vacía, los 616 contenidos visibles y la salida cerrada. Permanecen los cuatro elementos del orden y el SHA-256 de la Biblia local. Esta revisión no resuelve la demora intermitente observada al iniciar algunos MKV ni sustituye la prueba pendiente con las pantallas físicas.

### Consola audiovisual — versión 0.6

La segunda referencia visual del usuario reemplaza la dirección carbón/lima: gris oscuro con matiz violeta, acciones moradas y estados en verde menta. El orden queda a la izquierda, la vista previa y los controles de salida en el centro y las fuentes a la derecha. Se conservan las cápsulas redondeadas, la tipografía incluida, las animaciones breves y los menús superiores. El indicador superior se enlaza al estado real de la salida y distingue SIN SALIDA, ENSAYO, ACTIVA, NEGRO y FONDO.

Se adaptó el ensayo de distribución para comprobar la nueva posición central de la vista previa y el ajuste de los paneles laterales. Antes del cambio falló; después pasó, incluidos el encogimiento a 1280×680/720, los controles multimedia, los clics y la accesibilidad de los contenidos de las tres pestañas. `scripts/build.ps1 -Smoke` terminó con código 0: **60 núcleo + 39 mando + 44 escritorio + 24 reproductor = 167 comprobaciones**. Compilación sin errores ni advertencias y publicación Windows x64 completada.

Se observó el ejecutable publicado con la nueva distribución, los botones y las selecciones en violeta y el estado SIN SALIDA. La búsqueda de Juan 3:16 funciona en el panel derecho con el título y el texto legibles. El programa queda abierto, con salida cerrada. No se añadieron elementos al orden ni a la biblioteca durante esta revisión.

### Diseño de referencia — versión 0.5

Se estudió el video local facilitado por el usuario, correspondiente al pin `141300507053011816`, a lo largo de sus 46 segundos. La consola adopta superficies carbón con matiz verdoso, acento lima, pequeños iconos pastel y radios de 10–18 px. La navegación permanece arriba, sin barra lateral de navegación. El orden conserva su panel de trabajo y el botón Agregar al pie.

Poppins se incluye en cuatro pesos con licencia OFL para funcionar sin instalación ni internet. Es una aproximación visual: la fuente exacta del video no está identificada. El cambio de sección usa entrada de 180 ms y el hover de los botones 130 ms. Se respeta `SystemParameters.ClientAreaAnimation`; los valores finales no dependen de terminar una animación. El reproductor nativo y la salida no reciben transformaciones ni animaciones.

Se ejecutó `scripts/build.ps1 -Smoke` y, tras corregir la herencia del color de las pestañas, se repitió el ensayo de escritorio. Resultado: **60 núcleo + 39 mando + 44 escritorio + 24 reproductor = 167 comprobaciones correctas**. Las cuatro nuevas verificaciones cubren resolución de los pesos de fuente desde el paquete, cambios rápidos de sección sin alterar la salida, contraste del contenido de Biblia y comandos/enlace del deslizador. Continúan pasando las comprobaciones de tamaño a 1280×680 y 1280×720.

Se publicó Windows x64 y se verificó visualmente el ejecutable: consulta de Juan 3:16 con contraste legible, pestañas redondeadas, biblioteca completa y reproducción del MKV 012 en el minirreproductor. Se dejó abierto con el video pausado a 00:06, búsqueda vacía y salida cerrada. Se conservan los 4 elementos del orden y los 616 contenidos; el SHA-256 de la Biblia local permanece igual.

### Orden del culto y minirreproductor — versión 0.4

El orden incluye **+ Agregar** al pie: biblioteca/himnario, Biblia y texto abren la sección correspondiente; archivos permite seleccionar multimedia y añadirla directamente. Un clic prepara, incluido volver a una fila ya seleccionada después de consultar otra fuente; doble clic proyecta la fila pulsada e inicia video/audio. El espacio vacío no proyecta. El arrastre comienza sólo desde una fila y se distingue del clic.

La vista previa conserva reproducción silenciosa con pausa, reinicio, tiempo/duración y barra de avance independiente de la salida. Los controles aparecen sólo para video/audio. Se mantuvo la selección de salida configurada y la ventana de ensayo cuando sólo está disponible el monitor principal.

Se ejecutó `scripts/build.ps1 -Smoke` con código 0: **60 núcleo + 39 mando + 40 escritorio + 24 reproductor = 163 comprobaciones**. Cuatro pruebas nuevas cubren los clics, el menú y la independencia de reproducción. Las comprobaciones de tamaño incluyen Agregar y los controles del minirreproductor a 1280×680 y 1280×720. Compilación sin advertencias ni errores.

La prueba manual usó una copia de datos en `.tools/order-qa-v04`: abrir +, buscar el himno 12, reproducir su MKV en la vista previa, añadirlo al orden y proyectarlo con doble clic. Un clic en «Momento de oración» cambió la vista previa mientras el himno siguió en salida. En esta prueba el MKV inició sin el bloqueo prolongado observado anteriormente; no se modificó el motor de reproducción ni se considera resuelto aquel problema intermitente. La copia de prueba se cerró sin sustituir los datos del usuario.

### Comprobaciones anteriores

Se ejecutó `scripts/build.ps1 -Smoke -Publish`: salida 0, compilación Release sin errores ni advertencias, publicación con runtime .NET y LibVLC incluidos.

| Componente | Resultado |
|---|---|
| Núcleo: sesión, Biblia, multimedia y recuperación de archivos | 60 pruebas correctas |
| Servidor del mando: HTTP, autenticación, validación y deduplicación | 39 pruebas correctas |
| Integración nativa WPF: orden, Biblia, salida, mando, recuperación y diseño | 36 comprobaciones correctas |
| Reproductor nativo: texto, imagen, MKV, audio, negro y pausa | 24 comprobaciones correctas |
| Cliente móvil: `node tests/Culto.Remote.Tests/mobile-smoke.cjs` | PASS |

Los dos procesos de ensayo nativo terminaron con código 0. Total en la versión 0.3: 159 pruebas/comprobaciones, más el ensayo JavaScript de la primera entrega (su código no cambió). El ensayo JavaScript usa un DOM mínimo: no sustituye una comprobación visual en navegador o teléfono.

En la versión anterior se ajustó el desplazamiento vertical del panel de conexión del mando y se comprobó visualmente que las instrucciones completas son accesibles.

## Comodidad de uso — versión 0.3

### Apariencia de escritorio — versión 0.3.1

La consola utiliza negro y gris neutro, bordes discretos y controles menos redondeados. Archivo, Biblioteca y Configuración reúnen las acciones en una barra superior; Configuración abre pantallas y mando móvil, mientras Biblioteca contiene las importaciones, captura y edición. La barra de título principal adopta el fondo oscuro en Windows 11 mediante los atributos nativos de DWM. La vista previa conserva el fondo propio del contenido.

Se repitió `scripts/build.ps1 -Smoke -Publish` con salida 0, sin errores ni advertencias: 60 pruebas del núcleo, 39 del mando, 36 de escritorio y 24 del reproductor. La comprobación de geometría incluye los tres botones superiores a 1280×720 y 1280×680. Se corrigió el ensayo para reconocer los fragmentos de texto de las etiquetas con teclas de acceso al comprobar los clics.

En el ejecutable publicado se verificaron visualmente los tres menús, sus accesos Alt+A/B/C, la apertura y cancelación del diálogo de pantallas y la sección Biblia. Se conservaron los 4 elementos del orden, 616 contenidos de biblioteca y 6 botones del mando. El SHA-256 de la Biblia local no cambió. La aplicación quedó abierta; el reinicio cerró la ventana de ensayo y desactivó el mando de la sesión anterior.

### Organización y navegación incorporadas en 0.3

Refinamiento visual posterior: se retiraron el icono y nombre de la cabecera por petición del usuario. Después se suavizaron los paneles (radio 14), controles y campos (radio 8), con bordes de panel menos marcados y categorías sin recuadro salvo selección o foco. Se ejecutaron nuevamente las 159 comprobaciones con salida 0 y, tras ajustar el espacio interior del campo de texto, se repitieron las 36 de escritorio con salida 0. La publicación final se comprobó visualmente buscando Juan 3:16; conservó sus selectores, resultado y vista previa, sin abrir salida.

La cabecera reúne las acciones del culto en un menú. La importación y edición se agrupan en «Administrar»; las notas se abren bajo demanda. Los controles de salida se sitúan junto a la vista previa y muestran reproducción sólo para video/audio. El orden marca el elemento «EN VIVO», mientras que el panel de salida indica qué sigue y mantiene visibles los estados Negro/Fondo.

La búsqueda numérica de himnos es exacta en Todo/Himnos. Enter prepara el resultado visible, Ctrl+F enfoca la búsqueda y Ctrl+1/2/3 cambia de sección. Las otras categorías conservan búsqueda por título y reconocimiento de números de himno.

La Biblia permite elegir libro, capítulo y versículo o escribir un capítulo completo, incluido un alias con ordinal como «2. Tes 2». Las acciones «Versículo» y «Pasaje (N)» distinguen añadir sólo el seleccionado de añadir todos los versos del rango/capítulo. Buscar y navegar conserva la salida que ya está en vivo.

Se añadieron 16 pruebas del núcleo y 13 comprobaciones de escritorio: coincidencia exacta sin perder las búsquedas por categoría; Enter y foco; atajos e inicialización; controles contextuales y Negro/Fondo; marca y siguiente después de preparar/reordenar; selectores sobre capítulos dispersos y largos; conservación de consultas; separación de un verso y 176 versos; y consultas inválidas. La geometría existente ahora comprueba también los selectores y los controles multimedia a 1280×720 y 1280×680.

La revisión detectó y permitió corregir un indicador Fondo que quedaba oculto con video, la clasificación de referencias con ordinal y una regresión de búsqueda numérica en la categoría Videos. El último ensayo completo pasó con código 0, sin advertencias ni errores.

En una instancia aislada con copia de los datos reales se comprobaron visualmente «Juan 3» (36 versículos), el selector hasta Juan 3:36 y la búsqueda `12` con un único himno preparado. Se revisó el menú Administrar y se corrigió el fondo claro heredado del menú nativo.

La primera preparación del MKV 012 en ese ensayo mostró temporalmente «No responde» y luego se recuperó y reprodujo el video en la vista previa silenciosa. No se estableció la causa de esa demora inicial; las comprobaciones automatizadas con MKV local corto sí terminaron correctamente. La mejora de organización no debe interpretarse como solución verificada de esa demora.

Se ejecutó después `scripts/build.ps1 -Smoke -Publish` con las 159 comprobaciones correctas y código 0. Se abrió el ejecutable publicado 0.3, se comprobó el menú oscuro con foco de teclado y se dejó Juan 3:16 preparado en Biblia, sin salida ni mando activos. Se conservaron los cuatro elementos que el usuario tenía en el orden, 616 contenidos de biblioteca y seis botones del mando. Los 31.102 versículos permanecen sin cambios (SHA-256 idéntico). Antes de actualizar se guardó un respaldo de los dos archivos de datos en `%LOCALAPPDATA%\CultoStudio\Respaldos\antes-v03-20261111-124832`.

## Mejora de diseño y organización — versión 0.2

Se reorganizaron la biblioteca por categorías, el orden numerado, la vista previa y la barra de reproducción. Los paneles se pueden ajustar; los menús y diálogos comparten colores, foco y tipografía. El editor del mando separa la lista de botones de sus propiedades y permite buscar un contenido por el comienzo de su título.

Se añadieron seis comprobaciones de escritorio: búsqueda/categorías/contador y limpieza con foco; numeración después de reordenar; orientación con lista vacía; desplazamiento profundo en un orden de 80 elementos y posterior reordenación/eliminación; restablecimiento del botón de pausa previa; y geometría de la consola a 1280×720 y 1280×680 después de ampliar los paneles laterales, incluyendo una lectura de varias líneas y todas las pestañas.

Las pruebas detectaron dos fallos del diseño inicial: numeración incorrecta al reciclar filas lejanas y una referencia bíblica demasiado ancha. Se corrigieron manteniendo índices absolutos en las filas realizadas y limitando/ajustando el texto al panel. La misma prueba pasó tras los cambios. La vista previa también se hizo flexible para conservar espacio para el resumen en vivo.

Se ejecutó de nuevo `scripts/build.ps1 -Smoke -Publish` completo: código 0, sin errores ni advertencias. Se abrió el ejecutable publicado 0.2 y se verificaron visualmente la categoría Himnos (613 de 616 contenidos) y el arrastre de la barra de desplazamiento hasta los himnos 538–543 y de vuelta al principio. En el ensayo aislado se comprobaron los menús oscuros con teclado y la búsqueda `613` en el selector de contenido del mando.

La actualización conserva 613 videos del himnario, tres textos de biblioteca, tres elementos del orden y seis botones del mando. Se cerró la versión anterior para actualizarla y se dejó abierta la nueva, sin proyección ni mando activos. Una conexión anterior del teléfono necesita volver a vincularse al nuevo QR.

## Aplicación publicada

Se abrió el ejecutable publicado y se comprobaron la consola, la preparación independiente y la proyección de texto en ventana de ensayo. Mediante HTTP autenticado se enviaron comandos al servidor real de esa instancia: Proyectar mostró «Bienvenidos» y Siguiente cambió a «Momento de oración». Se cerró la instancia de ensayo y se dejó abierta la aplicación con los datos normales del usuario, sin proyección ni mando activados.

La revisión independiente detectó y permitió corregir la restauración de video después de recrear una salida en negro, el despacho de botones personalizados de contenido y la salida explícita ante fallo de guardado local. Se añadieron las comprobaciones correspondientes y la revisión posterior no encontró nuevos problemas en ese alcance.

## Himnario de esta PC

Se encontró la instalación portable `Documentos\Himnario Adventista 2022 VIDEOS`, cuyos ejecutables declaran Himnario Adventista 2023, versión 23.01.01.0.

Se importó mediante la interfaz la carpeta `Data\Himnario Videos`: **613 MKV**, 6.490.180.227 bytes. El archivo local `workspace.json` conserva 613 rutas únicas, todas existentes y con número de himno reconocido, además de los tres textos iniciales. No se movieron ni duplicaron los videos originales.

Se comprobó la búsqueda por número (`613`) y por título (`Cantad`), la vista previa de los himnos 001 y 613 y el avance visible del video 613. La vista previa se dejó pausada. No se reprodujeron de principio a fin los 613 archivos.

Durante la primera importación la ventana mostró temporalmente «No responde» y se recuperó sin intervención; los datos quedaron guardados. La enumeración posterior de la carpeta fue rápida. No se determinó la causa exacta de esa demora inicial.

## Biblia local de esta PC

Se importó mediante la interfaz una **Reina-Valera 1960 completa**, convertida desde el archivo local `Documentos\Easy Worshipp\Programa EasyWorship\Biblias\RV1960.ewb`. El original se conserva. El catálogo guardado contiene **66 libros, 1.189 capítulos y 31.102 versículos** y está disponible sin internet.

Se verificaron los conteos y la fidelidad de la conversión respecto a la fuente local, junto con un muestreo de lecturas para identificar la versión. Estas comprobaciones no equivalen a un cotejo editorial completo de la edición. El [informe de importación y procedencia](importacion-biblia-local.md) detalla la fuente, las correcciones y la persistencia.

Con el catálogo real, `BibleCatalog` validó nueve citas, incluidas lecturas del principio y final de la Biblia y de los tres capítulos de 2 Tesalonicenses. También se comprobaron búsqueda sin tildes y creación de elementos preparados con texto, referencia y versión.

Se volvió a cargar `%LOCALAPPDATA%\CultoStudio\biblia.json` y se repitieron las nueve consultas y la búsqueda por frase. Los 31.102 textos y sus coordenadas coinciden con el archivo convertido; los alias de libros quedaron normalizados a nombres canónicos.

En la aplicación se observaron los 31.102 versículos disponibles, los 17 resultados de `2 Tesalonicenses 2:1-17` y la consulta `Apocalipsis 22:21`. Se preparó `Juan 3:16` con el texto y la versión correctos. La salida quedó cerrada y se conservaron los tres elementos del orden, los 616 contenidos de biblioteca y los seis botones del mando.

El corpus permanece en los datos locales, fuera de Git y de la distribución ejecutable. Esta carga modificó datos del usuario y no requirió reconstruir el código del programa.

## Verificaciones pendientes y alcance

- Probar las tres pantallas conectadas al HDMI y el dispositivo de audio real de la iglesia.
- Probar el mando en el teléfono físico y su red Wi-Fi. No se cambiaron firewall ni router.
- Ensayar el orden y cada archivo que se utilizará antes del culto.

Esta versión ofrece capturas estáticas; no incorpora captura continua de otras aplicaciones, transmisión, grabación ni mezcla de cámaras. PowerPoint nativo está disponible desde 0.16 y requiere Microsoft PowerPoint instalado. Desde 0.11, En pantalla reproduce también sin ventana externa. Cuando se activa HDMI, el reflejo DWM requiere que esa ventana permanezca sin minimizar.
