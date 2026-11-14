@echo off
setlocal
set "CULTOPROGRAM=%~dp0artifacts\CultoStudio\CultoStudio.exe"
if exist "%CULTOPROGRAM%" (
    start "" "%CULTOPROGRAM%"
    exit /b 0
)
echo No se encontro la distribucion del programa.
echo Ejecuta scripts\build.ps1 -Publish para crearla.
pause
