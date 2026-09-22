# Cultos

Aplicación de escritorio nativa para Windows destinada a preparar y presentar el contenido visual de cultos sin conexión a internet.

## Estado

La rama `paneles` contiene la versión **0.2.0**, orientada a comportamiento de aplicación Windows y estabilidad durante el culto.

Tecnología:

- WPF sobre .NET 8 para Windows.
- SQLite local.
- Funcionamiento completamente offline.
- Instalador Inno Setup x64.
- Ventana independiente para la pantalla de congregación.

## Comportamiento de escritorio

- Instancia única: si Cultos ya está abierto, una segunda ejecución activa la ventana existente.
- Los archivos `.cultos` quedan asociados al programa y pueden abrirse con doble clic.
- Tamaño, posición, maximizado, último módulo, volumen y preferencias de pantalla se guardan localmente.
- Recuperación del último culto activo y detección de cierres inesperados.
- Registro de errores en `%LOCALAPPDATA%\Cultos\logs`.
- Arrastrar y soltar imágenes, videos y copias `.cultos` desde el Explorador de Windows.
- Icono propio en el ejecutable e instalador.
- Interfaz adaptable a escalado de Windows y tamaños de pantalla más pequeños.

## Pantalla de congregación

- Selección persistente del monitor de congregación.
- Identificación visual de pantallas desde Configuración.
- Reacción a conexión o desconexión de monitores.
- Posicionamiento mediante API nativa de Windows para funcionar correctamente con DPI distintos.
- La salida externa no roba el foco del operador.
- Con dos o más pantallas, la salida se abre sin bordes y a pantalla completa.
- Con una sola pantalla, F5 abre un modo de prueba en ventana para no bloquear al operador.
- La pantalla negra es temporal: pulsar `B` vuelve a mostrar exactamente el contenido que estaba en vivo.

## Presentación y multimedia

- Vista previa independiente de la salida en vivo.
- Navegación por elementos del orden del culto.
- Paginación automática de textos largos, letras e himnos.
- Anterior/Siguiente recorre primero las diapositivas del elemento actual.
- Reproducción, pausa y detención de video.
- Control de volumen.
- Barra de posición y tiempo del video.
- Sincronización de posición con la pantalla externa.
- Manejo visible de videos dañados o códecs no compatibles.
- Detección de archivos multimedia movidos o eliminados.
- Pantalla negra, limpiar salida, quitar texto y mostrar logotipo.

## Datos locales

Los datos se guardan en:

```text
%LOCALAPPDATA%\Cultos
```

Incluye:

- `cultos.db`: base SQLite.
- `settings.json`: preferencias de la aplicación.
- `logs\cultos.log`: registro de errores.
- `session.running`: marcador temporal usado para detectar cierres inesperados.

SQLite utiliza claves foráneas y migraciones versionadas para permitir actualizar el esquema sin recrear la base.

## Atajos

- `←` / `→`: navegar.
- `Espacio`: enviar la vista previa en vivo.
- `B`: activar o restaurar pantalla negra.
- `C`: limpiar la salida.
- `F5`: abrir o recuperar la pantalla de congregación.
- `Escape`: restaurar contenido cuando la pantalla negra está activa.

## Ejecutar desde el código

Requisitos: Windows 10/11 y .NET 8 SDK.

```powershell
dotnet restore Cultos.sln
dotnet run --project src/Cultos.App/Cultos.App.csproj
```

## Compilar y probar

```powershell
dotnet restore Cultos.sln
dotnet build Cultos.sln -c Release
dotnet test tests/Cultos.Tests/Cultos.Tests.csproj -c Release
```

Publicación autónoma:

```powershell
dotnet publish src/Cultos.App/Cultos.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish
```

El instalador se genera como:

```text
artifacts/installer/Cultos-Setup-0.2.0.exe
```

## Integración continua

GitHub Actions valida en Windows:

1. Restauración de dependencias.
2. Compilación Release.
3. Pruebas automatizadas.
4. Publicación autónoma `win-x64`.
5. Generación del instalador con Inno Setup.
6. Publicación de los artefactos `Cultos-win-x64` y `Cultos-Setup`.

## Alcance pendiente fuera de esta fase

La base de escritorio y estabilidad de Windows de esta fase está terminada. Quedan como trabajo de producto separado:

- Importar bibliotecas completas de Biblia e Himnario con licencia o dominio público.
- Editor visual avanzado de temas y fondos.
- Miniaturas y relocalización avanzada de bibliotecas multimedia.
- Firma digital del instalador cuando exista un certificado de firma de código.
- Validación física final con el hardware concreto de la iglesia y sus proyectores/televisores.
