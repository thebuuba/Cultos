param([switch]$Publish, [switch]$Smoke)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location -LiteralPath $projectRoot
try {
    & $sdk build CultoStudio.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
    & $sdk test tests/Culto.Core.Tests/Culto.Core.Tests.csproj -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas del núcleo.' }
    & $sdk test tests/Culto.Remote.Tests/Culto.Remote.Tests.csproj -c Release --no-build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas del mando.' }
    if ($Smoke) {
        & $sdk run --project tests/Culto.Desktop.Smoke -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Falló el ensayo de escritorio.' }
        & $sdk run --project tests/Culto.Renderer.Smoke -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Falló el ensayo del reproductor.' }
        Get-Content -LiteralPath 'tests\Culto.Renderer.Smoke\fixtures\result.txt'
    }
    if ($Publish) {
        $destination = Join-Path $projectRoot 'artifacts\CultoStudio'
        & $sdk publish src/Culto.Desktop/Culto.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $destination --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación local.' }
        Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md'), (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $destination
        $documentation = Join-Path $destination 'docs'
        New-Item -ItemType Directory -Path $documentation -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\formato-biblia.md'), (Join-Path $projectRoot 'docs\validacion.md') -Destination $documentation
        Write-Output ('Aplicación disponible en: ' + (Join-Path $destination 'CultoStudio.exe'))
    }
}
finally { Pop-Location }
