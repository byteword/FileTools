$ErrorActionPreference = 'Stop'

$solution = Join-Path $PSScriptRoot 'installer\FileTools.Installer.sln'
$msi = Join-Path $PSScriptRoot 'installer\FileTools.Installer\bin\Release\FileTools.msi'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK was not found. Install .NET 8 SDK first.'
}

if (Test-Path $msi) {
    Remove-Item $msi -Force
}

dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $msi)) {
    throw "MSI not found: $msi"
}

Write-Host "MSI created: $msi"
