# Culto Studio

Consola nativa para Windows destinada a preparar y proyectar cultos. Reúne el orden del culto, colecciones de videos, imágenes, textos, Biblia importable, presentaciones de PowerPoint y controles desde el teléfono o un presentador USB. No depende de OBS.

En **Medios**, clic derecho en un video → **Añadir a lista de reproducción** permite elegir una lista o crear una nueva.

La pestaña **Lista de videos** permite crear listas, agregar videos, eliminar y cambiar el orden con las flechas. Doble clic o Reproducir inicia la continuación automática hasta el final. Proyectar otro contenido cancela la lista; para continuar tras un video externo, selecciona el video de entrada en la lista y marca **Después de este video, iniciar la lista**.

## Abrir el programa

Ejecuta **Abrir Culto Studio.cmd** en esta carpeta, o abre `artifacts/CultoStudio-0.35.1/CultoStudio.exe`. Cierra la versión anterior primero. La distribución publicada incluye .NET y el reproductor nativo; conserva todos los archivos de esa carpeta junto al ejecutable. Windows x64.

En **0.34.1**, Vista previa y En pantalla forman un solo panel fijo, sin cierres ni arrastre independientes, con Proyectar y la transici?n en el centro. Escenas, Vista previa y En pantalla comparten una tarjeta. Las escenas incluyen Logo, Fondo negro, Imagen, Video y YouTube; imagen y video recuerdan la elección y el menú derecho permite cambiarla. La transición se elige en la misma tarjeta. Los controles semitransparentes aparecen sobre el monitor local al pasar el ratón y también en Vista previa cuando sigue la salida. Usan el mismo reproductor y no aparecen en HDMI. Se conservan las distribuciones acoplables, el orden del culto y el arranque anticipado del motor.

En **0.33.0**, marca o desmarca cada panel en **Espacio de trabajo** para mostrarlo u ocultarlo. El clic derecho en la cabecera ofrece **Ocultar panel**; **Mostrar todos los paneles** recupera el conjunto. La visibilidad se guarda con la distribución. Ocultar En pantalla conserva la reproducción y la salida HDMI.

En **0.32.0** el principal agrupa Himnario, Biblia, YouTube, Medios y Texto en cinco pestañas del mismo panel. Orden del culto y ambos monitores permanecen visibles. Cada fuente conserva sus controles y búsquedas; puedes separar una pestaña con su cabecera o el menú contextual. El nuevo espacio se guarda en `paneles-v3.xml`; la distribución anterior se conserva en `paneles-v2.xml` y puede recuperarse desde Espacio de trabajo.

En **0.31.1** arrastra el titulo de un panel sobre otro: la zona verde indica donde quedara. Los bordes superior e inferior apilan; los laterales separan en columnas; el centro agrupa en pestanas. Esc cancela. Ajusta ancho y alto con los separadores entre paneles.

En **0.31.0**, el mando permite buscar **Biblia, Himnario y Videos** del equipo en **Contenido de la PC** y proyectar cada resultado. Los videos incluyen la biblioteca y las carpetas Videos, Descargas y la carpeta personalizada. Logo puede iniciar la salida desde el teléfono. Cierra la versión anterior, abre esta y recarga la página del mando.

Los paneles continúan unidos: arrastra los separadores para ajustar ancho y alto mientras se mueve el contenido; el clic derecho sobre el título permite colocarlos encima, debajo, a los lados o agruparlos en pestañas. Las fuentes pueden apilarse en paneles más bajos; sus controles se alcanzan desplazando el contenido. Guarda tu distribución en Espacio de trabajo; las tres distribuciones y el reset siguen disponibles.

En **0.30.0** el cambio incluye la estructura: tarjetas con un solo contorno, sombras discretas, controles cápsula y cabecera con el culto, su posición en el orden y **Control remoto**. Himnario, Biblia, Texto y YouTube son paneles independientes. El programa abre maximizado; puedes restaurar el tamaño de la ventana.

**Espacio de trabajo** ofrece tres distribuciones: **Principal**, con Medios, Himnario, Biblia y YouTube juntos y los monitores a la derecha; **Compacta**, con las fuentes en pestañas; y **Proyección**, con monitores grandes y las fuentes agrupadas a la izquierda. Arrastra y acopla sus cabeceras como prefieras, guarda la distribución y usa **Restablecer diseño principal** para volver. Principal se adapta a una ventana pequeña mientras no hayas personalizado sus posiciones. Las nuevas posiciones se guardan en `paneles-v2.xml`; el archivo anterior se conserva y puede recuperarse desde el mismo menú.

**Control remoto** abre la conexión con QR, dispositivos activos y vista de teléfono. **Editar botones** conserva la personalización de los accesos. La web del mando comparte este diseño en móvil y PC, mantiene el orden del culto y controla la salida de la computadora. La preview muestra el texto bíblico o anuncio real; para videos y capturas muestra título y estado. La vinculación sigue usando el enlace privado del QR; cerrar la ventana de conexión mantiene el mando funcionando.

