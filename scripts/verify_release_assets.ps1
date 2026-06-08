param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [string] $Repository = 'byteword/FileTools',

    [string] $ExpectedPublisher = 'CN=FileTools Self-Signed',

    [switch] $SkipSignature,

    [switch] $VerifyAttestations
)

$ErrorActionPreference = 'Stop'

$assetRoot = [IO.Path]::GetFullPath($Path)
$checksumsPath = Join-Path $assetRoot 'checksums.txt'

if (-not (Test-Path $assetRoot)) {
    throw "Release asset directory was not found: $assetRoot"
}

if (-not (Test-Path $checksumsPath)) {
    throw "checksums.txt was not found in $assetRoot"
}

function Get-ChecksumEntries {
    param([Parameter(Mandatory = $true)][string] $ChecksumPath)

    $entries = foreach ($line in Get-Content -Path $ChecksumPath) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $parts = $line.Trim() -split '\s+', 2
        if ($parts.Count -ne 2 -or $parts[0] -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Invalid checksum line: $line"
        }

        [pscustomobject]@{
            Hash = $parts[0].ToLowerInvariant()
            Name = $parts[1]
            Path = Join-Path $assetRoot $parts[1]
        }
    }

    return @($entries)
}

function Test-Checksums {
    param([Parameter(Mandatory = $true)] [array] $Entries)

    foreach ($entry in $Entries) {
        if (-not (Test-Path $entry.Path)) {
            throw "Missing release asset listed in checksums.txt: $($entry.Name)"
        }

        $actual = (Get-FileHash -Algorithm SHA256 -Path $entry.Path).Hash.ToLowerInvariant()
        if ($actual -ne $entry.Hash) {
            throw "Checksum mismatch for $($entry.Name). Expected $($entry.Hash), got $actual."
        }

        Write-Host "Checksum OK: $($entry.Name)"
    }
}

function Test-Signatures {
    param([Parameter(Mandatory = $true)] [array] $Entries)

    $signedEntries = $Entries | Where-Object {
        $_.Name.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase) -or
        $_.Name.EndsWith('.msi', [StringComparison]::OrdinalIgnoreCase) -or
        $_.Name.EndsWith('.msix', [StringComparison]::OrdinalIgnoreCase)
    }

    foreach ($entry in $signedEntries) {
        $signature = Get-AuthenticodeSignature -FilePath $entry.Path
        if (-not $signature.SignerCertificate) {
            throw "Signature certificate was not found for $($entry.Name). Status: $($signature.Status)"
        }

        if ($signature.SignerCertificate.Subject -ne $ExpectedPublisher) {
            throw "Unexpected signer for $($entry.Name): $($signature.SignerCertificate.Subject)"
        }

        $status = $signature.Status.ToString()
        if ($status -eq 'NotSigned' -or $status -eq 'HashMismatch' -or $status -eq 'NotSupportedFileFormat' -or $status -eq 'Incompatible') {
            throw "Invalid signature for $($entry.Name). Status: $status"
        }

        if ($status -ne 'Valid') {
            Write-Warning "Signature for $($entry.Name) is present but not fully trusted locally. Status: $status"
        } else {
            Write-Host "Signature OK: $($entry.Name)"
        }
    }
}

function Test-Attestations {
    param([Parameter(Mandatory = $true)] [array] $Entries)

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        throw 'GitHub CLI was not found. Install gh or omit -VerifyAttestations.'
    }

    $allEntries = @($Entries) + [pscustomobject]@{
        Name = 'checksums.txt'
        Path = $checksumsPath
    }

    foreach ($entry in $allEntries) {
        Write-Host "Verifying attestation: $($entry.Name)"
        & gh attestation verify $entry.Path -R $Repository
        if ($LASTEXITCODE -ne 0) {
            throw "Attestation verification failed for $($entry.Name)."
        }
    }
}

$entries = Get-ChecksumEntries -ChecksumPath $checksumsPath
if ($entries.Count -eq 0) {
    throw 'checksums.txt did not contain any release assets.'
}

Test-Checksums -Entries $entries

if (-not $SkipSignature) {
    Test-Signatures -Entries $entries
}

if ($VerifyAttestations) {
    Test-Attestations -Entries $entries
}

Write-Host ''
Write-Host "Verified $($entries.Count) release asset checksum entries in $assetRoot."
