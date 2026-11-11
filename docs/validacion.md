# Validación de Culto Studio

Comprobación realizada el 11 de noviembre de 2026 en esta PC Windows. Entrega autónoma Windows x64 en `artifacts/CultoStudio`; iniciador `Abrir Culto Studio.cmd`.

## Compilación y pruebas

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

Esta versión ofrece capturas estáticas; no incorpora captura continua de otras aplicaciones, transmisión, grabación ni PowerPoint. Desde 0.11, En pantalla reproduce también sin ventana externa. Cuando se activa HDMI, el reflejo DWM requiere que esa ventana permanezca sin minimizar.
