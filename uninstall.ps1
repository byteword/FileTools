$ErrorActionPreference = 'Stop'

$exe = Join-Path $PSScriptRoot 'src\FileTools.App\bin\Release\net8.0-windows\win-x64\publish\FileTools.exe'
if (Test-Path $exe) {
    & $exe /uninstall
} else {
    # Fallback: remove the current-user registry keys directly.
    foreach ($base in 'HKCU:\Software\Classes\*\shell','HKCU:\Software\Classes\Directory\shell') {
        foreach ($name in 'FileTools_NameCorrection','FileTools_FolderStructure','FileTools_AutoRelocation') {
            $path = Join-Path $base $name
            if (Test-Path $path) { Remove-Item $path -Recurse -Force }
        }
    }
    $legacyBase = 'HKCU:\Software\Classes\Directory\shell'
    foreach ($name in 'FolderUnwrap_SameName','FolderUnwrap_SingleFile','FolderUnwrap_MoveAll') {
        $path = Join-Path $legacyBase $name
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }
}
Write-Host 'Uninstalled.'
