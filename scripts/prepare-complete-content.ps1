param(
    [Parameter(Mandatory=$true)][string]$HymnDirectory,
    [Parameter(Mandatory=$true)][string]$BibleDirectory,
    [Parameter(Mandatory=$true)][string[]]$BackgroundFiles,
    [string]$OutputDirectory = 'artifacts\CompleteContent'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if (Test-Path -LiteralPath $output) { throw 'El destino ya existe. Usa una carpeta nueva para conservar el contenido anterior.' }
$hymns = @()
foreach ($file in Get-ChildItem -LiteralPath $HymnDirectory -File | Sort-Object Name) {
    if ($file.Extension -notin @('.mkv','.mp4','.mov','.webm','.avi','.wmv','.mp3','.wav','.flac','.ogg')) { continue }
    if ($file.BaseName -notmatch '^(?:himno\s*)?(\d{1,4})(?=\s|[-_.]|$)') { throw ('Himno sin número: ' + $file.Name) }
    $number = [int]$Matches[1]
    if ($number -lt 1 -or $number -gt 9999) { throw ('Número no válido: ' + $file.Name) }
    $hymns += [PSCustomObject]@{Number=$number;Title=$file.BaseName;Path=('Himnario/' + $file.Name);Source=$file.FullName}
}
if ($hymns.Count -eq 0 -or @($hymns | Group-Object Number | Where-Object Count -gt 1).Count -gt 0) { throw 'El himnario está vacío o tiene números duplicados.' }
$bibles = @(Get-ChildItem -LiteralPath $BibleDirectory -File -Filter '*.json' | Sort-Object Name)
if ($bibles.Count -eq 0) { throw 'No hay Biblias para incluir.' }
$defaultBible = $null
foreach ($bible in $bibles) {
    $document = Get-Content -LiteralPath $bible.FullName -Raw | ConvertFrom-Json
    if (-not $document.Name -or @($document.Verses).Count -eq 0) { throw ('Biblia no válida: ' + $bible.Name) }
    if ($document.Name -match '1960') { $defaultBible = 'Biblias/' + $bible.Name }
}
$backgrounds = @($BackgroundFiles | ForEach-Object { Get-Item -LiteralPath $_ })
if (@($backgrounds | Group-Object Name | Where-Object Count -gt 1).Count -gt 0) { throw 'Hay nombres de fondos duplicados.' }
foreach ($background in $backgrounds) {
    if ($background.PSIsContainer -or $background.Extension -notin @('.jpg','.jpeg','.png','.bmp')) { throw 'Formato de fondo no válido.' }
}
foreach ($folder in @('Himnario','Biblias','Fondos')) { New-Item -ItemType Directory -Path (Join-Path $output $folder) -Force | Out-Null }
foreach ($hymn in $hymns) { Copy-Item -LiteralPath $hymn.Source -Destination (Join-Path $output $hymn.Path) }
foreach ($bible in $bibles) { Copy-Item -LiteralPath $bible.FullName -Destination (Join-Path $output ('Biblias/' + $bible.Name)) }
foreach ($background in $backgrounds) { Copy-Item -LiteralPath $background.FullName -Destination (Join-Path $output ('Fondos/' + $background.Name)) }
[ordered]@{
    SchemaVersion=1
    Hymns=@($hymns | Select-Object Number,Title,Path)
    Bibles=@($bibles | ForEach-Object { 'Biblias/' + $_.Name })
    Backgrounds=@($backgrounds | ForEach-Object { 'Fondos/' + $_.Name })
    DefaultBible=if ($defaultBible) { $defaultBible } else { 'Biblias/' + $bibles[0].Name }
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'content-manifest.json') -Encoding UTF8
[PSCustomObject]@{ContentDirectory=$output;Hymns=$hymns.Count;Bibles=$bibles.Count;Backgrounds=$backgrounds.Count}
