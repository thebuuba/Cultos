# Notas locales y limpieza del orden

**Objetivo:** vaciar el orden desde su menú, simplificar Biblia e Himnario y sustituir Texto por un cuaderno local de varias notas.

**Diseño:** conservar el identificador de panel `text` para respetar las distribuciones guardadas. Guardar notas independientes de los contenidos proyectables dentro del espacio local existente, con búsqueda, lista, editor y autoguardado. Vaciar el orden requiere confirmación y conserva la salida actual.

- [x] Añadir pruebas de persistencia de notas, independencia del orden y controles retirados.
- [x] Crear el modelo de notas en Services y el editor en `MainWindow.Notes.cs` y XAML.
- [x] Retirar controles bíblicos y filtros solicitados; adaptar accesos de teclado y nombres del panel.
- [x] Añadir la acción de vaciar el orden y comprobar persistencia sin detener la salida.
- [x] Ejecutar pruebas funcionales, revisión visual y compilación antes de actualizar la instalación.