En **0.29.0** la consola adopta fondos carbón, acentos verdes, Lexend incluida y paneles y controles redondeados. Conserva los seis paneles movibles y sus distribuciones guardadas. Al reducir la ventana, los controles secundarios de salida se desplazan dentro de su panel y permanecen visibles la imagen, Logo y Fondo negro. El diseño de la consola no cambia el estilo que hayas elegido para los textos proyectados. La distribución 0.28.3 se conserva por separado.

En 0.28.3, al proyectar una captura se prepara el motor de archivos en segundo plano para adelantar su primera carga antes de cambiar de YouTube a un video de la PC. Durante esa preparación la captura sigue funcionando. Repetir la orden del mismo archivo mientras carga conserva el reproductor pendiente y su avance de carga. Una selección distinta sigue sustituyendo la solicitud anterior.

En 0.28.2, **YouTube → Editar captura → Guardar captura** conserva el recorte, posición y tamaño como ajuste para las siguientes capturas de YouTube, aunque cambie el video o reinicies el programa. Si hay una sola fuente guardada y una sola ventana compatible, la lista puede recuperarla tras cerrar y volver a abrir Chrome con otro título. Las capturas que ya tienen un recorte propio lo conservan. Cierra la versión anterior antes de abrir esta actualización; si el recorte anterior ya se perdió, ajústalo y guárdalo una vez.

En 0.28, al arrastrar contenido aparece una tarjeta con su título y miniatura junto al cursor. Mantén pulsado un elemento del orden de culto o de Secuencia y arrástralo hasta la posición deseada; la marca indica dónde quedará. Ambos paneles comparten el orden y guardan la nueva posición. También puedes añadir contenido desde Himnario, Biblia, Medios y YouTube. Esc cancela el arrastre sin alterar la salida.

En 0.27 las capturas tienen su pestaña **YouTube**, junto a Himnario/Biblia/Texto. Detecta ventanas de Chrome con YouTube en el título al abrir y actualiza la lista al entrar en la pestaña. Elegir una captura permite proyectarla, añadirla al orden y editarla. La detección por sí sola no inicia la captura ni cambia su audio. Las capturas guardadas anteriormente en la biblioteca se trasladan conservando sus recortes y posiciones. La reproducción real se controla desde YouTube: se elimina la pausa que solo congelaba la imagen.

**Mantener audio**, debajo de la salida, permite mostrar un texto, Biblia, imagen, Logo o negro mientras el video/audio/captura sigue avanzando y sonando. **Volver al video** o **F9** recupera esa misma fuente sin reiniciarla. Al enviar otro video/audio/captura se hace el fundido normal y se reemplaza la fuente conservada. Desactivar Mantener audio retira con fundido la fuente que suena detrás. El volumen y el silencio de salida también controlan esa fuente.

La captura conserva su último fotograma y se recupera automáticamente después de una pérdida temporal de imagen, minimización o reinicio de su proceso auxiliar. La reconexión comprueba la misma ventana, proceso y arranque; si cierras Chrome o cambias a otra ventana, elige la fuente nueva en YouTube. Una captura ya audible conserva su sonido durante la recuperación de imagen.

En 0.26, la captura se acomoda directamente en su vista previa: arrastra la imagen para moverla y sus tiradores para cambiar el tamaño. **Alt + arrastrar un tirador** recorta; **Shift + arrastrar** estira libremente; **Ctrl + arrastrar** desactiva el ajuste a los bordes y al centro. Ctrl+F ajusta a pantalla, Ctrl+S estira, Ctrl+D centra y Ctrl+R restablece. Las flechas mueven la fuente y Shift+flechas avanza diez píxeles. La imagen continúa en movimiento mientras editas y la transformación se conserva al añadir la captura al orden o guardarla en una plantilla. YouTube → Editar captura permite volver a ajustarla.

En 0.25.2 la primera carga del motor de video se realiza en segundo plano. Durante esa preparación la interfaz sigue disponible y muestra «Preparando reproductor…». Si eliges otro contenido mientras espera, se conserva tu última elección.

En el orden de culto, selecciona un elemento y pulsa **Supr/Delete** para quitarlo. Se guarda el cambio y se conserva la reproducción actual; la tecla se aplica cuando el foco está en la lista del orden.

La entrega **0.25.0** añade un **reproductor de salida en ventana**: doble clic sobre la imagen de «En pantalla» lo abre, con tamaño libre, pausa, avance, volumen, repetición y opción de fijarlo encima. Comparte la reproducción de la consola y no duplica el audio. Doble clic sobre su imagen o F11 alterna pantalla completa; Esc vuelve al tamaño anterior. Cerrar el reproductor conserva la proyección. Los controles de reproducción se ofrecen para archivos del programa; las capturas se controlan desde su aplicación original.

Desde 0.24 se incluye **Captura de ventana** en vivo, con vista previa, recorte y control del audio de la aplicación. Ahora está disponible en YouTube y en + Agregar del orden. La captura usa el motor gráfico OBS/WGC incluido, sin abrir OBS ni una ventana de proyección adicional.

La corrección **0.23.0** separa el volumen y silencio nativos de cada video para evitar cortes al precargar o retirar otro. Compensa la curva de volumen de VLC durante los fundidos, conservando el volumen elegido. Se verificó mediante captura de la salida real de Windows y pruebas nativas del compositor; mantiene los tiempos configurables y los valores predeterminados de 250 ms para audio y 300 ms para imagen.

