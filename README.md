# Cultos

Aplicación WPF para Windows destinada a preparar y presentar el contenido visual de un culto sin conexión a internet. Esta entrega es una primera vertical funcional; no pretende afirmar que todo el alcance del brief está terminado.

## Ejecutar desde el código

Requisitos: Windows 10/11 y .NET 8 SDK.

```powershell
dotnet restore Cultos.sln
dotnet run --project src/Cultos.App/Cultos.App.csproj
```

Los datos locales se guardan en `%LOCALAPPDATA%\Cultos\cultos.db`. La aplicación no usa cuentas, servidor ni telemetría propia.

## Compilar y probar

```powershell
dotnet test Cultos.sln
dotnet publish src/Cultos.App/Cultos.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish
```

El instalador generado está en `artifacts/installer/Cultos-Setup-0.1.0.exe`.

## Funciones terminadas en esta entrega

- Interfaz principal oscura en español, adaptable y orientada al operador.
- Base SQLite local con migración inicial y contenido demostrativo.
- Recuperación automática del último culto activo.
- Orden del culto con agregar, eliminar, subir y bajar elementos.
- Búsqueda de versículos por referencia o texto.
- Búsqueda de himnos demostrativos por número, título o letra.
- Vista previa separada de la salida en vivo.
- Ventana de congregación sin bordes y sin controles.
- Selección automática del monitor secundario cuando está disponible.
- Pantalla negra, limpiar, quitar texto y mostrar logotipo.
- Atajos: flechas, espacio, B, C, Escape y F5.
- Importación de referencias a archivos multimedia y detección de rutas ausentes en la capa de datos.
- Guardado automático, exportación e importación de copias `.cultos`.
- Ejecutable autónomo e instalador de Windows x64.
- Pruebas automatizadas para persistencia, búsquedas, paginación, multimedia ausente, recuperación y copias.

## Pendiente para una versión de producción

- Asistente visual de primer inicio y edición completa de la configuración.
- Gestión visual de varios cultos guardados, duplicado y renombrado.
- Editor completo de canciones, himnos y texto libre.
- Reproducción real de video con transporte y volumen.
- Miniaturas de multimedia, carpetas y relocalización interactiva.
- Editor visual de temas y fondos.
- División y navegación visual de múltiples diapositivas por elemento.
- Selección manual persistente del monitor y recuperación automática ante desconexión en caliente.
- Importadores autorizados de Biblia e Himnario.
- Pruebas UI y validación manual con dos monitores físicos.
- Firma digital del instalador.

## Pantalla de congregación

Conecta el proyector o televisor y configura Windows en modo **Extender estas pantallas**. Abre Cultos y pulsa **Configurar pantalla** o `F5`. La salida se abre sin bordes en el primer monitor secundario; si solo existe uno, se abre allí para poder probarla. La selección en la biblioteca solo cambia la vista previa. Debes pulsar **ENVIAR EN VIVO** o la barra espaciadora para cambiar la pantalla pública.

## Datos demostrativos

Los versículos y letras incluidos están identificados como demostración y su volumen es deliberadamente limitado. La arquitectura queda preparada para importar bibliotecas con licencia o de dominio público.
