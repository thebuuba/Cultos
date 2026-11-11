# Culto Studio

Primera versión de una consola nativa para Windows destinada a preparar y proyectar cultos. Reúne el orden del culto, himnario en video, imágenes, textos, Biblia importable y mando desde el teléfono. No depende de OBS.

## Abrir el programa

Ejecuta **Abrir Culto Studio.cmd** en esta carpeta, o abre `artifacts/CultoStudio/CultoStudio.exe`. La distribución publicada incluye .NET y el reproductor nativo; conserva todos los archivos de esa carpeta junto al ejecutable. Windows x64.

Una instalación nueva ofrece una plantilla sencilla con bienvenida, oración y despedida. La distribución del programa no incluye videos del himnario ni el texto completo de una traducción bíblica.

La consola tiene dos monitores: **Vista previa a la izquierda y En pantalla a la derecha**. En pantalla reproduce el contenido enviado incluso sin HDMI, incluidos el video, la disolución, la pausa, el negro y el fondo. Al activar HDMI, la pantalla externa usa el mismo reproductor, sin audio duplicado. Antes del primer envío muestra «Sin salida».

Vista previa, En pantalla, Orden del culto, Secuencia y Biblioteca/Biblia son paneles independientes. Arrastra sus cabeceras para moverlos, acoplarlos a los bordes, agruparlos en pestañas o separarlos en ventanas; arrastra los separadores para cambiar su tamaño. El menú superior **Espacio de trabajo** permite activar cada panel, guardar la distribución o restablecerla. La distribución también se guarda al cerrar y se recupera al abrir, en `paneles.xml`, aparte de los datos del culto. Los monitores y contenidos conservan su estado al moverlos.

En pantalla admite un ancho mínimo de 220 píxeles. Al estrecharlo, el título, los botones y la barra de reproducción se reorganizan en filas, manteniendo el video en 16:9. Si falta altura, se desplazan los controles secundarios; el video, Negro y Fondo permanecen visibles. Las distribuciones guardadas recuperan mínimos adecuados para cada panel, evitando que el de salida quede recortado a la derecha.

La distribución inicial sitúa los monitores arriba, el orden y la secuencia debajo, y las fuentes a la derecha. Conserva el gris con acentos violeta, los botones redondos, Poppins y las transiciones breves que respetan la preferencia de animaciones de Windows.

Desde 0.8.1, el tamaño de los paneles y sus monitores se actualiza continuamente al arrastrar los separadores horizontales o verticales. El ajuste respeta sus tamaños mínimos y se conserva en la distribución guardada.

La secuencia inferior sigue el orden del culto: un clic prepara y doble clic proyecta. Los textos y versículos muestran su contenido en miniatura; las imágenes se cargan como vistas reducidas. Videos y audio usan portadas con título, sin abrir reproductores adicionales.

En esta PC ya se incorporaron a la biblioteca los **613 videos MKV** encontrados en `Documentos\Himnario Adventista 2022 VIDEOS\Data\Himnario Videos`. La instalación original se identifica como Himnario Adventista 2023. Los videos se conservan allí y la biblioteca guarda sus ubicaciones; no forman parte de la distribución del programa.

## Preparar un culto

1. Escribe el nombre del culto arriba. El menú **Archivo** reúne Nuevo, Abrir y Guardar una copia.
2. En **Biblioteca**, importa los archivos o la carpeta del himnario. Se admiten MKV, MP4, MOV, WEBM, AVI, WMV, MP3, WAV, FLAC, OGG, JPG, JPEG, PNG y BMP. La compatibilidad final depende del contenido interno del archivo; prueba los videos que usarás antes del culto.
3. Los números al inicio del nombre se reconocen como números de himno. **Biblioteca → Editar el contenido seleccionado** permite corregir título y número. Los archivos permanecen en su carpeta original: conserva la ubicación y conecta cualquier disco externo antes de comenzar.
4. Usa **+ Agregar**, debajo del orden, para elegir himnos/biblioteca, Biblia, texto o archivos. Las tres primeras opciones abren su sección para buscar y pulsar **Añadir al orden**; la opción de archivos permite seleccionar varios y los incorpora directamente al orden y a la biblioteca. Reordena con Subir/Bajar o arrastrando. Las notas son privadas y no se proyectan.
5. En **Texto**, prepara anuncios, bienvenidas o letras. Puedes guardarlos en la biblioteca y el orden.

**Videos**, **Imágenes** y **Descargas** muestran los archivos multimedia de esas carpetas de Windows, incluidas las subcarpetas, y se actualizan cuando cambian los archivos. Se respetan las ubicaciones redirigidas, como Imágenes en OneDrive. Videos muestra videos; Imágenes muestra imágenes; Descargas reúne videos, imágenes y audio compatibles. El botón circular de actualizar vuelve a leer la carpeta. Estas vistas no importan archivos a la biblioteca ni los trasladan.

**Todo, Himnos, Audio y Textos** filtran la biblioteca guardada. En Todo o Himnos, escribir solo un número busca ese himno exacto: `12` o `012` encuentra el 12. En las demás categorías se busca por el título. **Enter** prepara el primer resultado visible; **F5** lo proyecta. Buscar por sí solo conserva la salida. **Limpiar** borra la búsqueda y conserva la categoría.