La entrega **0.22.0** convierte Fondo iglesia en **Logo**: clic izquierdo para mostrarlo y derecho para editar su lista de imágenes, avance automático y tiempos. Conserva el explorador Medios de 0.21 y las transiciones de video anteriores.

La corrección **0.18.1** permite proyectar versículos con Enter desde el buscador o los resultados, y con doble clic sobre un resultado. Un clic sencillo sigue preparando sin proyectar.

La entrega **0.18.0** elimina la reproducción independiente de la vista previa y añade las acciones del clic izquierdo en el orden. Se publica por separado; la versión abierta no cambia hasta cerrarla y abrir la nueva. Verificación de compilación y pruebas sin interfaz; las interacciones y el reflejo nativo de PowerPoint requieren ensayo visual.

La entrega **0.17.0** se prepara por separado en `artifacts/CultoStudio-0.17.0`, con **Abrir Culto Studio 0.17.cmd**, para conservar la versión que está abierta. Incluye el motor Move y los cambios de PowerPoint descritos en [la integración nativa](docs/obs-move-native.md). Se ha limitado la verificación a código, compilación y pruebas sin interfaz por indicación del usuario: aún falta ejecutar el plugin/GPU y comprobar el audio y la barra de PowerPoint.

Una instalación nueva ofrece una plantilla sencilla con bienvenida, oración y despedida. La distribución del programa no incluye videos del himnario ni el texto completo de una traducción bíblica.

La consola tiene dos monitores: **Vista previa a la izquierda y En pantalla a la derecha**. En pantalla reproduce el contenido enviado incluso sin HDMI, incluidos el video, la disolución, la pausa, el negro y el fondo. Al activar HDMI, la pantalla externa usa el mismo reproductor, sin audio duplicado. Antes del primer envío muestra «Sin salida».

Vista previa, En pantalla, Orden del culto, Secuencia, Medios, Himnario, Biblia, Texto y YouTube son paneles independientes. Arrastra sus cabeceras para moverlos, acoplarlos a los bordes, agruparlos en pestañas o separarlos en ventanas; arrastra los separadores para cambiar su tamaño. El menú superior **Espacio de trabajo** permite activar cada panel, elegir una distribución, guardarla o restablecerla. Las posiciones también se guardan al cerrar y se recuperan al abrir, en `paneles-v2.xml`, aparte de los datos del culto. Los monitores y contenidos conservan su estado al moverlos.

En pantalla admite un ancho mínimo de 220 DIP. Al estrecharlo, el título y las acciones se ajustan manteniendo el video en 16:9. Si falta altura, desplaza los controles secundarios para acceder también a Logo y Negro. La imagen crece al ampliar el panel y cada control de reproducción puede mostrarse completo mediante el desplazamiento.

La distribución Principal sitúa las fuentes a la izquierda y los monitores y el orden a la derecha. Compacta conserva las fuentes agrupadas para ventanas pequeñas. Las tres usan gris carbón, acentos verdes, botones redondos, Lexend y transiciones breves que respetan la preferencia de animaciones de Windows.

Desde 0.8.1, el tamaño de los paneles y sus monitores se actualiza continuamente al arrastrar los separadores horizontales o verticales. El ajuste respeta sus tamaños mínimos y se conserva en la distribución guardada.

La secuencia inferior sigue el orden del culto: un clic prepara y doble clic proyecta. Los textos y versículos muestran su contenido en miniatura; las imágenes se cargan como vistas reducidas. Videos y audio usan portadas con título, sin abrir reproductores adicionales.

En esta PC ya se incorporaron a la biblioteca los **613 videos MKV** encontrados en `Documentos\Himnario Adventista 2022 VIDEOS\Data\Himnario Videos`. La instalación original se identifica como Himnario Adventista 2023. Los videos se conservan allí y la biblioteca guarda sus ubicaciones; no forman parte de la distribución del programa.

## Preparar un culto

**Para YouTube y otras ventanas:** abre el video en Chrome y entra en la pestaña **YouTube**. Elige una ventana detectada y pulsa Proyectar o haz doble clic. Editar captura abre el recorte visual; + Al orden guarda esa fuente y la agrega al culto. Elegir otra ventana permite capturar otras aplicaciones. También se conserva en las plantillas de culto. El navegador se controla desde su propia ventana.

La captura sigue actualizándose aunque otras ventanas la tapen. La ventana de origen debe permanecer abierta; contenido protegido o aplicaciones que suspenden su renderizado pueden impedir la captura. Se valida su identidad y nunca se cambia automáticamente a otra ventana durante una captura activa. Al minimizarla se conserva el último fotograma y al restaurarla se reanuda automáticamente. Si se cierra, elige la nueva ventana desde YouTube.

