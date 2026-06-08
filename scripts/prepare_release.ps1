[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string] $Tag,

    [ValidateSet('beta', 'stable')]
    [string] $Channel = 'beta',

    [switch] $Force,

    [switch] $SkipReleaseNotes,

    [switch] $SkipWiki
)

$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Resolve-ReleaseTag {
    param([Parameter(Mandatory = $true)][string] $InputTag)

    $normalizedTag = $InputTag.Trim()
    if ($normalizedTag -notmatch '^v\d+\.\d+\.\d+\.\d+$') {
        throw "Tag '$InputTag' must use v<major>.<minor>.<build>.<revision>, for example v1.3.0.0."
    }

    $versionText = $normalizedTag.Substring(1)
    $parsed = $null
    if (-not [version]::TryParse($versionText, [ref] $parsed)) {
        throw "Tag '$InputTag' is not a valid .NET version."
    }

    if ($parsed.Major -gt 255 -or $parsed.Minor -gt 255 -or $parsed.Build -gt 65535 -or $parsed.Revision -gt 65535) {
        throw "Tag '$InputTag' exceeds Windows Installer/MSIX numeric limits."
    }

    return [pscustomobject]@{
        Tag = $normalizedTag
        Version = $versionText
    }
}

function Get-RelativePath {
    param([Parameter(Mandatory = $true)][string] $Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($repoRoot.Length).TrimStart('\', '/')
    }

    return $fullPath
}

function Read-TextFile {
    param([Parameter(Mandatory = $true)][string] $Path)

    return [IO.File]::ReadAllText($Path)
}

function Set-TextFileIfChanged {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Content
    )

    $relativePath = Get-RelativePath -Path $Path
    $oldContent = if (Test-Path $Path) { Read-TextFile -Path $Path } else { $null }
    if ($oldContent -eq $Content) {
        Write-Host "Unchanged: $relativePath"
        return
    }

    if ($WhatIfPreference) {
        Write-Host "Would update: $relativePath"
        return
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    [IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
    Write-Host "Updated: $relativePath"
}

function Get-MarkdownSection {
    param(
        [Parameter(Mandatory = $true)][string] $Content,
        [Parameter(Mandatory = $true)][string] $Heading
    )

    $escapedHeading = [regex]::Escape($Heading)
    $match = [regex]::Match($Content, "(?ms)^## $escapedHeading\s*(?<section>.*?)(?=^## |\z)")
    if (-not $match.Success) {
        throw "Could not find markdown section '$Heading'."
    }

    return $match.Groups['section'].Value.Trim()
}

function Convert-NextNotesToTagNotes {
    param(
        [Parameter(Mandatory = $true)][string] $NextNotes,
        [Parameter(Mandatory = $true)][string] $ReleaseTag,
        [Parameter(Mandatory = $true)][string] $ReleaseVersion,
        [Parameter(Mandatory = $true)][string] $ReleaseChannel
    )

    $highlights = Get-MarkdownSection -Content $NextNotes -Heading 'Highlights'
    $scope = Get-MarkdownSection -Content $NextNotes -Heading 'Support Scope'
    $scope = $scope.Replace('the current release draft includes', 'this release includes')

    $title = if ($ReleaseChannel -eq 'beta') { "# FileTools $ReleaseTag Beta" } else { "# FileTools $ReleaseTag" }
    $intro = if ($ReleaseChannel -eq 'beta') {
        "This is a beta release. It is intended for early validation before the same feature line is promoted to stable after additional real-world stabilization."
    } else {
        "This is a stable release prepared after the beta stabilization pass for the same feature line."
    }
    $scopeHeading = if ($ReleaseChannel -eq 'beta') { 'Beta Scope' } else { 'Support Scope' }

    return @"
$title

$intro

## Highlights

$highlights

## $scopeHeading

$scope

## Assets

- ``FileTools-$ReleaseVersion-win-x64-setup.exe``: normal signed installer bootstrapper.
- ``FileTools-$ReleaseVersion-win-x64.msi``: direct signed MSI package.
- ``FileTools-$ReleaseVersion-win-x64-identity.msix``: signed sparse package identity for Windows 11 native context menus.
- ``FileTools-$ReleaseVersion-msix-self-signed.cer``: public certificate used to sign the self-signed MSIX identity package.
- ``checksums.txt``: SHA256 hashes for the release assets.

## Verification Before Publishing

- Run ``dotnet test tests\FileTools.Tests\FileTools.Tests.csproj``.
- Run the Release managed test command: ``dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release --no-build``.
- Build the full mixed solution with Visual Studio MSBuild in ``Release|x64``.
- Build or dry-run ``build_msi.ps1 -Version $ReleaseTag`` before tagging to confirm the release tag is accepted and propagated into the installer build.
- Validate real ZIP samples with legacy filename encodings, comments, directory entries, external attributes, and local/central extra fields.
- Check large ZIP merge progress, cancellation, temp-file cleanup, and final move failure behavior.
- Verify release assets, checksums, signatures, and GitHub artifact attestations before publishing the draft release.
"@
}

function Update-ReleaseNotes {
    param(
        [Parameter(Mandatory = $true)] $Release,
        [Parameter(Mandatory = $true)][string] $ReleaseChannel
    )

    $nextNotesPath = Join-Path $repoRoot 'docs\release-notes\next.md'
    $targetNotesPath = Join-Path $repoRoot "docs\release-notes\$($Release.Tag).md"

    if (-not (Test-Path $nextNotesPath)) {
        throw "Release notes draft was not found: $nextNotesPath"
    }

    if ((Test-Path $targetNotesPath) -and -not $Force) {
        Write-Host "Release notes already exist: $(Get-RelativePath -Path $targetNotesPath)"
        Write-Host 'Use -Force to regenerate them from docs\release-notes\next.md.'
        return
    }

    $nextNotes = Read-TextFile -Path $nextNotesPath
    $content = Convert-NextNotesToTagNotes `
        -NextNotes $nextNotes `
        -ReleaseTag $Release.Tag `
        -ReleaseVersion $Release.Version `
        -ReleaseChannel $ReleaseChannel

    Set-TextFileIfChanged -Path $targetNotesPath -Content ($content.TrimEnd() + [Environment]::NewLine)
}

function Update-Readme {
    param(
        [Parameter(Mandatory = $true)] $Release,
        [Parameter(Mandatory = $true)][string] $DisplayVersion
    )

    $path = Join-Path $repoRoot 'README.md'
    if (-not (Test-Path $path)) {
        Write-Warning 'README.md was not found.'
        return
    }

    $text = Read-TextFile -Path $path
    $text = [regex]::Replace($text, '현재 버전: `[^`]+`\.', "현재 버전: ``$DisplayVersion``.")
    $text = [regex]::Replace($text, 'Current version: `[^`]+`\.', "Current version: ``$DisplayVersion``.")
    $text = [regex]::Replace($text, 'FileTools-\d+\.\d+\.\d+\.\d+-win-x64', "FileTools-$($Release.Version)-win-x64")

    Set-TextFileIfChanged -Path $path -Content $text
}

function Update-Wiki {
    param(
        [Parameter(Mandatory = $true)] $Release,
        [Parameter(Mandatory = $true)][string] $DisplayVersion
    )

    $wikiRoot = Join-Path $repoRoot '.wiki'
    if (-not (Test-Path $wikiRoot)) {
        Write-Warning '.wiki was not found. Skipping wiki updates.'
        return
    }

    $wikiFiles = Get-ChildItem -Path $wikiRoot -Filter '*.md' -File
    foreach ($file in $wikiFiles) {
        $text = Read-TextFile -Path $file.FullName
        $text = [regex]::Replace($text, '현재 버전: `[^`]+`\.', "현재 버전: ``$DisplayVersion``.")
        $text = [regex]::Replace($text, '현재 릴리스 버전은 `[^`]+`', "현재 릴리스 버전은 ``$DisplayVersion``")
        $text = [regex]::Replace($text, '`\d+\.\d+\.\d+\.\d+-beta`', "``$DisplayVersion``")
        $text = [regex]::Replace($text, '`\d+\.\d+\.\d+\.\d+`은 베타 릴리스입니다', "``$($Release.Version)``은 베타 릴리스입니다")
        $text = [regex]::Replace($text, '`\d+\.\d+\.\d+\.\d+`은 GitHub prerelease/beta입니다', "``$($Release.Version)``은 GitHub prerelease/beta입니다")
        $text = [regex]::Replace($text, '### \d+\.\d+\.\d+\.\d+(?:-beta)? 주요 변경', "### $DisplayVersion 주요 변경")
        $text = [regex]::Replace($text, 'FileTools-\d+\.\d+\.\d+\.\d+-win-x64', "FileTools-$($Release.Version)-win-x64")

        Set-TextFileIfChanged -Path $file.FullName -Content $text
    }
}

function Show-GitStatusSummary {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) {
        Write-Warning 'git was not found; skipping working tree summary.'
        return
    }

    $status = & git -C $repoRoot status --short
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'Could not read git status.'
        return
    }

    if ($status) {
        Write-Host ''
        Write-Host 'Working tree changes:'
        $status | ForEach-Object { Write-Host "  $_" }
    } else {
        Write-Host ''
        Write-Host 'Working tree is clean.'
    }
}

$release = Resolve-ReleaseTag -InputTag $Tag
$displayVersion = if ($Channel -eq 'beta') { "$($release.Version)-beta" } else { $release.Version }

Write-Host "Preparing FileTools release $($release.Tag) ($Channel)."
Write-Host "Display version: $displayVersion"

if (-not $SkipReleaseNotes) {
    Update-ReleaseNotes -Release $release -ReleaseChannel $Channel
}

Update-Readme -Release $release -DisplayVersion $displayVersion

if (-not $SkipWiki) {
    Update-Wiki -Release $release -DisplayVersion $displayVersion
}

Show-GitStatusSummary

Write-Host ''
Write-Host 'Next release steps:'
Write-Host "  1. Review the generated documentation changes."
Write-Host "  2. Run .\build_msi.ps1 -Version $($release.Tag) and the managed tests when ready."
Write-Host "  3. Commit the docs, push the wiki, tag $($release.Tag), then run the GitHub Release workflow."
