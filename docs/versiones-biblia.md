# Versiones de Biblia instaladas en esta PC

Actualizado el 15 de noviembre de 2026. El panel Biblia permite elegir una versión, conserva la cita seleccionada y guarda la última versión activa. El botón `+` admite uno o varios archivos JSON o Zefania XML. Una importación nueva conserva las versiones anteriores. El texto proyectado y las entradas existentes del orden mantienen su versión original hasta que el operador envía otro contenido.

Los textos están en `%LOCALAPPDATA%\CultoStudio\Biblias`, separados por versión. `biblia.json` conserva la versión activa y la compatibilidad con las instalaciones anteriores. La instalación de las versiones adicionales no modificó ese archivo ni la selección RVR1960. Los textos bíblicos privados no se incorporan al repositorio ni al ejecutable.

Las versiones se seleccionan en una fila horizontal debajo de los versículos, con las siglas de cada traducción y la versión activa resaltada. El nombre completo aparece al pasar el cursor. La fila permite desplazamiento horizontal cuando las versiones no caben en el panel y selección con el teclado.

Libro, capítulo, versículo y navegación entre capítulos ocupan una sola fila compacta. El editor de estilo comparte las tarjetas, botones y colores del editor de transiciones, con secciones Texto, Referencia, Fondo y Transición y una vista previa fija. En Referencia se puede elegir una fuente independiente para el libro y la cita; «Misma fuente del texto» conserva el comportamiento anterior. La elección se guarda en el espacio de trabajo y se aplica a la vista preparada y a la siguiente proyección.

En **Estilo de la Biblia → Transición** también están **Subida suave**, **Entrada por líneas** y **Enfoque suave**. Animan el texto con el fondo fijo y la referencia aparece con un pequeño retraso. Se puede ajustar la duración entre 100 y 2.000 ms y usar **Probar transición** antes de guardar. Entrada por líneas respeta las líneas ajustadas por la fuente, el tamaño y la alineación elegidos. Cambiar de versículo, poner negro o cerrar la salida cancela la animación anterior.

El catálogo ofrece **291 efectos de texto**: los tres anteriores y 288 presets (12 familias × tres alcances × ocho variantes). Incluye Deslizamiento, Barrido, Apertura radial, Acercamiento, Rotación, Inclinación, Rebote, Resorte, Desenfoque, Onda, Pliegue y Desenrollado, aplicados al pasaje completo, a sus líneas o a sus palabras. Del sistema, Corte y Disolver continúan disponibles aparte.

El selector permite buscar con o sin tildes, filtrar por familia y guardar hasta 64 favoritos con la estrella. Los favoritos se conservan al guardar el estilo; Cancelar no modifica los anteriores. Los filtros no cambian el efecto activo hasta elegir otro. Los pasajes extensos utilizan líneas en lugar de palabras para limitar el trabajo a 80 fragmentos, sin omitir contenido. Al terminar vuelve el texto nativo con su fuente y efectos originales.

