param(
    [Parameter(Mandatory=$true)][string]$InstallerPath,
    [Parameter(Mandatory=$true)][string]$NotesFile,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$notes = (Resolve-Path -LiteralPath $NotesFile).Path
$manifestPath = Join-Path (Split-Path -Parent $installer) 'update.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$repository = (Get-Content -LiteralPath (Join-Path $root 'packaging\update-channel.json') -Raw | ConvertFrom-Json).repository
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($installer).ProductVersion.Trim()
$expectedUrl = "https://github.com/$repository/releases/download/v$($manifest.version)/$([IO.Path]::GetFileName($installer))"
if ($hash -ne $manifest.sha256 -or (Get-Item -LiteralPath $installer).Length -ne $manifest.size -or
    $manifest.installerUrl -ne $expectedUrl -or $version -ne $manifest.version) { throw 'El instalador no coincide con el manifiesto de publicación.' }
$tag = 'v' + $manifest.version
$arguments = @('release','create',$tag,'--repo',$repository,'--title',('Culto Studio ' + $manifest.version),'--notes-file',$notes)
if ($Publish) { $arguments += '--latest' } else { $arguments += '--draft' }
$arguments += @($installer, ($installer + '.sha256'), $manifestPath)
& gh @arguments
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear la release. No se sobrescriben releases existentes.' }
