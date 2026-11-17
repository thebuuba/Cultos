# Actualizaciones de Culto Studio

Desde la versión 0.43.0, Culto Studio comprueba las versiones estables de `thebuuba/Cultos` en GitHub Releases al abrir. El aviso aparece en la barra inferior, sin abrir ventanas ni cambiar la proyección. En **Configuración → Buscar actualizaciones…** se puede comprobar manualmente. **Buscar actualizaciones al abrir** permite desactivar la comprobación inicial.

La persona pulsa el aviso, descarga el instalador y elige **Instalar y cerrar Culto Studio**. Se verifica tamaño y SHA-256 antes de ofrecer la instalación. Si hay una salida abierta, debe confirmar expresamente que se detendrá. El culto se guarda por el cierre normal; un error al guardar cancela la instalación. La descarga se puede cancelar y se puede aplazar la instalación. Una descarga completa se reutiliza solo después de comprobar su integridad otra vez.

El programa no instala en segundo plano ni requiere una cuenta de GitHub del usuario. No envía su biblioteca ni sus ajustes. Sin internet, el trabajo local sigue funcionando. El canal usa HTTPS y permite instaladores de la misma release del repositorio configurado. La verificación SHA-256 detecta corrupción; no sustituye la firma digital del editor ni protege ante el compromiso de la cuenta que publica.

## Publicar una versión

1. Cambia `Version` en `src/Culto.Desktop/Culto.Desktop.csproj`. Cada publicación necesita una versión nueva.
2. Compila y publica en una carpeta limpia con `scripts/build.ps1 -Publish` y prepara el EXE con `scripts/package-release.ps1 -Installer`.
3. El empaquetador genera `update.json` junto al EXE y su `.sha256`, usando `packaging/update-channel.json`. El canal está incorporado en el programa; cambiar de repositorio requiere distribuir una versión que conozca el nuevo canal antes de retirarlo.
4. Comprueba el instalador con `tests/installer-smoke.ps1` y revisa las notas de versión.
5. Usa la CLI de GitHub autenticada para publicar. Sin `-Publish`, el script deja una release borrador que los usuarios no ven:

```powershell
./scripts/publish-update.ps1 -InstallerPath artifacts/releases/CultoStudio-0.43.0.0-Windows-x64-Setup.exe -NotesFile docs/releases/0.43.0.md
# Después de revisar, publica el borrador desde GitHub.
# Para crear y publicar directamente una versión ya verificada, añade -Publish.
```

Se suben el EXE, su checksum y `update.json`. El script no sobrescribe releases existentes. Solo las versiones estables publicadas y marcadas como última versión se ofrecen mediante `releases/latest/download/update.json`. No es suficiente hacer commit: publicar la release es lo que pone la actualización a disposición de los demás equipos.

Quien tenga el instalador 0.42.0 o una versión anterior debe instalar la 0.43.0 una vez manualmente. A partir de esa primera instalación recibirá el aviso de las siguientes versiones publicadas. Los instaladores continúan sin firma digital hasta contratar y configurar un certificado de editor.
