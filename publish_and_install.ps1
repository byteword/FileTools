$ErrorActionPreference = 'Stop'

$projectDir = Join-Path $PSScriptRoot 'src\FileTools.App'
$project = Join-Path $projectDir 'FileTools.App.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK was not found. Install .NET 8 SDK first.'
}

dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

$exe = Join-Path $projectDir 'bin\Release\net8.0-windows\win-x64\publish\FileTools.exe'
if (-not (Test-Path $exe)) {
    throw "Published exe not found: $exe"
}

& $exe /install
Write-Host "Installed: $exe"
