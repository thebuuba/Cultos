# Dos monitores y paneles acoplables

**Objetivo:** vista previa a la izquierda y monitor real de salida a la derecha, con paneles que se puedan mover, separar, agrupar y redimensionar; conservar la distribución elegida.

**Diseño:** AvalonDock 5.0.1 para cinco paneles (previa, salida, orden, fuentes y secuencia). Los controles existentes permanecen vivos durante los cambios de distribución. La distribución inicial muestra ambos monitores arriba, orden/secuencia abajo y fuentes a la derecha para conservar espacio para los selectores y resultados de Biblia. El monitor de salida usa una miniatura nativa DWM de la ventana de proyección, sin otro reproductor ni audio duplicado. La configuración de paneles se guarda aparte de los datos del culto y puede restablecerse desde el menú superior.

**Restricciones:** WPF y Windows x64; conservar gris/violeta, botones redondos, fuentes, acciones y datos locales. No añadir instrucciones dentro de la interfaz. Los cambios de panel no proyectan ni modifican el orden. Las ventanas flotantes deben poder recuperarse al cambiar de pantallas.

## Ejecución

- [x] Añadir prueba de los dos marcos y observar su fallo antes de implementar.
- [x] Integrar AvalonDock y convertir los cinco bloques actuales en paneles, conservando nombres y eventos. Añadir menú Espacio de trabajo con acceso a paneles y restablecimiento.
- [x] Implementar `OutputMonitor` para enlazar/desenlazar el HWND de salida, actualizar dimensiones, DPI y ventana contenedora al moverlo. Verificar texto, video, negro, fondo, pausa y cierre.
- [x] Guardar/restaurar XML validado con identificación fija de los cinco paneles, escritura atómica y recuperación ante configuración dañada. Conservar los controles y su estado al restaurar.
- [x] Adaptar pruebas de geometría a la nueva distribución; probar movimiento, ventana flotante, persistencia y restablecimiento sin alterar sesión ni datos.
- [x] Ejecutar suites, publicar y comprobar con la interfaz nativa ambos monitores y un arrastre real. Actualizar documentación y avisos de dependencias.

## Archivos

`MainWindow.xaml`, `MainWindow.Docking.cs`, `MainWindow.Output.cs`, `MainWindow.xaml.cs`, `MainWindow.Usability.cs`: composición e integración. `Projection/OutputMonitor.cs`: miniatura DWM y ciclo de vida. `tests/Culto.Desktop.Smoke/DockingTests.cs` y pruebas de geometría: cobertura. `Culto.Desktop.csproj`: dependencias/versionado.

## Fuentes técnicas

- https://dirkster99.github.io/AvalonDock/getting-started/quick-start/
- https://dirkster99.github.io/AvalonDock/guides/serialization/
- https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw
