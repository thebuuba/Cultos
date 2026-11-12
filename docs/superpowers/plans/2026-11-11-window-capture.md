# Captura de ventana para Culto Studio

**Goal:** Añadir una fuente de ventana en movimiento al orden del culto, con recorte y audio controlado en origen.

**Architecture:** El motor OBS aislado ya incluido captura por HWND mediante Windows Graphics Capture. Envía BGRA por el protocolo local existente a un compositor WPF. El navegador conserva su reproducción y dispositivo; un controlador de sesiones de audio aplica la envolvente sin duplicar sonido. No controla el volumen maestro de Windows.

**Tech Stack:** WPF/.NET 10, libobs/libobs-winrt 33.0.1, canal local con acceso del usuario actual, WASAPI/NAudio.

**Alcance aprobado:** Ventana YouTube, imagen y audio, entrada al orden y transición existente. Selección explícita, sin capturar automáticamente otras ventanas. La interfaz conserva el estilo y usa etiquetas breves. El audio puede abarcar otras pestañas de la misma aplicación. No se promete captura de ventanas minimizadas ni contenido protegido.

- [x] Motor: captura exacta, recorte, 30 fps, cola acotada, cierre/cancelación y errores; conservar Move.
- [x] Selector: ventanas abiertas, vista previa silenciosa, recorte, cursor, opción de audio y añadir al orden.
- [x] Audio: sesiones del proceso y descendientes, ganancias combinadas, silencio al salir, restauración al cerrar.
- [x] Integración: persistir fuente en copias/plantillas, compositor con fundidos, vista previa y transporte apropiados.
- [x] Verificación: pruebas de persistencia, captura nativa con ventana sintética ocluida, cierre y recorte; audio de proceso propio; regresiones de mezcla; publicar y comprobar interfaz.

Se trabaja en los archivos separados indicados a cada subagente; no se cambia el orden del usuario para ensayos automatizados. La validación nativa usa fuentes sintéticas propias y permite una comprobación final explícita de navegador.
