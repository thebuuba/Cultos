# Downloads and inspects pinned upstream files only. Never starts OBS, installers or native modules.
param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$runtimeRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/ObsMoveRuntime'))
$runtimePrefix = $runtimeRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$downloads = Join-Path $runtimeRoot '.downloads'
New-Item -ItemType Directory -Path $downloads -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

# SHA-256 digests are pinned from each official GitHub release asset's digest field.
$obs = [ordered]@{
    Component = 'OBS Studio'; Version = '33.0.1'; Name = 'OBS-Studio-33.0.1-Windows-x64.zip'
    Url = 'https://github.com/obsproject/obs-studio/releases/download/33.0.1/OBS-Studio-33.0.1-Windows-x64.zip'
    Release = 'https://github.com/obsproject/obs-studio/releases/tag/33.0.1'
    DigestSource = 'https://api.github.com/repos/obsproject/obs-studio/releases/tags/33.0.1'
    Source = 'https://github.com/obsproject/obs-studio/tree/33.0.1'
    Size = 223786669; Sha256 = '0d9436451c2d772b33428e3fdae9776f071895c335de17283176306fb72651d3'
}
$move = [ordered]@{
    Component = 'Move Transition'; Version = '3.2.1'; Name = 'move-transition-3.2.1-windows.zip'
    Url = 'https://github.com/exeldro/obs-move-transition/releases/download/3.2.1/move-transition-3.2.1-windows.zip'
    Release = 'https://github.com/exeldro/obs-move-transition/releases/tag/3.2.1'
    DigestSource = 'https://api.github.com/repos/exeldro/obs-move-transition/releases/tags/3.2.1'
    Source = 'https://github.com/exeldro/obs-move-transition/tree/3.2.1'
    Size = 1547613; Sha256 = 'cc74ddbaeefe9af8e1f3946faecf6fe3bc1154b371d427ed2d3a855c0e2e154c'
}
$moveLicense = [ordered]@{
    Component = 'Move Transition license'; Version = '3.2.1'; Name = 'move-3.2.1-LICENSE'
    Url = 'https://raw.githubusercontent.com/exeldro/obs-move-transition/3.2.1/LICENSE'
    Size = 18092; Sha256 = '8177f97513213526df2cf6184d8ff986c675afb514d4e68a404010521b880643'
}

function Get-PinnedDownload($asset) {
    $path = Join-Path $downloads $asset.Name
    if (Test-Path -LiteralPath $path) {
        if ((Get-Item -LiteralPath $path).Length -ne $asset.Size -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $asset.Sha256) {
            throw "Cached file differs from its pinned upstream hash: $path"
        }
        return $path
    }
    if ($Offline) { throw "Offline cache is missing: $path" }
    $partial = $path + '.partial'
    Write-Host "Downloading $($asset.Name) ($($asset.Size) bytes)"
    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($curl) {
        & $curl.Source --fail --location --silent --show-error --retry 3 --output $partial $asset.Url
        if ($LASTEXITCODE -ne 0) { throw "Download failed: $($asset.Url)" }
    }
    else { Invoke-WebRequest -Uri $asset.Url -OutFile $partial -UseBasicParsing }
    if ((Get-Item -LiteralPath $partial).Length -ne $asset.Size -or
        (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $asset.Sha256) {
        throw "Downloaded file differs from its pinned upstream hash: $partial"
    }
    Move-Item -LiteralPath $partial -Destination $path
    return $path
}

function Get-RuntimePath([string]$relative) {
    if ([IO.Path]::IsPathRooted($relative)) { throw "Rooted archive path rejected: $relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $runtimeRoot $relative))
    if (-not $path.StartsWith($runtimePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Archive path leaves the runtime directory: $relative"
    }
    # Never follow an existing link/junction when writing extracted files.
    for ($ancestor = $path; $ancestor.StartsWith($runtimePrefix, [StringComparison]::OrdinalIgnoreCase);
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
        if ((Test-Path -LiteralPath $ancestor) -and
            ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Reparse point inside runtime directory: $ancestor"
        }
    }
    return $path
}

$files = [Collections.Generic.List[object]]::new()
function Expand-RuntimeFiles([string]$archivePath, [string]$component) {
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if ($name.EndsWith('/')) { continue }
            $destination = $null
            if ($component -eq 'OBS Studio') {
                if ($name.StartsWith('bin/64bit/') -or $name.StartsWith('data/libobs/') -or
                    $name.StartsWith('data/obs-studio/license/')) { $destination = $name }
                # OBS 33 ships built-in modules under core/. Preserve their original DLL names.
                elseif ($name -match '^core/(image-source|obs-transitions)/data/(.+)$') {
                    $destination = 'data/obs-plugins/' + $Matches[1] + '/' + $Matches[2]
                }
                elseif ($name -match '^core/(image-source|obs-transitions)/\1\.dll$') {
                    $destination = 'obs-plugins/64bit/' + $Matches[1] + '.dll'
                }
            }
            elseif ($name.StartsWith('data/obs-plugins/move-transition/') -or
                $name -eq 'obs-plugins/64bit/move-transition.dll') { $destination = $name }
            if ($null -eq $destination) { continue }
            $path = Get-RuntimePath $destination
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($path)) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $path, $true)
            $files.Add([ordered]@{ Path = $destination; Component = $component; ArchiveEntry = $name
                Bytes = $entry.Length; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() })
        }
    }
    finally { $archive.Dispose() }
}

