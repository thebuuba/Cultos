@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
if errorlevel 1 (
  echo.
  echo No se pudo completar la instalacion. Revisa el mensaje anterior.
  pause
  exit /b 1
)
echo.
echo Instalacion terminada. Puedes abrir Culto Studio desde el Escritorio.
pause
exit /b 0
