param(
    [string] $Publisher = 'CN=FileTools Self-Signed',
    [string] $PfxPath = 'artifacts\signing\FileTools.Signing.pfx',
    [string] $CerPath = 'artifacts\identity\FileTools.Identity.cer'
)

$ErrorActionPreference = 'Stop'

$pfxFullPath = Join-Path $PSScriptRoot "..\$PfxPath"
$cerFullPath = Join-Path $PSScriptRoot "..\$CerPath"
$pfxFullPath = [IO.Path]::GetFullPath($pfxFullPath)
$cerFullPath = [IO.Path]::GetFullPath($cerFullPath)

if (Test-Path $pfxFullPath) {
    throw "PFX already exists: $pfxFullPath"
}

if (Test-Path $cerFullPath) {
    throw "CER already exists: $cerFullPath"
}

$password = Read-Host 'Enter a password for the signing PFX' -AsSecureString

$cert = $null
try {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $pfxFullPath) | Out-Null
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $cerFullPath) | Out-Null

    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Publisher `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

    Export-PfxCertificate -Cert $cert -FilePath $pfxFullPath -Password $password | Out-Null
    Export-Certificate -Cert $cert -FilePath $cerFullPath | Out-Null
}
finally {
    if ($cert) {
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Created PFX: $pfxFullPath"
Write-Host "Created CER: $cerFullPath"
Write-Host ''
Write-Host 'Set GitHub release secrets with:'
Write-Host '$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("artifacts\signing\FileTools.Signing.pfx"))'
Write-Host '$base64 | gh secret set FILETOOLS_SIGNING_PFX_BASE64'
Write-Host 'gh secret set FILETOOLS_SIGNING_PASSWORD'
Write-Host ''
Write-Host 'Keep the PFX and password private. The CER is public and can be attached to releases.'
