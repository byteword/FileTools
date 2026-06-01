$ErrorActionPreference = 'Stop'

$solution = Join-Path $PSScriptRoot 'installer\FileTools.Installer.sln'
$shellExtProject = Join-Path $PSScriptRoot 'src\FileTools.ShellExt\FileTools.ShellExt.vcxproj'
$msi = Join-Path $PSScriptRoot 'installer\FileTools.Installer\bin\Release\FileTools.msi'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK was not found. Install .NET 8 SDK first.'
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $installationPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installationPath)) {
            $candidate = Join-Path $installationPath 'MSBuild\Current\Bin\MSBuild.exe'
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'MSBuild with Visual C++ tools was not found. Install Visual Studio Build Tools with the C++ workload.'
}

if (Test-Path $msi) {
    Remove-Item $msi -Force
}

$msbuild = Find-MSBuild
& $msbuild $shellExtProject /p:Configuration=Release /p:Platform=x64 /m
if ($LASTEXITCODE -ne 0) {
    throw "Shell extension build failed with exit code $LASTEXITCODE."
}

dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $msi)) {
    throw "MSI not found: $msi"
}

Write-Host "MSI created: $msi"
