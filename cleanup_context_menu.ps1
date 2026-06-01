[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [switch]$RemoveInstalledFiles,
    [switch]$RestartExplorer
)

$ErrorActionPreference = 'Stop'

$shellExtensionClassId = '{716e7cc4-5941-4362-8aca-d38c62817de9}'

$contextMenuKeyNames = @(
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
    'FileTools_99_Open',
    'FolderUnwrap_SameName',
    'FolderUnwrap_SingleFile',
    'FolderUnwrap_MoveAll'
)

$shellBasePaths = @(
    'Registry::HKEY_CURRENT_USER\Software\Classes\*\shell',
    'Registry::HKEY_CURRENT_USER\Software\Classes\Directory\shell',
    'Registry::HKEY_CURRENT_USER\Software\Classes\Folder\shell',
    'Registry::HKEY_CURRENT_USER\Software\Classes\Directory\Background\shell'
)

$extraRegistryPaths = @(
    "Registry::HKEY_CURRENT_USER\Software\Classes\CLSID\$shellExtensionClassId",
    'Registry::HKEY_CURRENT_USER\Software\FileTools\ContextMenu'
)

function Remove-RegistryTree {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LiteralPath
    )

    if (-not (Test-Path -LiteralPath $LiteralPath)) {
        Write-Verbose "Missing registry key: $LiteralPath"
        return
    }

    if ($PSCmdlet.ShouldProcess($LiteralPath, 'Remove registry key tree')) {
        Remove-Item -LiteralPath $LiteralPath -Recurse -Force
        Write-Host "Removed registry key: $LiteralPath"
    }
}

foreach ($basePath in $shellBasePaths) {
    foreach ($keyName in $contextMenuKeyNames) {
        Remove-RegistryTree -LiteralPath (Join-Path $basePath $keyName)
    }
}

foreach ($path in $extraRegistryPaths) {
    Remove-RegistryTree -LiteralPath $path
}

if ($RemoveInstalledFiles) {
    $appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
    if (-not [string]::IsNullOrWhiteSpace($appData)) {
        $installPath = Join-Path $appData 'FileTools'
        if (Test-Path -LiteralPath $installPath) {
            if ($PSCmdlet.ShouldProcess($installPath, 'Remove installed FileTools files and settings')) {
                Remove-Item -LiteralPath $installPath -Recurse -Force
                Write-Host "Removed installed files: $installPath"
            }
        }
    }
}

if ($RestartExplorer) {
    if ($PSCmdlet.ShouldProcess('explorer.exe', 'Restart Explorer')) {
        Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Process explorer.exe
        Write-Host 'Restarted Explorer.'
    }
}

Write-Host 'FileTools ContextMenu cleanup completed.'
