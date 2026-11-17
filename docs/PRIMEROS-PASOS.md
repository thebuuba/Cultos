# Primeros pasos con Culto Studio

Culto Studio prepara y proyecta textos, imágenes, videos y otros contenidos para el culto. Cada equipo conserva su propia configuración y biblioteca.

## Requisitos

- Windows 10 u 11 de 64 bits, en un equipo Intel o AMD.
- 8 GB de memoria recomendados; 16 GB para trabajar con videos y varias fuentes.
- El paquete incluye .NET y los motores de reproducción; no necesitas instalar el SDK de .NET.
- PowerPoint requiere una instalación compatible de Microsoft Office y su licencia. Es opcional.
- Chrome es opcional para abrir contenido externo. El navegador integrado se incluye en el paquete.
- Algunas bibliotecas nativas pueden necesitar Microsoft Visual C++ Redistributable x64. Si aparece un error sobre VCRUNTIME140 o MSVCP140, instala ese componente desde Microsoft.

## Instalar o actualizar

Si recibiste el archivo **CultoStudio-…-Windows-x64-Setup.exe**, ábrelo y sigue el asistente. Instala para tu usuario, crea un acceso en Inicio y permite añadirlo al Escritorio. Para quitar esa instalación usa **Configuración de Windows → Aplicaciones → Culto Studio → Desinstalar**. El perfil de cultos y ajustes se conserva. Consulta [el instalador EXE](instalador-exe.md) para sus componentes y cómo generarlo.

Si recibiste la distribución ZIP:

1. Copia el ZIP completo al equipo y extráelo en una carpeta.
2. Cierra Culto Studio si ya está abierto.
3. Abre **Instalar Culto Studio.cmd**. No solicita permisos de administrador ni descarga archivos.
4. Al terminar, abre **Culto Studio** desde el Escritorio o el menú Inicio.

El programa se copia en `%LOCALAPPDATA%\Programs\CultoStudio`. La actualización conserva tus datos en `%LOCALAPPDATA%\CultoStudio`. No copies la carpeta de datos de otra iglesia para instalar el programa.

Si Windows muestra una advertencia de seguridad, comprueba quién te entregó el paquete y verifica su archivo SHA-256 antes de decidir si ejecutarlo. El paquete puede no tener firma digital.

También puedes usar la versión portátil abriendo `App\CultoStudio.exe` después de extraer el ZIP completo. Sus datos siguen guardándose en el perfil local del usuario de Windows; no viajan dentro de la carpeta portátil.

## Preparar el primer culto

La **instalación completa** incluye 613 himnos en video, 11 versiones de la Biblia y 8 fondos. No necesita internet para presentar ese contenido. En la primera apertura prepara la biblioteca con las rutas de esta instalación; no contiene cultos ni ajustes de otra iglesia. Conserva la carpeta `Content` junto al programa.

1. Indica el nombre de tu iglesia en la bienvenida y configura su logo y la pantalla de salida.
2. Prepara un contenido, revisa la vista previa y luego proyéctalo.
3. Selecciona la pantalla de salida cuando conectes un proyector o televisor.
4. Prueba imagen y sonido antes del culto. Comprueba el volumen del programa y la salida de sonido de Windows.
5. Selecciona la versión de la Biblia que quieras usar. Para añadir otra, consulta las instrucciones de importación incluidas en `App\docs`.

El paquete completo lleva una selección de contenido para distribución; excluye el perfil del equipo que lo preparó: nombre de iglesia, cultos guardados, notas personales, sesiones de navegador, contraseñas y ajustes de pantalla o audio. La edición ligera conserva la importación manual de contenido. Al actualizar, se conserva el perfil existente; la carga automática del contenido se realiza solo para perfiles nuevos.

## Compartir y respaldar

Comparte el ZIP de distribución y su archivo `.sha256`, junto con los avisos de terceros incluidos. No compartas `%LOCALAPPDATA%\CultoStudio` como parte de una instalación nueva: contiene los datos personales de ese usuario.

Antes de actualizar, respalda tus datos y conserva los archivos multimedia originales. El instalador copia archivos de programa y crea accesos directos; no borra tu perfil ni elimina archivos de la instalación anterior. Para quitar el programa, cierra la aplicación, borra su carpeta de programa y sus accesos directos. El perfil de datos se conserva hasta que decidas borrarlo por separado.
