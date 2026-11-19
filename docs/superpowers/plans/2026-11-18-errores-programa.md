# Correcciones del programa

Objetivo: resolver los problemas reportados de reproducción, escenas, monitores, texto y Biblia conservando los datos existentes.

- [x] Reproducir el cambio de video con una lista activa, con salida externa y al volver desde Logo; corregir la selección de video y verificar continuidad del mismo decodificador.
- [x] Añadir «Vista previa» al menú de las escenas sin modificar la salida; actualizar la preparación al editar una escena.
- [x] Guardar una opción para volver a Videos al regresar al panel Medios, activada inicialmente.
- [x] Sustituir las proporciones fijas por la proporción de la pantalla de salida y añadir ampliar, ajustar, rellenar y zoom.
- [x] Permitir texto por página de Tema/Propósito y hacer visible el acceso existente para añadir texto al orden.
- [x] Colapsar una selección bíblica con clic sencillo, conservar arrastre y verificar búsquedas de rangos con espacios.
- [x] Comprobar una fuente íntegra para NVI e instalarla solo si supera la validación de importación.
- [x] Compilar, ejecutar pruebas Core/Remote y ensayos específicos de escritorio; publicar en la instalación local si está cerrada.

Implementación: cambios sobre los flujos existentes de WPF/.NET 10, sin dependencias nuevas. Las áreas independientes de Biblia, monitores y títulos se verifican por separado antes de integrar. Las pruebas de escritorio usan datos temporales y volumen silenciado.

Verificación: compilación Release sin advertencias ni errores; 393 pruebas de Core y 78 de Remote. Ensayos de escritorio de reproducción reportada, monitores compactos, escenas editables, Biblia, orden compacto, listas, reproductor, ventana flotante y paneles: sin fallos. Suite completa del renderer: aprobada; se actualizó una expectativa antigua de fondo de logo a negro, sin alterar el comportamiento del producto. Prueba de cambio de video con salida completa en la segunda pantalla detectada (1280 × 720); consola en 1920 × 1080. NVI instalada mediante BibleVersionStore, con revisión de la fuente local y reparaciones documentadas en docs/versiones-biblia.md.
