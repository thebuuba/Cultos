# Actualizaciones de Culto Studio

Objetivo: comprobar versiones estables de GitHub Releases al abrir y desde Configuración, avisar sin interrumpir la proyección, descargar con progreso y verificar tamaño/SHA-256 antes de abrir el instalador con aceptación explícita.

Canal: repositorio público thebuuba/Cultos, asset fijo `update.json` en la última release estable. La configuración del canal se comparte entre el cliente y el empaquetador. Sin tokens ni perfiles en los archivos publicados. No se instala al abrir, detener una proyección requiere confirmación explícita y no se repiten descargas concurrentes. El instalador conserva su identificador y carpeta por usuario.

- [x] Cliente HTTPS, versiones y descarga verificada con pruebas de errores, cancelación y contenido alterado.
- [x] Aviso discreto, ventana de descarga y acción manual; cierre normal para guardar el culto antes de abrir el instalador.
- [x] Manifiesto de release generado junto al EXE, publicación reproducible, pruebas y documentación de límites reales.

La publicación en GitHub se realiza después de preparar y verificar el resultado. El instalador anterior no sabe buscar actualizaciones y requiere instalar esta primera versión una vez.
