param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\CultoStudio'),
    [switch]$NoShortcuts,
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'
try {
    if (-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { throw 'Este paquete requiere Windows x64 (Intel o AMD).' }
    $source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'App')).TrimEnd('\')
    $target = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')
    $data = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'CultoStudio')).TrimEnd('\')
    if ($target -eq [IO.Path]::GetPathRoot($target).TrimEnd('\') -or $target.Equals($data, [StringComparison]::OrdinalIgnoreCase) -or
        $target.StartsWith($data + '\', [StringComparison]::OrdinalIgnoreCase) -or $target.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or
        $target.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Elige una carpeta de programa distinta de la carpeta de datos y del paquete.' }
    $exe = Join-Path $target 'CultoStudio.exe'
    if (Get-Process -Name CultoStudio -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) { throw 'Cierra Culto Studio antes de instalar o actualizar. No se cerrara automaticamente.' }
    if (-not (Test-Path -LiteralPath (Join-Path $source 'CultoStudio.exe') -PathType Leaf)) { throw 'Extrae el ZIP completo antes de instalar; falta la carpeta App.' }
    $files = @(Get-ChildItem -LiteralPath $source -Recurse -Force)
    if ($files | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'El paquete contiene enlaces no admitidos.' }
    if (Test-Path -LiteralPath $target) {
        if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'La carpeta de destino es un enlace; elige otra carpeta.' }
        if (Get-ChildItem -LiteralPath $target -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'La instalacion existente contiene enlaces; elige otra carpeta.' }
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    foreach ($file in $files | Where-Object { -not $_.PSIsContainer }) {
        $destination = Join-Path $target $file.FullName.Substring($source.Length + 1)
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
    # Program updates never copy, delete or modify the independent user profile.
    if (-not $NoShortcuts) {
        $shell = New-Object -ComObject WScript.Shell
        foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
            if (-not [string]::IsNullOrWhiteSpace($folder)) {
                $shortcut = $shell.CreateShortcut((Join-Path $folder 'Culto Studio.lnk'))
                $shortcut.TargetPath = $exe; $shortcut.WorkingDirectory = $target; $shortcut.IconLocation = "$exe,0"
                $shortcut.Description = 'Culto Studio'; $shortcut.Save()
            }
        }
    }
    Write-Output "Culto Studio instalado en: $target"
    Write-Output 'Abre Culto Studio desde el acceso directo o desde CultoStudio.exe.'
    Write-Output 'Tus datos existentes se conservan en LocalAppData\CultoStudio. No se abre el programa automaticamente.'
    # NoLaunch is accepted for automation; installation never launches implicitly.
}
catch { Write-Error $_; exit 1 }