**Audio de la aplicación** controla el volumen de sus sesiones de Windows y sus procesos hijos, sin reproducir una segunda copia. Puede incluir otras pestañas del mismo navegador y conserva el dispositivo de sonido que el navegador ya usa. Al preparar una captura con audio, su fuente queda silenciada; al proyectarla entra con fundido y al retirarla se desvanece y permanece silenciada, salvo que esté activado Mantener audio. Tras quitarla de salida, vista previa y fondo sonoro, **YouTube → Devolver audio** restaura los niveles originales; cerrar Culto Studio también los restaura. El selector de ventanas, antes de añadir, no modifica el audio.

Las capturas no ofrecen pausa, avance, reinicio ni repetición desde la consola, el reproductor flotante o el teléfono; esos controles pertenecen al navegador. Disolución, corte, desplazamiento y zoom funcionan en vivo. Si el efecto global es Move, las entradas o salidas de una captura o de medios conservados usan disolución porque el host Move actual trabaja con imágenes estáticas.

1. Escribe el nombre del culto arriba. El menú **Archivo** reúne Nuevo, Abrir, Guardar culto, Cultos guardados y Guardar una copia.
2. En **Biblioteca**, importa los archivos o la carpeta del himnario. Se admiten MKV, MP4, MOV, WEBM, AVI, WMV, MP3, WAV, FLAC, OGG, JPG, JPEG, PNG, BMP y presentaciones PPT/PPTX/PPS/PPSX. PowerPoint requiere Microsoft PowerPoint instalado. La compatibilidad final depende del contenido interno del archivo; prueba los archivos que usarás antes del culto.
3. Los números al inicio del nombre se reconocen como números de himno. **Biblioteca → Editar el contenido seleccionado** permite corregir título y número. Los archivos permanecen en su carpeta original: conserva la ubicación y conecta cualquier disco externo antes de comenzar.
4. Usa **+ Agregar**, debajo del orden, para elegir himnos/biblioteca, Biblia, texto, archivos, una colección de videos o un culto guardado. Las tres primeras opciones abren su sección para buscar y pulsar **Añadir al orden**; la opción de archivos permite seleccionar varios y los incorpora directamente al orden y a la biblioteca. Reordena con Subir/Bajar o arrastrando. Las notas son privadas y no se proyectan.
5. En **Texto**, prepara anuncios, bienvenidas o letras. Puedes guardarlos en la biblioteca y el orden.

En la pestaña inferior **Medios**, **Videos**, **Imágenes** y **Descargas** muestran los archivos compatibles de esas carpetas de Windows, incluidas sus subcarpetas, y se actualizan cuando cambian los archivos. Se respetan las ubicaciones redirigidas, como Imágenes en OneDrive. Videos muestra videos; Imágenes muestra imágenes; Descargas reúne videos, imágenes, audio y presentaciones compatibles. El botón de carpeta permite elegir otra ubicación y el de actualizar vuelve a leerla. Estas vistas no importan archivos a la biblioteca ni los trasladan.

Los botones **Lista** y **Miniaturas** conservan la selección y no cambian la proyección. La lista muestra nombre de archivo, tipo y extensión; la galería muestra imágenes y miniaturas de video proporcionadas por Windows. Si el sistema no ofrece una miniatura para un formato o códec, aparece su icono y título. Se usa una caché limitada, carga fuera del hilo de interfaz y reciclado de las tarjetas visibles, sin reproducir videos para crear portadas. Los archivos disponibles solo en la nube usan miniaturas ya almacenadas para evitar descargas automáticas.

Un clic prepara, doble clic o Enter sobre un archivo lo proyecta, y **+ Al orden** o arrastrar crea una copia en el orden. La búsqueda filtra los archivos de la carpeta activa; **A–Z / Z–A** cambia su orden. Las flechas recorren la galería y Ctrl+F busca dentro de Medios. Se guardan la vista, la carpeta y la ordenación. Al actualizar desde una versión anterior, Medios se añade junto a Secuencia conservando la distribución de los demás paneles; la pestaña Secuencia continúa disponible como vista visual del culto.

**Todo, Himnos, Audio, Textos y PowerPoint** filtran la biblioteca guardada. En Todo o Himnos, escribir solo un número busca ese himno exacto: `12` o `012` encuentra el 12. En las demás categorías se busca por el título. **Enter** prepara el primer resultado visible; **F5** lo proyecta. Buscar por sí solo conserva la salida. **Limpiar** borra la búsqueda y conserva la categoría.

Arrastra un himno, un versículo de la Biblia o un archivo multimedia al **Orden del culto**. La línea violeta marca la posición; soltar en el espacio vacío lo añade al final, incluso cuando el orden está vacío. Se guarda una copia independiente del elemento en el culto, conservando la referencia al archivo original. Un clic prepara la vista previa y un doble clic lo reproduce en la salida. También puedes proyectar un archivo directamente con doble clic desde su carpeta.

El orden muestra la posición de cada momento y una marca **EN VIVO** en el elemento que está en salida. Un clic izquierdo muestra **Reproducir** y **Eliminar** debajo de la lista, para ese elemento. Eliminar solo lo retira del orden; conserva el archivo, la biblioteca y lo que ya está en pantalla. El menú **⋯** permite subir, bajar o quitar el seleccionado; también puedes arrastrarlo. **Notas del elemento** abre las notas privadas.

