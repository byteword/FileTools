param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $Publisher,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [string] $MakeAppxPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$template = Join-Path $repoRoot 'installer\FileTools.Identity\AppxManifest.xml.in'
$staging = Join-Path $repoRoot 'artifacts\identity\staging'
$assets = Join-Path $staging 'Assets'

function Find-WindowsSdkTool {
    param([Parameter(Mandatory = $true)][string] $ToolName)

    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $candidates = Get-ChildItem -Path $sdkRoot -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        ForEach-Object {
            $versionText = Split-Path (Split-Path $_.DirectoryName -Parent) -Leaf
            $version = $null
            [pscustomobject]@{
                Tool = $_
                Version = if ([version]::TryParse($versionText, [ref] $version)) { $version } else { [version] '0.0' }
            }
        }

    $candidate = $candidates |
        Sort-Object Version, { $_.Tool.FullName } -Descending |
        Select-Object -First 1

    if (-not $candidate) {
        throw "$ToolName was not found under $sdkRoot."
    }

    return $candidate.Tool.FullName
}

if ([string]::IsNullOrWhiteSpace($MakeAppxPath)) {
    $MakeAppxPath = Find-WindowsSdkTool 'makeappx.exe'
}

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $assets | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null

$manifest = Get-Content -Raw -Path $template
$manifest = $manifest.Replace('$(Version)', $Version).Replace('$(Publisher)', $Publisher)
Set-Content -Path (Join-Path $staging 'AppxManifest.xml') -Value $manifest -Encoding utf8

$iconSource = Join-Path $repoRoot 'src\FileTools.App\Resources\FileToolsIcon.png'
Copy-Item $iconSource (Join-Path $assets 'StoreLogo.png') -Force
Copy-Item $iconSource (Join-Path $assets 'Square44x44Logo.png') -Force
Copy-Item $iconSource (Join-Path $assets 'Square150x150Logo.png') -Force

Remove-Item $OutputPath -Force -ErrorAction SilentlyContinue
& $MakeAppxPath pack /d $staging /p $OutputPath /o /nv
if ($LASTEXITCODE -ne 0) {
    throw "makeappx failed with exit code $LASTEXITCODE."
}

Write-Host "Identity MSIX created: $OutputPath"
