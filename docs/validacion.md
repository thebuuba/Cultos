# Validación de Culto Studio

Comprobación realizada el 11 de noviembre de 2026 en esta PC Windows. Entrega autónoma Windows x64 en `artifacts/CultoStudio`; iniciador `Abrir Culto Studio.cmd`.

## Compilación y pruebas

Se ejecutó `scripts/build.ps1 -Smoke -Publish`: salida 0, compilación Release sin errores ni advertencias, publicación con runtime .NET y LibVLC incluidos.

| Componente | Resultado |
|---|---|
| Núcleo: sesión, Biblia, multimedia y recuperación de archivos | 44 pruebas correctas |
| Servidor del mando: HTTP, autenticación, validación y deduplicación | 39 pruebas correctas |
| Integración nativa WPF: orden, Biblia, salida, mando, recuperación y diseño | 23 comprobaciones correctas |
| Reproductor nativo: texto, imagen, MKV, audio, negro y pausa | 24 comprobaciones correctas |
| Cliente móvil: `node tests/Culto.Remote.Tests/mobile-smoke.cjs` | PASS |

Los dos procesos de ensayo nativo terminaron con código 0. Total en la versión 0.2: 130 pruebas/comprobaciones, más el ensayo JavaScript de la primera entrega (su código no cambió). El ensayo JavaScript usa un DOM mínimo: no sustituye una comprobación visual en navegador o teléfono.

Después se ajustó únicamente el desplazamiento vertical del panel de conexión del mando, se volvió a publicar y se comprobó visualmente que las instrucciones completas son accesibles.

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

## Verificaciones pendientes y alcance

- Probar las tres pantallas conectadas al HDMI y el dispositivo de audio real de la iglesia.
- Probar el mando en el teléfono físico y su red Wi-Fi. No se cambiaron firewall ni router.
- Importar una fuente autorizada de Reina-Valera 1960. El importador y la búsqueda están implementados; el texto completo no está incluido ni cargado.
- Ensayar el orden y cada archivo que se utilizará antes del culto.

Esta versión ofrece capturas estáticas; no incorpora captura continua, transmisión, grabación ni PowerPoint. El panel «En pantalla» resume el contenido y estado; no duplica el video en vivo. Estos límites también se explican en la guía.
