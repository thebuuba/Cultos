# Validación de Culto Studio

Comprobación realizada el 11 de noviembre de 2026 en esta PC Windows. Entrega autónoma Windows x64 en `artifacts/CultoStudio`; iniciador `Abrir Culto Studio.cmd`.

## Compilación y pruebas

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

Esta versión ofrece capturas estáticas; no incorpora captura continua, transmisión, grabación ni PowerPoint. El panel «En pantalla» resume el contenido y estado; no duplica el video en vivo. Estos límites también se explican en la guía.
