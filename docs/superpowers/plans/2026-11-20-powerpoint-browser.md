# Explorador de PowerPoint basado en la referencia

**Objetivo:** lista de presentaciones con portada, búsqueda y datos; miniaturas reales para elegir inicio o saltar en vivo; controles de proyección, negro, detener y moderador.

**Referencia y aprobación:** https://dev-untitled-pro-kdzi99xs.polymet.app/; el usuario confirmó copiar también la lista de varias presentaciones. Sustituye su preferencia anterior de mostrar solo una. Se conserva la selección por fondo, sin contorno, y la rama experimental de tema lima.

**Arquitectura:** reutilizar biblioteca, Cue y reproductor nativo existentes. Leer metadatos y portadas Open XML; exportar miniaturas reales con una copia propia de PowerPoint en un hilo STA, con cancelación y caché por archivo/modificación. No sustituir animaciones o videos por imágenes en la salida.

**Restricciones:** sin dependencias nuevas; sin borrar los archivos originales ni perder ajustes/cultos. No auto-proyectar al seleccionar un archivo. Cambiar de diapositiva preparada no altera otro archivo que esté en pantalla. Sin commit del experimento salvo petición del usuario.

- [x] Escribir regresión de biblioteca con varias presentaciones, búsqueda y controles del explorador; observar el fallo.
- [x] Añadir metadatos/portadas y exportación STA de miniaturas con cancelación y caché; comprobar archivos sintéticos y render de Office.
- [x] Implementar lista, menú, arrastre, miniaturas y navegación de preparado/en vivo; conservar controles nativos, audio y moderador.
- [x] Verificar regresión, geometría compacta, saltos nativos y conservación de archivos/datos; inspeccionar capturas.
- [x] Publicar la prueba, guardar respaldo del perfil, reiniciar normalmente y verificar el resultado.