**Un clic en el orden** prepara la vista previa. **Doble clic sobre un elemento** lo envía a **En pantalla** e inicia su reproducción si es video/audio, incluso sin HDMI. El doble clic en espacio vacío no proyecta. Arrastrar dentro del orden reordena sus elementos. La salida local aparece dentro de la consola, sin otra ventana. Si el HDMI está activado, muestra el mismo contenido en las pantallas de la iglesia.

**Configuración → Transición del sistema…** permite guardar el efecto visual —Corte, Disolución, Desplazamiento, Zoom o **Move · OBS**—, su duración y los fundidos de audio de entrada y salida. Los valores iniciales son **Disolución de 0,30 s** y **0,25 s para cada fundido de audio**. Los controles avanzan en pasos de 0,05 s; **Restablecer** recupera esos valores al guardar. Guardar cambia las transiciones de los próximos envíos; conserva el contenido que ya está en pantalla. Al abrir esta versión se actualiza una sola vez la combinación predeterminada antigua (500/1500/1500 ms); otras combinaciones personalizadas se conservan.

Move usa el complemento oficial en un motor privado sin ventana. Anima dos capturas del compositor, incluido el fotograma de un video; al terminar reaparece la reproducción viva. No necesita conectar con la ventana del OBS instalado. La carga y el renderizado nativos todavía requieren comprobación; un error se comunica a la consola y no se sustituye por otro efecto.

La configuración se aplica al proyectar desde el orden, la secuencia, la biblioteca o el botón Proyectar, y al pasar entre videos, audio, textos, imágenes, negro y fondo. En Disolución, Desplazamiento, Zoom y Corte, el video anterior sigue reproduciéndose mientras el nuevo carga silenciado. Al recibir el primer fotograma comienza una mezcla con un solo reloj para imagen y ambos audios: sus duraciones se solapan, sin sumarse. Durante el cambio se usan temporalmente dos decodificadores VLC dentro del mismo compositor de salida; la vista previa comparte ese resultado sin otro reproductor. Si falla la carga, se conserva la salida anterior. Negro y fondo pausan el video al terminar el fundido y permiten recuperarlo en su posición. **F8** también usa la transición configurada.

**Move · OBS** conserva su motor nativo basado en capturas estáticas y no participa en esta mezcla de videos en movimiento. PowerPoint mantiene su reproducción y fundidos propios. Desde 0.23 se ejecutaron pruebas nativas de video y capturas de audio de la mezcla en los altavoces Realtek de esta PC. Queda pendiente comprobarla en la salida de audio física de la iglesia.

**Vista previa** sigue el contenido de **En pantalla** después de proyectar, usando el mismo compositor y reproductor, incluidos pausa, transiciones, negro y fondo. Al seleccionar otro elemento muestra **PREPARADO**: textos, Biblia e imágenes se dibujan sin enviarlos; videos y audio muestran una portada con título, sin abrir otro reproductor. **Ver salida** vuelve al contenido proyectado sin reiniciarlo. Al proyectar el preparado, la vista previa vuelve a seguir la salida.

Los controles de reproducción están únicamente en **En pantalla**: barra de avance, tiempo y duración, reproducción/pausa, saltos de **10 segundos**, reinicio, silencio, volumen y repetición. Arrastrar la barra conserva la pausa y mantiene la posición elegida hasta soltarla; también puede usarse con el teclado.

PowerPoint conserva una sola presentación nativa. Con HDMI se usa un reflejo DWM; localmente se solicita un recorte del compositor donde está incrustada. Ese recorte necesita comprobación real con Office/GPU. Si DWM rechaza el reflejo o los paneles se solapan, la vista previa indica **PowerPoint · ver En pantalla**, sin abrir otro reproductor ni alterar la presentación.

**Ctrl+1** abre Himnario y medios; **Ctrl+2**, Biblia; **Ctrl+3**, Texto. **Ctrl+F** enfoca y selecciona el buscador de la sección activa; en Texto selecciona el mensaje para editarlo.

## Colecciones y cultos guardados

**Biblioteca → Colecciones de videos…**, o el botón **Colecciones** de la biblioteca, abre las sesiones de videos guardadas. **Nueva colección** crea una sesión con nombre; **Añadir videos…** incorpora archivos y **Subir**, **Bajar** y **Quitar** organizan su secuencia. Para incorporar un video ya seleccionado en la biblioteca, usa **Biblioteca → Añadir video a una colección…**, elige la colección y pulsa **Añadir seleccionado**. **Guardar** conserva los cambios y **Cancelar** descarta la edición.

**Archivo → Guardar culto…** conserva una plantilla con el orden actual y sus notas. **Archivo → Cultos guardados…** permite renombrar, quitar o reorganizar esas plantillas. Ambas acciones también están en el menú **⋯** del orden. **+ Agregar** ofrece accesos a las colecciones y los cultos guardados.

En ambos diálogos, **Añadir al orden** incorpora el contenido al final y **Reemplazar orden** carga su secuencia completa. Cada uso crea elementos independientes de la plantilla. Reemplazar archiva el orden anterior en `Historial` y conserva la salida que está en vivo. Las colecciones y plantillas guardan referencias a los archivos; no copian los videos.

## Biblia

