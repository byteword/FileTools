$ErrorActionPreference = 'Stop'

$exe = Join-Path $PSScriptRoot 'src\FileTools.App\bin\Release\net8.0-windows\win-x64\publish\FileTools.exe'
if (Test-Path $exe) {
    & $exe /uninstall
} else {
    # Fallback: remove the current-user registry keys directly.
    $contextMenuKeys = @(
        'FileTools',
        'FileTools_Open',
        'FileTools_NameCorrection',
        'FileTools_FolderStructure',
        'FileTools_AutoRelocation',
        'FileTools_01_NameCorrection',
        'FileTools_02_FolderWrapFiles',
        'FileTools_03_FolderUnwrapSameName',
        'FileTools_04_FolderUnwrapSingleFile',
        'FileTools_04a_FolderUnwrapUseFolderName',
        'FileTools_04b_FolderUnwrapKeepFileName',
        'FileTools_05_FolderMoveInnerFilesUp',
        'FileTools_06_AutoRelocationCurrentFolder',
        'FileTools_07_AutoRelocationChooseTarget',
        'FileTools_99_Open'
    )
    foreach ($base in 'HKCU:\Software\Classes\*\shell','HKCU:\Software\Classes\Directory\shell') {
        foreach ($name in $contextMenuKeys) {
            $path = Join-Path $base $name
            if (Test-Path $path) { Remove-Item $path -Recurse -Force }
        }
    }
    $legacyBase = 'HKCU:\Software\Classes\Directory\shell'
    foreach ($name in 'FolderUnwrap_SameName','FolderUnwrap_SingleFile','FolderUnwrap_MoveAll') {
        $path = Join-Path $legacyBase $name
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }

    $clsid = 'HKCU:\Software\Classes\CLSID\{716e7cc4-5941-4362-8aca-d38c62817de9}'
    if (Test-Path $clsid) { Remove-Item $clsid -Recurse -Force }
    $options = 'HKCU:\Software\FileTools\ContextMenu'
    if (Test-Path $options) { Remove-Item $options -Recurse -Force }
}
Write-Host 'Uninstalled.'