| Versión instalada | Registros de texto | Fuente |
|---|---:|---|
| Reina-Valera 1960 (RVR1960) | 31.102 | Importación local existente, documentada en `importacion-biblia-local.md` |
| La Biblia de las Américas (LBLA) | 31.103 | Archivo local del usuario `lbla.ewb` |
| Reina-Valera 1909 (RV1909) | 31.084 | [eBible](https://ebible.org/bible/details.php?id=spaRV1909) |
| Biblica® Open Nueva Biblia Viva 2008 | 29.102 | [eBible / Biblica](https://ebible.org/bible/details.php?id=spaonbv) |
| La Biblia en Español Sencillo 2026 (BES) | 31.102 | [eBible / AudioBiblia](https://ebible.org/bible/details.php?id=spabes) |
| Reina-Valera Gómez (RVG) | 31.102 | [eBible](https://ebible.org/bible/details.php?id=sparvg) |
| Palabra de Dios para ti (PDDPT) | 31.081 | [eBible](https://ebible.org/bible/details.php?id=spapddpt) |
| Biblia libre Latinoamericano (BLL) | 31.098 | [eBible](https://ebible.org/bible/details.php?id=spabll) |
| Biblia libre para el mundo (BLM) | 31.098 | [eBible](https://ebible.org/bible/details.php?id=spablm) |
| Versión Biblia Libre (VBL) | 31.086 | [eBible](https://ebible.org/bible/details.php?id=spavbl) |
| Valera 1602 Purificada | 31.091 | [eBible](https://ebible.org/bible/details.php?id=spav1602p) |

Se utilizan los 66 libros que admite actualmente la aplicación. BLL y BLM también ofrecen libros adicionales en la fuente; esta copia contiene únicamente esos 66 libros, y la selección queda indicada en su atribución. Las referencias sin texto en los archivos VPL de RV1909 y VBL no se rellenan con otro texto ni se fabrican: se enumeran en la atribución. Los recuentos entre traducciones pueden variar. Todas las copias instaladas contienen los 66 libros y los 1.189 capítulos.

Se conservan los avisos de autoría y las condiciones publicados en los archivos `_about.htm` de cada paquete. Pueden consultarse en el texto informativo de la versión dentro del panel. La LBLA se convirtió desde el archivo ya existente del usuario, sin descargar ni distribuir otra copia de su texto. Las descargas originales y el manifiesto de sumas SHA-256 de fuentes y conversiones permanecen en `.tools/bible-sources`; las conversiones usan `scripts/prepare-spanish-bibles.py` y se validaron además con el `BibleCatalog` real antes de instalarlas.

## Fuentes pendientes

No se instalaron todas las traducciones españolas existentes. El archivo local `nvi.ewb` fue rechazado por referencias ambiguas: Números 26:43 aparece con dos textos distintos y Abdías contiene el capítulo duplicado con variantes. El original permanece intacto. No se sustituyeron esos textos ni se eligió una variante arbitrariamente. La NVI necesita una fuente corregida. Para otras ediciones, incluidas NTV y DHH, hace falta un archivo utilizable; el selector no tiene un límite fijo de versiones.

## Comprobaciones

- `BibleVersionStoreTests`: coexistencia, reimportación, persistencia, nombres de archivo seguros y aislamiento de archivos dañados.
- `BibleGroupedVerseTests`: referencias agrupadas sin duplicar texto, cobertura de rangos y rechazo de solapamientos.
- `--bible-versions`: cambio de versión, conservación de cita/rango y consulta, versión ausente, persistencia y protección del texto proyectado y del orden.
- La prueba con `CULTO_BIBLE_LIBRARY_PROBE` cargó las once versiones reales en una instalación temporal y comprobó Juan 3:16 en cada una. Registro `.tools/bible-installed-ui.log`; captura `.tools/bible-version-selector.png`.
- La copia activa conservó su SHA-256 durante la instalación: `.tools/bible-install.log`.
- Pasaron 309 pruebas de Core y la suite de búsquedas, selección y estilos `--bible-tools`. Durante esta última se corrigió que la palabra «amor» se interpretaba como una errata de «Amós» incluso cuando había coincidencias de texto; los nombres exactos y las abreviaturas de libros mantienen su prioridad.

### Cambio de versión y fondo de la fila — 16 de noviembre de 2026

Cada selección releía las once Biblias desde el hilo de la interfaz y reconstruía las pestañas. Ahora conserva la lista instalada y carga y guarda únicamente la versión elegida fuera de ese hilo. La plantilla de la fila conserva el fondo oscuro durante la carga; el foco señala el borde de la pestaña sin sustituir su verde de selección por un fondo claro.

En esta PC, con copias temporales de las once versiones, el ensayo anterior registró cambios de 0,8–1,5 s y un bloqueo máximo de la interfaz de 1.439 ms. El ensayo final registró cambios de 408–567 ms y un intervalo máximo de 65 ms entre pulsos del Dispatcher. Son mediciones locales, no una garantía para otros equipos. Registros: `.tools/bible-switch-red.log` y `.tools/bible-switch-final.log`. `--bible-versions` comprueba también el fondo durante la carga y el color de la pestaña con foco.
