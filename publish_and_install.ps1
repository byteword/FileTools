$ErrorActionPreference = 'Stop'

$projectDir = Join-Path $PSScriptRoot 'src\FileTools.App'
$project = Join-Path $projectDir 'FileTools.App.csproj'
$shellExtProject = Join-Path $PSScriptRoot 'src\FileTools.ShellExt\FileTools.ShellExt.vcxproj'

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

$msbuild = Find-MSBuild
& $msbuild $shellExtProject /p:Configuration=Release /p:Platform=x64 /m
if ($LASTEXITCODE -ne 0) {
    throw "Shell extension build failed with exit code $LASTEXITCODE."
}

dotnet publish $project -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

$exe = Join-Path $projectDir 'bin\Release\net8.0-windows\win-x64\publish\FileTools.exe'
if (-not (Test-Path $exe)) {
    throw "Published exe not found: $exe"
}

$shellExt = Join-Path $PSScriptRoot 'src\FileTools.ShellExt\x64\Release\FileTools.ShellExt.dll'
if (-not (Test-Path $shellExt)) {
    throw "Shell extension DLL not found: $shellExt"
}

Copy-Item $shellExt (Join-Path (Split-Path $exe) 'FileTools.ShellExt.dll') -Force

& $exe /install
Write-Host "Installed: $exe"
