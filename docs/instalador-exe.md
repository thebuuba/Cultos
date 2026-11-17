# Instalador de Culto Studio para Windows

Abre `CultoStudio-0.43.0.0-Windows-x64-Setup.exe` y sigue el asistente en español. Instala el programa para el usuario actual, sin pedir permisos de administrador, y permite crear un acceso en el Escritorio. Incluye acceso en Inicio y desinstalador en Configuración de Windows → Aplicaciones. Desde la versión 0.43.0 también permite [recibir avisos de nuevas versiones](actualizaciones.md).

La carpeta predeterminada es `%LOCALAPPDATA%\Programs\CultoStudio`. Los datos permanecen separados en `%LOCALAPPDATA%\CultoStudio`: actualizar y desinstalar el programa conserva ese perfil. Para actualizar, cierra Culto Studio y ejecuta el nuevo instalador sobre la misma carpeta.

El EXE contiene .NET, el navegador integrado, los motores de reproducción y el instalador del plugin de Stream Deck. No descarga componentes durante la instalación. Chrome, Microsoft PowerPoint y la aplicación de Elgato siguen siendo opcionales externos. El himnario, las Biblias y los fondos del paquete completo de varios GB se distribuyen mediante el ZIP completo; el EXE no los incluye.

El instalador no tiene firma digital de editor. No elimina las advertencias de Windows sobre software sin firma. Comparte también el archivo `.sha256` y los avisos de terceros del paquete.

## Generar de nuevo

Necesitas Inno Setup 6 y una publicación limpia de Windows x64. El mismo script conserva la opción de generar ZIP:

```powershell
./scripts/build-streamdeck.ps1
./scripts/build.ps1 -Publish -OutputDirectory .tools/installer-publish
./scripts/package-release.ps1 -SourceDirectory .tools/installer-publish -Installer -CompilerPath .tools/InnoSetup/ISCC.exe
./tests/installer-smoke.ps1 -InstallerPath artifacts/releases/CultoStudio-0.43.0.0-Windows-x64-Setup.exe
```

El empaquetado rechaza perfiles personales, cachés y enlaces, excluye archivos de depuración y genera SHA-256. La prueba instala en una carpeta temporal, comprueba dependencias y acceso directo, actualiza, desinstala y verifica que el perfil del usuario no cambia. Se detiene si ya existe una instalación registrada con el mismo identificador para no reemplazarla.
