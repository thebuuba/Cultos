# Versiones de Biblia en español

Implementar en el flujo existente de importación y consulta. Conservar RVR1960 y toda lectura proyectada al instalar o seleccionar otra traducción.

- [x] Almacén local de versiones independientes en `Biblias`, nombres de archivo seguros, reimportación de una versión y recuperación de archivos dañados; pruebas en Core.
- [x] Selector visible y accesible en el panel Biblia. Importación múltiple, conservación de cita/consulta y persistencia de la versión activa mediante `biblia.json`; pruebas de escritorio.
- [x] Incorporar los textos españoles locales legibles y todas las traducciones españolas descargables de la fuente eBible que permitan este uso. Conservar metadatos y atribución; validar libros, referencias únicas, texto no vacío y cargas con BibleCatalog. No etiquetar una versión inexistente como instalada.
- [x] Ejecutar pruebas de almacenamiento, cambio de versión y regresiones de Biblia; compilar y publicar la aplicación sin interrumpir la sesión abierta. Documentar exactamente los textos incorporados y cualquier edición pendiente por falta de fuente utilizable.

Entrega 0.42.0 publicada en la instalación habitual `artifacts/CultoStudio`: 309 pruebas Core, 66 Remote, pruebas de Biblia y las once versiones reales aprobadas. La sesión del usuario ya estaba cerrada al publicar. Se mantuvo la selección RVR1960; NVI quedó pendiente por errores en la fuente local. Detalles en `docs/versiones-biblia.md`.

Se reutilizan `BibleCatalog`, `JsonFileStore`, `InstallBibleCatalog` y los controles WPF existentes. No se añaden dependencias. Los textos locales privados no se incluyen en Git.