Arrastra un himno, un versículo de la Biblia o un archivo multimedia al **Orden del culto**. La línea violeta marca la posición; soltar en el espacio vacío lo añade al final, incluso cuando el orden está vacío. Se guarda una copia independiente del elemento en el culto, conservando la referencia al archivo original. Un clic prepara la vista previa y un doble clic lo reproduce en la salida. También puedes proyectar un archivo directamente con doble clic desde su carpeta.

El orden muestra la posición de cada momento y una marca **EN VIVO** en el elemento que está en salida. El menú **⋯** permite subir, bajar o quitar el seleccionado; también puedes arrastrarlo. **Notas del elemento** abre las notas privadas. Puedes ajustar los paneles arrastrando los separadores y leer títulos largos al colocar el puntero encima.

**Un clic en el orden** prepara la vista previa. **Doble clic sobre un elemento** lo envía a **En pantalla** e inicia su reproducción si es video/audio, incluso sin HDMI. El doble clic en espacio vacío no proyecta. Arrastrar dentro del orden reordena sus elementos. La salida local aparece dentro de la consola, sin otra ventana. Si el HDMI está activado, muestra el mismo contenido en las pantallas de la iglesia.

Desde 0.10, los himnos y videos enviados desde el orden, la secuencia o la biblioteca entran con una **disolución de 400 ms**. También se aplica al botón Proyectar. La última imagen de la salida se conserva mientras carga el nuevo video y se desvanece cuando llega su primer fotograma; el nuevo video continúa reproduciéndose durante la mezcla. F8 la interrumpe inmediatamente. Es una transición visual; no mezcla dos pistas de audio.

Desde 0.13, el audio de salida baja suavemente durante **180 ms** antes de cambiar de contenido y el nuevo video/audio entra durante **350 ms**. Se usa un solo reproductor de salida, sin superponer pistas. F8, Pausar y los comandos inmediatos del mando interrumpen la transición. La vista previa permanece silenciosa.

Ambos reproductores tienen una barra de avance a todo el ancho, tiempo transcurrido y duración, botón central de reproducción/pausa, saltos de **10 segundos** y reinicio. La salida añade silencio/restauración del volumen anterior, volumen y repetición. Los controles de la vista previa sólo afectan a esa vista; los de **En pantalla** actúan sobre la salida. Arrastrar la barra mantiene la posición elegida hasta soltarla y conserva la pausa; también puede usarse con el teclado.

**Ctrl+1** abre Himnario y medios; **Ctrl+2**, Biblia; **Ctrl+3**, Texto. **Ctrl+F** enfoca y selecciona el buscador de la sección activa; en Texto selecciona el mensaje para editarlo.

## Biblia

En esta PC ya está cargada **Reina-Valera 1960 completa**, con **66 libros, 1.189 capítulos y 31.102 versículos**, disponible sin internet. Se importó desde el archivo local `Documentos\Easy Worshipp\Programa EasyWorship\Biblias\RV1960.ewb`, que se conserva en su ubicación original.

En **Biblia**, elige **Libro, Capítulo y Versículo**, o escribe un capítulo (`Juan 3`), una cita (`Juan 3:16`, `1 Juan 2:1-3`) o una frase. Seleccionar prepara el versículo; Proyectar lo envía a la salida. La búsqueda no interrumpe la lectura que ya está en pantalla.

**+ Versículo al orden** añade solamente el seleccionado. **Pasaje (N)** añade todos los versículos del capítulo o rango mostrado y aparece cuando hay varios; las búsquedas por frase permiten añadir el resultado seleccionado.

El texto se guarda con los datos locales, fuera de Git y de la distribución ejecutable. La copia importada identifica la versión y registra la atribución y procedencia. Consulta el [informe de importación y procedencia](docs/importacion-biblia-local.md) para conocer la conversión y las comprobaciones realizadas.

Para incorporar otra Biblia, abre **Biblioteca → Importar Biblia** y elige JSON o Zefania XML. Consulta [el formato de importación](docs/formato-biblia.md).

## Proyectar a las tres pantallas

Las tres pantallas que reciben el mismo HDMI muestran una sola salida. Configura Windows con **Extender** para conservar la consola en el monitor del operador. En **En pantalla → Pantalla…**, selecciona la pantalla externa; también puedes pulsar **Activar HDMI**. Se muestra el contenido local actual, conservando su posición y estado. Si todavía no enviaste contenido, se muestra un fondo vacío.

