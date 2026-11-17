param([Parameter(Mandatory=$true)][string]$InstallerPath)
$ErrorActionPreference = 'Stop'
$installer = [IO.Path]::GetFullPath($InstallerPath)
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Falta el instalador EXE que se va a comprobar.' }
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{3D48BFC5-6291-45BE-99F2-6CCF9AFCA731}_is1'
if (Test-Path $key) { throw 'Ya existe una instalación registrada. No se modifica en esta prueba.' }
$testRoot = Join-Path $env:TEMP ('CultoStudio-Installer-QA-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $testRoot 'App'
$group = 'CultoStudio-QA-' + [Guid]::NewGuid().ToString('N')
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) ($group + '\Culto Studio.lnk')
$workspace = Join-Path $env:LOCALAPPDATA 'CultoStudio\workspace.json'
$before = if (Test-Path -LiteralPath $workspace) { (Get-FileHash -LiteralPath $workspace).Hash } else { $null }
function RunInstaller([string]$exe, [string[]]$arguments) {
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "El instalador terminó con código $($process.ExitCode)." }
}
New-Item -ItemType Directory -Path $testRoot | Out-Null
$argsSetup = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=', ('/DIR="' + $app + '"'), ('/GROUP="' + $group + '"'), ('/LOG="' + (Join-Path $testRoot 'install.log') + '"'))
try {
    RunInstaller $installer $argsSetup
    foreach ($file in @('CultoStudio.exe','CultoStudio.dll','coreclr.dll','libcef.dll','libvlc\win-x64\libvlc.dll','MoveHost\Culto.MoveHost.exe','Runtime\ObsMove\manifest.json','StreamDeck\com.cultostudio.control.streamDeckPlugin','unins000.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $app $file))) { throw "Falta el archivo instalado: $file" }
    }
    if (-not (Test-Path $key)) { throw 'No se registró el desinstalador en Windows.' }
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcut).TargetPath -ne (Join-Path $app 'CultoStudio.exe')) { throw 'El acceso directo no apunta a la aplicación instalada.' }
    Write-Output 'PASS instalación completa, dependencias, plugin, acceso directo y registro de Windows'
    $marker = Join-Path $app 'preservar-archivo-local.txt'
    Set-Content -LiteralPath $marker -Value 'Conservar al actualizar y desinstalar.'
    RunInstaller $installer $argsSetup
    if (-not (Test-Path -LiteralPath $marker)) { throw 'La actualización borró un archivo ajeno al paquete.' }
    Write-Output 'PASS actualización en la misma carpeta sin borrar archivos ajenos'
    RunInstaller (Join-Path $app 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
    if ((Test-Path (Join-Path $app 'CultoStudio.exe')) -or (Test-Path $key) -or (Test-Path -LiteralPath $shortcut)) { throw 'La desinstalación dejó componentes registrados.' }
    if (-not (Test-Path -LiteralPath $marker)) { throw 'La desinstalación borró un archivo ajeno al paquete.' }
    $after = if (Test-Path -LiteralPath $workspace) { (Get-FileHash -LiteralPath $workspace).Hash } else { $null }
    if ($before -ne $after) { throw 'Cambió el perfil real del usuario durante la prueba.' }
    Write-Output 'PASS desinstalación, conservación de archivos ajenos y perfil del usuario intacto'
    Write-Output ('Evidencia de instalación: ' + $testRoot)
}
finally {
    # Only uninstall this test's own installation if an assertion failed.
    $uninstaller = Join-Path $app 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) { RunInstaller $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') }
}
