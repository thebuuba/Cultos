@echo off
setlocal
set "CULTOTRIAL=%~dp0artifacts\CultoStudio-TemaLima\CultoStudio.exe"
if exist "%CULTOTRIAL%" (
    start "" "%CULTOTRIAL%"
    exit /b 0
)
echo No se encontro la prueba del tema lima.
pause