En esta PC ya está cargada **Reina-Valera 1960 completa**, con **66 libros, 1.189 capítulos y 31.102 versículos**, disponible sin internet. Se importó desde el archivo local `Documentos\Easy Worshipp\Programa EasyWorship\Biblias\RV1960.ewb`, que se conserva en su ubicación original.

En **Biblia**, elige **Libro, Capítulo y Versículo**, o escribe un capítulo (`Juan 3`), una cita (`Juan 3:16`, `1 Juan 2:1-3`) o una frase. La búsqueda se actualiza al escribir y admite abreviaturas como `Jn3:16`, `Sal 23` y `1co13:4-7`, además de separadores como `Juan 3 16`. **Enter en el buscador** resuelve la búsqueda y proyecta el pasaje completo si escribiste un rango; para un capítulo o frase proyecta el primer resultado. **Enter en la lista** proyecta la selección completa. Un doble clic sobre un miembro de la selección proyecta el grupo. Un clic sencillo, escribir, cambiar de capítulo o pulsar Buscar solo preparan contenido. Una búsqueda vacía, inválida o sin resultados conserva la salida.

Para agrupar versículos escribe **Génesis 1:1-5**, o selecciona el primero y usa **Shift + clic** en el último; **Shift + ↑/↓** amplía o reduce la selección dentro de la lista. El grupo se prepara como un único contenido con versículos numerados y una referencia común. Los controles **Siguiente/Anterior** continúan después o antes del grupo, también sin HDMI. **+ Selección al orden** o arrastrar uno de los miembros seleccionados añade el grupo como un solo elemento, conservando los textos originales. **Separados (N)** sigue permitiendo añadir todos los resultados de una cita como elementos individuales.

En el buscador, un clic sobre el **libro**, el **capítulo** o los **versículos** selecciona solo esa parte; **Tab/Shift+Tab** recorre las partes antes de salir del campo. Al enfocar una cita o después de enviarla con Enter se selecciona el nombre del libro, incluidos nombres como «1 Corintios». **Ctrl+A** selecciona toda la consulta. Las búsquedas por palabras conservan la edición normal.

Con un solo resultado seleccionado, el botón muestra **+ Versículo al orden**; con varios, **+ Selección al orden**. También se pueden agrupar resultados de una búsqueda por palabras: la referencia identifica sus ubicaciones sin incluir versículos no seleccionados.

El botón **Estilo** abre un editor con vista previa para elegir fuente, tamaño, colores de texto y fondo, imagen de fondo, negrita, cursiva, alineación y efectos de texto. **Guardar** conserva el estilo para los versículos y actualiza la vista preparada; la lectura en vivo adopta el cambio en el siguiente envío. **Cancelar** conserva el estilo anterior. La transición bíblica usa **Del sistema** de forma predeterminada; puede sustituirse por **Corte** o **Disolver** con duración propia.

El texto se guarda con los datos locales, fuera de Git y de la distribución ejecutable. La copia importada identifica la versión y registra la atribución y procedencia. Consulta el [informe de importación y procedencia](docs/importacion-biblia-local.md) para conocer la conversión y las comprobaciones realizadas.

Para incorporar otra Biblia, abre **Biblioteca → Importar Biblia** y elige JSON o Zefania XML. Consulta [el formato de importación](docs/formato-biblia.md).

## Proyectar a las tres pantallas

Las tres pantallas que reciben el mismo HDMI muestran una sola salida. Configura Windows con **Extender** para conservar la consola en el monitor del operador. En **En pantalla → Pantalla…**, selecciona la pantalla externa; también puedes pulsar **Activar HDMI**. Se muestra el contenido local actual, conservando su posición y estado. Si todavía no enviaste contenido, se muestra un fondo vacío.

- **Preparado** es una vista previa silenciosa. Buscar y seleccionar no cambian lo que está en pantalla.
- **Proyectar a pantalla / F5**, debajo de la vista previa, envía el contenido preparado. A la derecha quedan juntos el estado de salida, la orientación **Siguiente**, la navegación y los controles de pantalla.
- **Siguiente / F6** y **Anterior / F7** avanzan desde el elemento en vivo, aunque hayas preparado otro contenido.
- **Fondo negro**, debajo del reproductor, desvanece el audio y lleva la imagen a negro con la transición del sistema. **F8** usa ese mismo comportamiento.
- **Logo**, junto a Fondo negro: clic izquierdo muestra la presentación con disolución; clic derecho abre el editor. También puedes editarlo en **Configuración → Logo…**. Agrega, quita y ordena imágenes, selecciona la inicial y guarda el **tiempo por imagen** (1–3600 segundos) y la **duración de transición** (100–3000 ms). Los valores iniciales son **10 s y 300 ms**. Quitar de la lista no elimina archivos; Cancelar conserva la configuración anterior.
- Las imágenes avanzan en círculo y el tiempo de permanencia empieza después de terminar cada disolución. Una sola imagen permanece fija. La duración visual de Logo es independiente de la transición global; conserva los fundidos de audio del sistema al entrar desde un video. Las imágenes ausentes o dañadas se omiten; si no hay ninguna disponible, se informa sin sustituir contenido nuevo. Cambiar de contenido, poner negro o cerrar la salida detiene el avance. El botón Logo del teléfono activa la misma presentación.
- Pulsar otra vez **Fondo negro**, o elegir **Configuración → Volver al contenido**, restaura el contenido anterior conservando el video pausado en su posición. **Reproducir** continúa desde ahí. F8 desde Logo pone negro y, al quitarlo, inicia Logo de nuevo; el botón Fondo negro conserva su funcionamiento de fondo exclusivo.
- Volumen, posición, Reiniciar y Repetir actúan sobre la reproducción de salida, no sobre la vista previa.

