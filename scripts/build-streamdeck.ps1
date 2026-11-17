param([string]$ApplicationDirectory = 'artifacts\CultoStudio')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginRoot = Join-Path $projectRoot 'integrations\streamdeck'
Push-Location -LiteralPath $pluginRoot
try {
    & npm.cmd ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron preparar las dependencias del plugin.' }
    & npm.cmd test
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas del plugin.' }
    & npm.cmd run pack
    if ($LASTEXITCODE -ne 0) { throw 'Falló la validación o el empaquetado de Elgato.' }
    $installer = Join-Path $pluginRoot 'dist\com.cultostudio.control.streamDeckPlugin'
    $output = Join-Path $projectRoot 'artifacts\StreamDeck'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath $installer -Destination $output -Force
    $application = [IO.Path]::GetFullPath((Join-Path $projectRoot $ApplicationDirectory))
    if (Test-Path -LiteralPath $application -PathType Container) {
        $bundled = Join-Path $application 'StreamDeck'
        New-Item -ItemType Directory -Path $bundled -Force | Out-Null
        Copy-Item -LiteralPath $installer -Destination $bundled -Force
    }
    Write-Output ('Instalador del plugin: ' + (Join-Path $output 'com.cultostudio.control.streamDeckPlugin'))
}
finally { Pop-Location }