- **Preparado** es una vista previa silenciosa. Buscar y seleccionar no cambian lo que está en pantalla.
- **Proyectar a pantalla / F5**, debajo de la vista previa, envía el contenido preparado. A la derecha quedan juntos el estado de salida, la orientación **Siguiente**, la navegación y los controles de pantalla.
- **Siguiente / F6** y **Anterior / F7** avanzan desde el elemento en vivo, aunque hayas preparado otro contenido.
- **Fondo negro**, debajo del reproductor, desvanece el audio y lleva la imagen a negro con disolución. **F8** conserva el corte inmediato.
- **Fondo iglesia**, junto a Fondo negro, abre una lista con miniaturas. Toca una imagen para enviarla con disolución. **Agregar imágenes** incorpora varias imágenes a la lista; **Sin imagen** usa un fondo sencillo. La lista se guarda en esta PC y también está disponible en **Configuración → Fondos de iglesia…**. Quitar una imagen de la lista no elimina el archivo.
- Pulsar otra vez **Fondo negro**, o elegir **Volver al contenido** en la lista, restaura el contenido anterior conservando el video pausado en su posición. **Reproducir** continúa desde ahí; la vista previa permanece independiente.
- Volumen, posición, Reiniciar y Repetir actúan sobre la reproducción de salida, no sobre la vista previa.

Los controles de reproducción aparecen cuando la salida tiene un video o audio. **Pausar previa** aparece solo al preparar contenido multimedia. El estado **NEGRO** o **FONDO** sigue visible aunque prepares otro contenido.

Desde 0.11, **Activar HDMI / Desactivar HDMI** controla únicamente la salida externa. La proyección ocupa la pantalla externa completa, sin bordes ni entrada propia en la barra de tareas. El monitor que contiene la consola queda excluido, aunque no sea el principal de Windows.

Sin HDMI, el estado **LOCAL** identifica la reproducción dentro del programa. El contenido, la disolución, los controles y el mando siguen funcionando. Para mostrarlo fuera, conecta el HDMI en modo Extender y actívalo. Las tres pantallas conectadas al mismo HDMI reciben la misma imagen.

Desactivar o desconectar el HDMI conserva la reproducción local. Activarlo usa el mismo reproductor, sin reiniciar el video ni duplicar audio. **Negro** oculta el contenido y pausa la reproducción tanto local como externa; **Continuar** reanuda tras restaurar la imagen. **Proyectar** o un doble clic envían el preparado y reproducen los videos.

El monitor «En pantalla» muestra el reproductor local; con HDMI activado refleja la ventana externa real mediante DWM. Esa ventana debe permanecer sin minimizar para que Windows mantenga el reflejo actualizado.

**Biblioteca → Capturar una pantalla** toma una imagen estática: la consola se minimiza durante 1,5 segundos y luego permite revisar la imagen. Esta versión no incluye captura continua de ventanas.

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

Los datos se guardan en `%LOCALAPPDATA%\CultoStudio`, fuera de la carpeta del programa. El orden, biblioteca, botones y preferencias se guardan automáticamente. Los archivos JSON mantienen una copia `.bak` para recuperación. Al crear o abrir un culto, el anterior se archiva en la carpeta `Historial`.

**Guardar** exporta una copia del orden; no copia los videos. Para trasladarlo a otra computadora debes conservar también los archivos multimedia y ajustar/importar sus ubicaciones. Si un archivo falta, la aplicación avisa antes de cambiar la proyección.

Si falla el guardado local al cerrar, la opción predeterminada mantiene abierta la consola. Puedes exportar el orden a otra ubicación y después elegir expresamente cerrar sin guardar localmente; esa exportación no respalda cambios de biblioteca, botones o configuración.

## Desarrollo y comprobaciones

El SDK .NET 10 se encuentra en `.tools/dotnet` en esta computadora. También puedes instalar el SDK .NET 10 oficial. El SDK no forma parte de la distribución para el operador.

```powershell
.\scripts\build.ps1
.\scripts\build.ps1 -Publish
```

El primer comando compila Release y ejecuta las pruebas del núcleo y del servidor. `-Publish` genera además una distribución autónoma Windows x64. `-Smoke` ejecuta las pruebas de integración de escritorio, que abren ventanas de ensayo y usan una carpeta temporal aislada.

```powershell
.\scripts\build.ps1 -Smoke
node tests/Culto.Remote.Tests/mobile-smoke.cjs
```

Los tests automatizados de Biblia emplean textos sintéticos y nunca los presentan como traducción real. La Biblia local instalada se comprobó por separado, como recoge el [informe de importación](docs/importacion-biblia-local.md). El ensayo de reproducción utiliza un MKV corto generado localmente. Las pantallas HDMI y el teléfono físico de la iglesia deben verificarse antes de usar esta versión en un culto.

Resultados y límites de la comprobación: [validación de esta entrega](docs/validacion.md).

## Alcance de esta versión

No incluye transmisión, grabación, mezcla de cámaras, captura continua, importación directa de bases de EasyWorship, reproducción de PowerPoint ni salidas independientes para escenario. La Biblia RVR1960 y los videos del himnario ya incorporados en esta PC se guardan por separado del programa. Es una primera versión funcional para ensayar y ajustar al equipo real.

Código organizado en `src/Culto.Core`, `src/Culto.Desktop` y `src/Culto.Remote`. Diseño y plan en `docs/superpowers`. Dependencias: [WPF/.NET](https://dotnet.microsoft.com/), [LibVLCSharp/VideoLAN](https://docs.videolan.me/libvlcsharp/), [QRCoder](https://github.com/codebude/QRCoder). Véase [componentes de terceros](THIRD-PARTY-NOTICES.md).