# Parse PE headers/exports/imports as bytes: no LoadLibrary, reflection load or native execution.
function Read-PeMetadata([string]$relative, [string[]]$requiredExports = @()) {
    $bytes = [IO.File]::ReadAllBytes((Get-RuntimePath $relative))
    function U16([int]$offset) { [BitConverter]::ToUInt16($bytes, $offset) }
    function U32([int]$offset) { [BitConverter]::ToUInt32($bytes, $offset) }
    function TextAt([int]$offset) {
        $end = $offset
        while ($end -lt $bytes.Length -and $bytes[$end] -ne 0) { $end++ }
        [Text.Encoding]::ASCII.GetString($bytes, $offset, $end - $offset)
    }
    if ((U16 0) -ne 0x5a4d) { throw "Not a PE image: $relative" }
    $pe = U32 0x3c
    if ((U32 $pe) -ne 0x4550 -or (U16 ($pe + 4)) -ne 0x8664) { throw "Not a Windows x64 PE: $relative" }
    $optional = $pe + 24
    if ((U16 $optional) -ne 0x20b) { throw "Not PE32+: $relative" }
    $sections = $optional + (U16 ($pe + 20))
    $sectionCount = U16 ($pe + 6)
    function RvaOffset([uint32]$rva) {
        for ($i = 0; $i -lt $sectionCount; $i++) {
            $section = $sections + 40 * $i
            $address = U32 ($section + 12)
            $size = [Math]::Max((U32 ($section + 8)), (U32 ($section + 16)))
            if ($rva -ge $address -and $rva -lt ($address + $size)) {
                return [int]((U32 ($section + 20)) + $rva - $address)
            }
        }
        throw "Unmapped PE address in $relative"
    }
    $exports = @()
    $exportRva = U32 ($optional + 112)
    if ($exportRva) {
        $directory = RvaOffset $exportRva
        $nameCount = U32 ($directory + 24)
        $names = RvaOffset (U32 ($directory + 32))
        $exports = @(for ($i = 0; $i -lt $nameCount; $i++) { TextAt (RvaOffset (U32 ($names + 4 * $i))) })
    }
    foreach ($required in $requiredExports) {
        if ($required -notin $exports) { throw "$relative is missing export $required" }
    }
    $imports = @()
    $importRva = U32 ($optional + 120)
    if ($importRva) {
        $descriptor = RvaOffset $importRva
        $imports = @(while ((U32 ($descriptor + 12)) -ne 0) {
            TextAt (RvaOffset (U32 ($descriptor + 12))); $descriptor += 20
        })
    }
    return [ordered]@{ Path = $relative; Machine = 'AMD64'; Format = 'PE32+'; ExportCount = $exports.Count
        RequiredExports = $requiredExports; Imports = $imports; Sha256 = (Get-FileHash -LiteralPath (Get-RuntimePath $relative) -Algorithm SHA256).Hash.ToLowerInvariant() }
}

