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
| Lexend | 1.007; Regular (400), Medium (500), SemiBold (600), Bold (700) | SIL Open Font License 1.1; https://github.com/google/fonts/tree/e2332cf862ac3145c0ee5f24f04f4c1819b2410b/ofl/lexend; licencia incluida en `Assets/Licenses/Lexend-OFL.txt` |
| OBS / libobs y módulos de imagen/transición | 33.0.1, Windows x64 | GPL; https://github.com/obsproject/obs-studio/tree/33.0.1; licencias, origen y hashes en `Runtime/ObsMove` |
| Move Transition, Exeldro | 3.2.1, Windows x64 | GPL; https://github.com/exeldro/obs-move-transition/tree/3.2.1; licencias, origen y hashes en `Runtime/ObsMove` |

Las cuatro caras de Lexend son instancias estáticas de `Lexend[wght].ttf`, preparadas con fontTools 4.68.0 a los pesos indicados. Conservan la atribución de The Lexend Project Authors y la licencia OFL original. La revisión de Google Fonts queda fijada en el enlace anterior; el SHA-256 del archivo variable de origen es `3add53e641fbc81da64da4bb254285e2831b52b029527bc0714e2b9610832ee6`. fontTools se usa solo para preparar los archivos, sin dependencia de ejecución.

Hashes SHA-256 de las caras estáticas distribuidas:

| Archivo | SHA-256 |
|---|---|
| `Lexend-Regular.ttf` | `fafea252b74809731da7ae9d8af9dd74df3b35077e37247e3120cf544541a85d` |
| `Lexend-Medium.ttf` | `f5342990dd528ce5a936c5da131a81a373994c2b98072b408fc0461628b3fb24` |
| `Lexend-SemiBold.ttf` | `e8d14c5d3d7a23f39ace22caa00f0e504ac28c71803d8d391fb554703213f816` |
| `Lexend-Bold.ttf` | `1787926b2eeffa8040b057b3c0766efc75682e01b5e73349253832c883a06be1` |

El motor Move se entrega en un proceso separado, `MoveHost`, con las bibliotecas portables oficiales de OBS y Move. No redistribuye una instalación modificada del OBS del usuario. Conserva íntegros los avisos del runtime y de sus dependencias (incluido Qt) y el manifiesto de procedencia. El script `scripts/prepare-obs-move.ps1` identifica las versiones y archivos originales mediante SHA-256. El código del host y su protocolo están en este repositorio; las fuentes de las dependencias corresponden a las etiquetas enlazadas.

LibVLC se distribuye como bibliotecas dinámicas y módulos separados. Sus fuentes y licencias están disponibles en los enlaces oficiales anteriores. Conserva los avisos y licencias que acompañan los paquetes. Los paquetes originales se obtienen de NuGet, bajo el propietario correspondiente.

Las dependencias de pruebas xUnit y Microsoft.NET.Test.Sdk sólo se usan durante desarrollo. No se incluyen Biblias ni videos comerciales dentro de la aplicación.
