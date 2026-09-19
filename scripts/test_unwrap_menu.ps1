param([string]$MSBuildPath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $MSBuildPath) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $MSBuildPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $MSBuildPath) { throw 'Visual Studio MSBuild with the C++ workload is required.' }
Push-Location $repoRoot
try {
    & $MSBuildPath tests/FileTools.ShellExt.Tests/FileTools.ShellExt.Tests.vcxproj /p:Configuration=Release /p:Platform=x64 /m:2 /verbosity:minimal /nologo
    if ($LASTEXITCODE) { throw 'Native test build failed.' }
    $outputDirectory = Join-Path $repoRoot 'artifacts/unwrap-menu-tests'
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $matrix = Join-Path $outputDirectory 'policy-matrix.csv'
    & ./tests/FileTools.ShellExt.Tests/bin/Release/FileTools.ShellExt.Tests.exe $matrix
    if ($LASTEXITCODE) { throw 'Native menu tests failed.' }
    dotnet run --project tests/FileTools.MenuContractTests -c Release -- $matrix
    if ($LASTEXITCODE) { throw 'Native/managed execution contract tests failed.' }
} finally { Pop-Location }