$obsZip = Get-PinnedDownload $obs
$moveZip = Get-PinnedDownload $move
$license = Get-PinnedDownload $moveLicense
Expand-RuntimeFiles $obsZip 'OBS Studio'
Expand-RuntimeFiles $moveZip 'Move Transition'
$licensePath = Get-RuntimePath 'licenses/move-transition-3.2.1-GPL-2.0.txt'
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($licensePath)) -Force | Out-Null
Copy-Item -LiteralPath $license -Destination $licensePath -Force
$files.Add([ordered]@{ Path = 'licenses/move-transition-3.2.1-GPL-2.0.txt'; Component = 'Move Transition'; Source = $moveLicense.Url
    Bytes = $moveLicense.Size; Sha256 = $moveLicense.Sha256 })

$moduleExports = @('obs_module_load', 'obs_module_set_pointer', 'obs_module_ver')
$bindings = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\src\Culto.MoveHost\ObsNative.cs'))
$hostExports = @([regex]::Matches($bindings, 'internal static extern\s+\w+\s+(\w+)\(') | ForEach-Object { $_.Groups[1].Value })
if ($hostExports.Count -eq 0) { throw 'No native OBS bindings were found for static compatibility verification.' }
$binaries = @(
    Read-PeMetadata 'bin/64bit/obs.dll' $hostExports
    Read-PeMetadata 'bin/64bit/obs-frontend-api.dll' @('obs_frontend_get_current_scene')
    Read-PeMetadata 'obs-plugins/64bit/image-source.dll' $moduleExports
    Read-PeMetadata 'obs-plugins/64bit/obs-transitions.dll' $moduleExports
    Read-PeMetadata 'obs-plugins/64bit/move-transition.dll' $moduleExports
)
foreach ($required in @('bin/64bit/Qt6Core.dll', 'bin/64bit/Qt6Gui.dll', 'bin/64bit/Qt6Widgets.dll',
    'bin/64bit/libobs-d3d11.dll', 'data/libobs/default.effect', 'data/obs-studio/license/gplv2.txt',
    'data/obs-plugins/image-source/locale/en-US.ini', 'data/obs-plugins/obs-transitions/locale/en-US.ini',
    'data/obs-plugins/move-transition/locale/en-US.ini')) {
    if (-not (Test-Path -LiteralPath (Get-RuntimePath $required))) { throw "Required runtime file missing: $required" }
}
$manifest = [ordered]@{
    SchemaVersion = 1; PreparedUtc = [DateTime]::UtcNow.ToString('o'); Architecture = 'win-x64'
    Preparation = 'Download, SHA-256 verification, selective extraction and static PE inspection only; no binary loaded or executed.'
    Prerequisites = @('Windows x64 system DLLs/UCRT', 'Microsoft Visual C++ x64 runtime (VCRUNTIME140.dll/MSVCP140.dll); not included in the official OBS ZIP and not installed by this script')
    Compatibility = 'Native module loading and rendering are not validated by this file-only preparation.'
    Assets = @($obs, $move); AdditionalSources = @($moveLicense)
    Layout = [ordered]@{ BinaryDirectory = 'bin/64bit'; LibobsData = 'data/libobs'; ModuleDirectory = 'obs-plugins/64bit'; ModuleData = 'data/obs-plugins'
        ImageSourceModule = 'image-source.dll'; Obs33ModuleMapping = 'core/<module>/<module>.dll and core/<module>/data mapped to the conventional runtime layout' }
    Licenses = @('data/obs-studio/license/gplv2.txt', 'licenses/move-transition-3.2.1-GPL-2.0.txt')
    Binaries = $binaries; Files = @($files.ToArray())
}
$manifest | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Get-RuntimePath 'manifest.json') -Encoding UTF8
Write-Output "Prepared portable runtime: $runtimeRoot"
Write-Output "Files: $($files.Count); bytes: $(($files | ForEach-Object { $_.Bytes } | Measure-Object -Sum).Sum)"
Write-Output 'Verified AMD64 PE headers and required exports for libobs, frontend API and all three modules. No modules were loaded.'
