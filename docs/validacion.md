# Validación de Culto Studio

Comprobación realizada el 11 de noviembre de 2026 en esta PC Windows. Entrega autónoma Windows x64 en `artifacts/CultoStudio`; iniciador `Abrir Culto Studio.cmd`.

## Compilación y pruebas

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