Los controles de reproducción aparecen cuando la salida tiene un video o audio. **Pausar previa** aparece solo al preparar contenido multimedia. El estado **NEGRO** o **FONDO** sigue visible aunque prepares otro contenido.

Desde 0.11, **Activar HDMI / Desactivar HDMI** controla únicamente la salida externa. La proyección ocupa la pantalla externa completa, sin bordes ni entrada propia en la barra de tareas. El monitor que contiene la consola queda excluido, aunque no sea el principal de Windows.

Sin HDMI, el estado **LOCAL** identifica la reproducción dentro del programa. El contenido, la disolución, los controles y el mando siguen funcionando. Para mostrarlo fuera, conecta el HDMI en modo Extender y actívalo. Las tres pantallas conectadas al mismo HDMI reciben la misma imagen.

Desactivar o desconectar el HDMI conserva la reproducción local. Activarlo usa el mismo reproductor, sin reiniciar el video ni duplicar audio. **Negro** oculta el contenido y pausa la reproducción tanto local como externa; **Continuar** reanuda tras restaurar la imagen. **Proyectar** o un doble clic envían el preparado y reproducen los videos.

El monitor «En pantalla» muestra el reproductor local; con HDMI activado refleja la ventana externa real mediante DWM. Esa ventana debe permanecer sin minimizar para que Windows mantenga el reflejo actualizado.

**Biblioteca → Capturar una pantalla** toma una imagen estática: la consola se minimiza durante 1,5 segundos y luego permite revisar la imagen. No hay una fuente general para capturar ventanas de otras aplicaciones.

## PowerPoint y presentador USB

Los archivos de PowerPoint aparecen en la categoría **PowerPoint** y pueden añadirse al orden como cualquier contenido. La reproducción usa **Microsoft PowerPoint instalado en Windows**, que conserva sus animaciones, transiciones y videos incrustados. El programa abre una copia propia de solo lectura; cerrar esa salida no cierra las presentaciones del usuario. La vista preparada puede mostrar la miniatura incluida en el archivo. En modo local, **En pantalla** aloja la ventana nativa dentro del panel para conservar el dibujo de animaciones y video. Al activar HDMI, esa misma ventana pasa a la pantalla externa y el panel la refleja mediante DWM.

**Siguiente** y **Anterior** recorren los pasos de animación y las diapositivas de la presentación activa. PowerPoint conserva sus controles de pausa y reinicio. Sus efectos y videos se reproducen con el motor de Office. En 0.17, un adaptador aplica los tiempos de fundido a `MediaFormat.Volume` de los medios de la copia propia; conserva sus niveles originales y evita modificar la sesión de audio compartida de Office. El efecto audible durante reproducción todavía no se ha comprobado en ejecución.

La pausa intenta ocultar solamente las barras nativas cuya cadena de propietarios identifica inequívocamente la presentación propia. La propiedad y desaparición de la barra flotante observada siguen pendientes de verificación; no se ocultan barras globales ni de otras presentaciones.

En **Configuración → Presentador USB**, activa **Control en segundo plano** para avanzar la salida aunque estés trabajando en otra aplicación. De forma predeterminada se utilizan **Av Pág / Re Pág**; **Usar flechas izquierda y derecha** cambia a esas dos teclas. Mientras el control está activo, las teclas elegidas quedan reservadas globalmente para Culto Studio, incluidas las del teclado normal. Desactivarlo devuelve las teclas a la aplicación que tenga el foco. La selección se guarda en esta PC.

Un presentador que envíe esas teclas puede recorrer el orden o los pasos de PowerPoint. Si otra aplicación ya tiene reservadas las teclas, la consola muestra el error de activación. No se requiere que la consola tenga el foco para recibirlas.

## Control desde el teléfono

1. Conecta PC y teléfono a la misma red local.
2. Abre **Configuración → Mando móvil → Activar mando**.
3. Selecciona la dirección IP de la PC que corresponde a esa red y escanea el QR con el teléfono.
4. En el teléfono aparece el **orden del culto** con los mismos títulos y posiciones que en la PC. Toca un elemento para enviarlo a **En pantalla** y reproducirlo; funciona también en salida local sin HDMI. La marca del elemento activo se actualiza desde la consola, junto con el estado Local/HDMI, Negro o Fondo.
5. Desde el teléfono puedes usar **Anterior, Siguiente, Negro, Fondo, Pausar/Continuar, Reiniciar, posición y volumen**, además de proyectar lo preparado en la PC. Los cambios del orden realizados en la PC aparecen automáticamente en el teléfono. Si se pierde conexión, las acciones no se reenvían solas.
6. En **Organizar botones** selecciona un acceso a la izquierda y edita su nombre, acción y color a la derecha. Para vincular un himno, elige **Proyectar un contenido** y escribe el comienzo de su número o título en el selector de contenido. Pulsa **Guardar botón** para aplicar los cambios.

