param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [string]$OutputDirectory = 'artifacts\releases',
    [string]$ContentDirectory,
    [switch]$Installer,
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function FullPath([string]$path) {
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $projectRoot $path }
    [IO.Path]::GetFullPath($path).TrimEnd('\')
}
$source = FullPath $SourceDirectory
$output = FullPath $OutputDirectory
if ($Installer) {
    if ($ContentDirectory) { throw 'El EXE es la edición del programa. Usa el ZIP para distribuir el contenido completo de varios GB.' }
    if (-not $CompilerPath) {
        $CompilerPath = @(
            (Join-Path $projectRoot '.tools\InnoSetup\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
    if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
        throw 'Instala Inno Setup 6 o indica su ISCC.exe con -CompilerPath.'
    }
    $CompilerPath = FullPath $CompilerPath
}
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw 'No existe la carpeta de publicacion.' }
if ($output.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or $output.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'La salida del paquete debe estar fuera de la carpeta de publicacion.'
}
$required = @('CultoStudio.exe', 'MoveHost\Culto.MoveHost.exe', 'Runtime\ObsMove\manifest.json', 'libvlc\win-x64\libvlc.dll', 'libcef.dll')
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $relative) -PathType Leaf)) { throw "Publicacion incompleta: falta $relative" }
}
$entries = @(Get-ChildItem -LiteralPath $source -Recurse -Force)
if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'La publicacion contiene enlaces; prepara una carpeta limpia.' }
$privateNames = @('workspace.json', 'biblia.json', 'browseruserdata', 'userdata', 'user data', 'captures', 'capturas', 'backups', 'backup', 'logs', 'notes', 'notas')
if ($entries | Where-Object { $_.Name.ToLowerInvariant() -in $privateNames -or $_.Name -match '^(workspace|biblia)\.json[.-]' }) {
    throw 'La publicacion contiene datos personales, copias o cache. Usa una publicacion limpia, sin el perfil de LocalAppData.'
}
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source 'CultoStudio.exe')).FileVersion
if ([string]::IsNullOrWhiteSpace($version) -or $version -notmatch '^\d+(\.\d+){1,3}$') { throw 'La version del ejecutable no es valida para el paquete.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$staging = Join-Path $output ('.staging-' + [Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $staging 'CultoStudio'
$app = Join-Path $packageRoot 'App'
New-Item -ItemType Directory -Path $app -Force | Out-Null
try {
    foreach ($entry in $entries | Where-Object { -not $_.PSIsContainer }) {
        $relative = $entry.FullName.Substring($source.Length + 1)
        if ($entry.Extension -in @('.pdb', '.log', '.bak', '.tmp', '.dmp') -or $relative -eq 'README.md' -or
            ($relative.StartsWith('docs\', [StringComparison]::OrdinalIgnoreCase) -and $relative -ne 'docs\formato-biblia.md') -or
            $relative -match '(^|\\)(\.git|\.downloads|\.tools|TestResults)(\\|$)') { continue }
        $destination = Join-Path $app $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $entry.FullName -Destination $destination
    }
    if ($ContentDirectory) {
        $content = FullPath $ContentDirectory
        if (-not (Test-Path -LiteralPath (Join-Path $content 'content-manifest.json') -PathType Leaf)) { throw 'Falta el manifiesto de contenido completo.' }
        $contentEntries = @(Get-ChildItem -LiteralPath $content -Recurse -Force)
        if ($contentEntries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint -or $_.Name.ToLowerInvariant() -in $privateNames }) {
            throw 'El contenido completo contiene enlaces o datos de perfil.'
        }
        foreach ($entry in $contentEntries | Where-Object { -not $_.PSIsContainer }) {
            $destination = Join-Path $app ('Content\' + $entry.FullName.Substring($content.Length + 1))
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $entry.FullName -Destination $destination
        }
    }
    foreach ($name in @('Install.ps1', 'Instalar Culto Studio.cmd')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot ('packaging\' + $name)) -Destination $packageRoot
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\PRIMEROS-PASOS.md') -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\PRIMEROS-PASOS.md') -Destination (Join-Path $app 'README.md')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\instalador-exe.md') -Destination $app
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\actualizaciones.md') -Destination $app
    foreach ($name in @('THIRD-PARTY-NOTICES.md', 'LICENSE', 'LICENSE.md', 'LICENSE.txt')) {
        $notice = Join-Path $projectRoot $name
        if (Test-Path -LiteralPath $notice -PathType Leaf) { Copy-Item -LiteralPath $notice -Destination $packageRoot }
    }
    $runtime = Get-Content -LiteralPath (Join-Path $app 'Runtime\ObsMove\manifest.json') -Raw | ConvertFrom-Json
    $hashes = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = $_.FullName.Substring($packageRoot.Length + 1).Replace('\', '/'); Bytes = $_.Length; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    [ordered]@{
        SchemaVersion = 1; Application = 'Culto Studio'; Version = $version; Architecture = 'win-x64'
        CreatedUtc = [DateTime]::UtcNow.ToString('o'); IncludesUserProfile = $false
        IncludesBundledContent = [bool]$ContentDirectory
        RuntimeComponents = @($runtime.Assets | Select-Object Component, Version, Source, Sha256)
        Files = $hashes
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packageRoot 'package-manifest.json') -Encoding UTF8
    $edition = if ($ContentDirectory) { '-Completo' } else { '' }
    if ($Installer) {
        $package = Join-Path $output ("CultoStudio-$version-Windows-x64-Setup.exe")
        if (Test-Path -LiteralPath $package) { throw "Ya existe $package. Usa otra carpeta de salida para conservar el paquete anterior." }
        & $CompilerPath "/DPackageDirectory=$packageRoot" "/DAppVersion=$version" "/DInstallerOutput=$output" (Join-Path $projectRoot 'packaging\CultoStudio.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador EXE.' }
    }
    else {
        $package = Join-Path $output ("CultoStudio-$version-Windows-x64$edition.zip")
        if (Test-Path -LiteralPath $package) { throw "Ya existe $package. Usa otra carpeta de salida para conservar el paquete anterior." }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::CreateFromDirectory($staging, $package, [IO.Compression.CompressionLevel]::Fastest, $false)
    }
    $digest = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
    "$digest  $([IO.Path]::GetFileName($package))" | Set-Content -LiteralPath ($package + '.sha256') -Encoding ASCII
    if ($Installer) {
        $channel = Get-Content -LiteralPath (Join-Path $projectRoot 'packaging\update-channel.json') -Raw | ConvertFrom-Json
        $updateManifest = [ordered]@{
            version = $version
            installerUrl = "https://github.com/$($channel.repository)/releases/download/v$version/$([IO.Path]::GetFileName($package))"
            sha256 = $digest
            size = (Get-Item -LiteralPath $package).Length
            notes = 'Mejoras de Culto Studio. Consulta las novedades en GitHub Releases.'
        } | ConvertTo-Json
        [IO.File]::WriteAllText((Join-Path $output 'update.json'), $updateManifest, [Text.UTF8Encoding]::new($false))
    }
    Write-Output "Paquete: $package"
    Write-Output "SHA-256: $digest"
}
finally {
    # Only this invocation's GUID directory is removed, never source or user data.
    $resolvedStaging = [IO.Path]::GetFullPath($staging)
    if ($resolvedStaging.StartsWith($output + '\.staging-', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedStaging) -match '^\.staging-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
