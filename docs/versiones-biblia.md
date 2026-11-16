# Versiones de Biblia instaladas en esta PC

Actualizado el 15 de noviembre de 2026. El panel Biblia permite elegir una versión, conserva la cita seleccionada y guarda la última versión activa. El botón `+` admite uno o varios archivos JSON o Zefania XML. Una importación nueva conserva las versiones anteriores. El texto proyectado y las entradas existentes del orden mantienen su versión original hasta que el operador envía otro contenido.

Los textos están en `%LOCALAPPDATA%\CultoStudio\Biblias`, separados por versión. `biblia.json` conserva la versión activa y la compatibilidad con las instalaciones anteriores. La instalación de las versiones adicionales no modificó ese archivo ni la selección RVR1960. Los textos bíblicos privados no se incorporan al repositorio ni al ejecutable.

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