Si cambias de Wi-Fi, pulsa **Actualizar red** y escanea el QR actualizado. El selector prioriza conexiones con puerta de enlace y excluye direcciones locales que no sirven para conectar el teléfono.

El mando utiliza el puerto TCP **18765**. No necesita internet y sólo permite acciones propias de la consola. La vinculación cambia al desactivar y activar el mando. No compartas el enlace fuera del equipo que dirige el culto. Usa una red local de confianza; esta versión usa HTTP dentro de esa red.

La aplicación no configura el firewall. Si Windows solicita acceso, el usuario debe decidir si permite el programa en su **red privada**. Si el teléfono no abre el mando, comprueba la misma Wi-Fi, dirección elegida, aislamiento de clientes del router y permiso del firewall. No hace falta abrir puertos del router hacia internet.

Si el teléfono pierde conexión, la proyección continúa y los controles de la PC siguen disponibles. **Desactivar mando** invalida el enlace sin cerrar la salida.

## Guardado y copias

Los datos se guardan en `%LOCALAPPDATA%\CultoStudio`, fuera de la carpeta del programa. El orden, biblioteca, botones y preferencias se guardan automáticamente. Las colecciones, plantillas de cultos, estilo bíblico, transiciones y opciones del presentador se conservan en el mismo espacio de trabajo. Los archivos JSON mantienen una copia `.bak` para recuperación. Al crear, abrir o reemplazar un culto, el anterior se archiva en la carpeta `Historial`.

**Archivo → Guardar una copia… / Ctrl+S** exporta una copia del orden; no copia los videos. **Guardar culto…** conserva una plantilla dentro del programa. Para trasladar el contenido a otra computadora debes conservar también los archivos multimedia y ajustar/importar sus ubicaciones. Si un archivo falta, la aplicación avisa antes de cambiar la proyección.

Si falla el guardado local al cerrar, la opción predeterminada mantiene abierta la consola. Puedes exportar el orden a otra ubicación y después elegir expresamente cerrar sin guardar localmente; esa exportación no respalda cambios de biblioteca, botones o configuración.

## Desarrollo y comprobaciones

El SDK .NET 10 se encuentra en `.tools/dotnet` en esta computadora. También puedes instalar el SDK .NET 10 oficial. El SDK no forma parte de la distribución para el operador.

```powershell
.\scripts\build.ps1
.\scripts\prepare-obs-move.ps1
.\scripts\build.ps1 -Publish -OutputDirectory artifacts\CultoStudio-0.27.0
```

El primer comando compila Release y ejecuta las pruebas del núcleo y del servidor. El segundo descarga y verifica el runtime portable de OBS/Move sin ejecutarlo ni instalarlo. `-Publish` genera además una distribución autónoma Windows x64; `-OutputDirectory artifacts\CultoStudio-0.17.0` permite publicarla aparte. `-Smoke` ejecuta las pruebas de integración de escritorio, que abren ventanas de ensayo y usan una carpeta temporal aislada; no se ejecutó para 0.17 debido a la restricción de no controlar aplicaciones.

```powershell
.\scripts\build.ps1 -Smoke
node tests/Culto.Remote.Tests/mobile-smoke.cjs
```

Los tests automatizados de Biblia emplean textos sintéticos y nunca los presentan como traducción real. La Biblia local instalada se comprobó por separado, como recoge el [informe de importación](docs/importacion-biblia-local.md). El ensayo de reproducción utiliza un MKV corto generado localmente. Las pantallas HDMI y el teléfono físico de la iglesia deben verificarse antes de usar esta versión en un culto.

Resultados y límites de la comprobación: [validación de esta entrega](docs/validacion.md).

## Alcance de esta versión

No incluye transmisión, grabación, mezcla de cámaras, importación directa de bases de EasyWorship ni salidas independientes para escenario. La integración nativa de Move se limita a su transición entre capturas completas; no importa proyectos OBS, no ofrece sus filtros adicionales ni es un cargador general de complementos. Desplazamiento y Zoom siguen siendo efectos propios. La Biblia RVR1960 y los videos del himnario ya incorporados en esta PC se guardan por separado del programa.

La Yamaha MG12XU puede usarse como interfaz de audio USB mediante la salida de sonido de Windows. Sus controles analógicos no funcionan como mando remoto de Culto Studio; no hay integración de esos controles para avanzar contenidos o mover controles de la consola.

Código organizado en `src/Culto.Core`, `src/Culto.Desktop` y `src/Culto.Remote`. Diseño y plan en `docs/superpowers`. Dependencias: [WPF/.NET](https://dotnet.microsoft.com/), [LibVLCSharp/VideoLAN](https://docs.videolan.me/libvlcsharp/), [QRCoder](https://github.com/codebude/QRCoder). Véase [componentes de terceros](THIRD-PARTY-NOTICES.md).
