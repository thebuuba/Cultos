# Componentes de terceros

Culto Studio integra componentes con sus respectivas licencias. No se atribuye la autoría de estos componentes.

| Componente | Versión | Licencia / origen |
|---|---|---|
| .NET y WPF | 10 | MIT y avisos incluidos en la distribución .NET; https://github.com/dotnet/runtime |
| LibVLCSharp.WPF | 3.10.2 | LGPL-2.1-or-later; https://code.videolan.org/videolan/LibVLCSharp |
| VideoLAN.LibVLC.Windows | 3.0.24 | Bibliotecas y módulos VideoLAN con licencias individuales; https://www.videolan.org/legal.html |
| QRCoder | 1.8.1 | MIT; https://github.com/codebude/QRCoder |
| NAudio.Core y NAudio.Wasapi | 2.2.1 | MIT; https://github.com/naudio/NAudio/tree/v2.2.1; copia incluida en `Assets/Licenses/NAudio.txt` |
| AvalonDock, Core, Serializer.Xml y Themes.Arc | 5.0.1 | Microsoft Public License (Ms-PL), según los archivos LICENSE de estos paquetes; https://github.com/Dirkster99/AvalonDock; copia incluida en `Assets/Licenses/AvalonDock.txt` |
| Poppins | Regular, Medium, SemiBold, Bold | SIL Open Font License 1.1; https://github.com/google/fonts/tree/main/ofl/poppins; licencia incluida en `Assets/Fonts/OFL.txt` |
| OBS / libobs y módulos de imagen/transición | 33.0.1, Windows x64 | GPL; https://github.com/obsproject/obs-studio/tree/33.0.1; licencias, origen y hashes en `Runtime/ObsMove` |
| Move Transition, Exeldro | 3.2.1, Windows x64 | GPL; https://github.com/exeldro/obs-move-transition/tree/3.2.1; licencias, origen y hashes en `Runtime/ObsMove` |

El motor Move se entrega en un proceso separado, `MoveHost`, con las bibliotecas portables oficiales de OBS y Move. No redistribuye una instalación modificada del OBS del usuario. Conserva íntegros los avisos del runtime y de sus dependencias (incluido Qt) y el manifiesto de procedencia. El script `scripts/prepare-obs-move.ps1` identifica las versiones y archivos originales mediante SHA-256. El código del host y su protocolo están en este repositorio; las fuentes de las dependencias corresponden a las etiquetas enlazadas.

LibVLC se distribuye como bibliotecas dinámicas y módulos separados. Sus fuentes y licencias están disponibles en los enlaces oficiales anteriores. Conserva los avisos y licencias que acompañan los paquetes. Los paquetes originales se obtienen de NuGet, bajo el propietario correspondiente.

Las dependencias de pruebas xUnit y Microsoft.NET.Test.Sdk sólo se usan durante desarrollo. No se incluyen Biblias ni videos comerciales dentro de la aplicación.
