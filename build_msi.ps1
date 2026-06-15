param(
    [string] $Configuration = 'Release',
    [string] $SigningPublisher = 'CN=FileTools Self-Signed',
    [string] $Version = '1.4.4.1'
)

$ErrorActionPreference = 'Stop'

function Resolve-FileToolsReleaseVersion {
    param([Parameter(Mandatory = $true)][string] $InputVersion)

    $normalized = $InputVersion.Trim()
    if ($normalized.StartsWith('v', [StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(1)
    }

    if ($normalized -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Version '$InputVersion' must use four numeric parts, for example 1.4.4.1 or v1.4.4.1."
    }

    $parsed = $null
    if (-not [version]::TryParse($normalized, [ref] $parsed)) {
        throw "Version '$InputVersion' is not a valid .NET version."
    }

    if ($parsed.Major -gt 255 -or $parsed.Minor -gt 255 -or $parsed.Build -gt 65535 -or $parsed.Revision -gt 65535) {
        throw "Version '$InputVersion' exceeds Windows Installer/MSIX numeric limits."
    }

    return $normalized
}

$releaseVersion = Resolve-FileToolsReleaseVersion -InputVersion $Version
$solution = Join-Path $PSScriptRoot 'installer\FileTools.Installer.sln'
$installerProject = Join-Path $PSScriptRoot 'installer\FileTools.Installer\FileTools.Installer.wixproj'
$bundleProject = Join-Path $PSScriptRoot 'installer\FileTools.Bundle\FileTools.Bundle.wixproj'
$shellExtProject = Join-Path $PSScriptRoot 'src\FileTools.ShellExt\FileTools.ShellExt.vcxproj'
$identityPackageScript = Join-Path $PSScriptRoot 'scripts\build_identity_msix.ps1'
$identityOutputDir = Join-Path $PSScriptRoot 'artifacts\identity'
$identityMsix = Join-Path $identityOutputDir 'FileTools.Identity.msix'
$identityCer = Join-Path $identityOutputDir 'FileTools.Identity.cer'
$signingOutputDir = Join-Path $PSScriptRoot 'artifacts\signing'
$bundleSigningDir = Join-Path $signingOutputDir 'bundle'
$signingPfx = Join-Path $signingOutputDir 'FileTools.Signing.pfx'
$msi = Join-Path $PSScriptRoot "installer\FileTools.Installer\bin\$Configuration\FileTools.msi"
$setup = Join-Path $PSScriptRoot "installer\FileTools.Bundle\bin\$Configuration\FileToolsSetup.exe"
$shellExt = Join-Path $PSScriptRoot "src\FileTools.ShellExt\x64\$Configuration\FileTools.ShellExt.dll"

Write-Host "Building FileTools version: $releaseVersion"

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

function Find-WixExe {
    $project = [xml](Get-Content -Raw -Path $bundleProject)
    $sdk = $project.Project.Sdk
    $version = if ($sdk -match '^WixToolset\.Sdk/(.+)$') { $matches[1] } else { '4.0.6' }
    $candidate = Join-Path $env:USERPROFILE ".nuget\packages\wixtoolset.sdk\$version\tools\net472\x64\wix.exe"
    if (Test-Path $candidate) {
        return $candidate
    }

    $candidate = Get-ChildItem -Path (Join-Path $env:USERPROFILE '.nuget\packages\wixtoolset.sdk') -Recurse -Filter wix.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\tools\\net472\\x64\\wix\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $candidate) {
        throw 'wix.exe was not found. Build the WiX project once or restore WiX Toolset SDK packages.'
    }

    return $candidate.FullName
}

function New-SigningMaterial {
    param([Parameter(Mandatory = $true)][string] $Publisher)

    New-Item -ItemType Directory -Force -Path $signingOutputDir | Out-Null
    New-Item -ItemType Directory -Force -Path $identityOutputDir | Out-Null

    $base64 = $env:FILETOOLS_SIGNING_PFX_BASE64
    $password = $env:FILETOOLS_SIGNING_PASSWORD
    if ([string]::IsNullOrWhiteSpace($base64)) {
        $base64 = $env:MSIX_SIGNING_PFX_BASE64
    }

    if ([string]::IsNullOrWhiteSpace($password)) {
        $password = $env:MSIX_SIGNING_PASSWORD
    }

    if (-not [string]::IsNullOrWhiteSpace($base64)) {
        if ([string]::IsNullOrWhiteSpace($password)) {
            throw 'A signing PFX was provided but FILETOOLS_SIGNING_PASSWORD/MSIX_SIGNING_PASSWORD is empty.'
        }

        [IO.File]::WriteAllBytes($signingPfx, [Convert]::FromBase64String($base64))
        $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($signingPfx, $password)
        if ($cert.Subject -ne $Publisher) {
            throw "Signing certificate subject '$($cert.Subject)' must match '$Publisher'."
        }

        [IO.File]::WriteAllBytes($identityCer, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        return [pscustomobject]@{
            PfxPath = $signingPfx
            Password = $password
            Publisher = $cert.Subject
            CertificatePath = $identityCer
            IsTemporary = $false
        }
    }

    $password = [Guid]::NewGuid().ToString('N')
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $Publisher `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -CertStoreLocation Cert:\CurrentUser\My `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

    Export-PfxCertificate -Cert $cert -FilePath $signingPfx -Password $securePassword | Out-Null
    [IO.File]::WriteAllBytes($identityCer, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue

    return [pscustomobject]@{
        PfxPath = $signingPfx
        Password = $password
        Publisher = $cert.Subject
        CertificatePath = $identityCer
        IsTemporary = $true
    }
}

function Invoke-CodeSigning {
    param(
        [Parameter(Mandatory = $true)][string] $FilePath,
        [Parameter(Mandatory = $true)] $SigningMaterial
    )

    $signtool = Find-WindowsSdkTool 'signtool.exe'
    $args = @('sign', '/fd', 'SHA256', '/f', $SigningMaterial.PfxPath, '/p', $SigningMaterial.Password)
    if (-not [string]::IsNullOrWhiteSpace($env:FILETOOLS_TIMESTAMP_URL)) {
        $args += @('/tr', $env:FILETOOLS_TIMESTAMP_URL, '/td', 'SHA256')
    }

    $args += $FilePath
    & $signtool @args
    if ($LASTEXITCODE -ne 0) {
        throw "Code signing failed for $FilePath with exit code $LASTEXITCODE."
    }
}

function Invoke-BurnBundleSigning {
    param(
        [Parameter(Mandatory = $true)][string] $BundlePath,
        [Parameter(Mandatory = $true)] $SigningMaterial
    )

    $wix = Find-WixExe
    Remove-Item $bundleSigningDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $bundleSigningDir | Out-Null

    $engine = Join-Path $bundleSigningDir 'FileToolsSetup.engine.exe'
    $signedBundle = Join-Path $bundleSigningDir 'FileToolsSetup.signed.exe'
    $extractDir = Join-Path $bundleSigningDir 'extract'

    & $wix burn detach $BundlePath -engine $engine
    if ($LASTEXITCODE -ne 0) {
        throw "Burn engine detach failed with exit code $LASTEXITCODE."
    }

    Invoke-CodeSigning -FilePath $engine -SigningMaterial $SigningMaterial

    & $wix burn reattach $BundlePath -engine $engine -o $signedBundle
    if ($LASTEXITCODE -ne 0) {
        throw "Burn engine reattach failed with exit code $LASTEXITCODE."
    }

    Invoke-CodeSigning -FilePath $signedBundle -SigningMaterial $SigningMaterial

    Remove-Item $BundlePath -Force
    Move-Item -Path $signedBundle -Destination $BundlePath -Force

    & $wix burn extract $BundlePath -o $extractDir
    if ($LASTEXITCODE -ne 0) {
        throw "Signed Burn bundle extraction failed with exit code $LASTEXITCODE."
    }
}

if (Test-Path $msi) {
    Remove-Item $msi -Force
}
if (Test-Path $setup) {
    Remove-Item $setup -Force
}
Remove-Item $identityOutputDir -Recurse -Force -ErrorAction SilentlyContinue

$signing = New-SigningMaterial -Publisher $SigningPublisher

$msbuild = Find-MSBuild
& $msbuild $shellExtProject /p:Configuration=$Configuration /p:Platform=x64 /p:FileToolsVersion="$releaseVersion" /m
if ($LASTEXITCODE -ne 0) {
    throw "Shell extension build failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path $shellExt)) {
    throw "Shell extension DLL not found: $shellExt"
}
Invoke-CodeSigning -FilePath $shellExt -SigningMaterial $signing

& $identityPackageScript -Version $releaseVersion -Publisher $signing.Publisher -OutputPath $identityMsix
if ($LASTEXITCODE -ne 0) {
    throw "Identity MSIX build failed with exit code $LASTEXITCODE."
}
Invoke-CodeSigning -FilePath $identityMsix -SigningMaterial $signing

dotnet build $installerProject -c $Configuration `
    /p:FileToolsVersion="$releaseVersion" `
    /p:ProductVersion="$releaseVersion" `
    /p:IdentityMsixPath="$identityMsix" `
    /p:IdentityCertificatePath="$identityCer"
if ($LASTEXITCODE -ne 0) {
    throw "MSI build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $msi)) {
    throw "MSI not found: $msi"
}
Invoke-CodeSigning -FilePath $msi -SigningMaterial $signing

dotnet build $bundleProject -c $Configuration `
    /p:FileToolsVersion="$releaseVersion" `
    /p:ProductVersion="$releaseVersion" `
    /p:SkipBuildFileToolsMsi=true
if ($LASTEXITCODE -ne 0) {
    throw "Setup bootstrapper build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $setup)) {
    throw "Setup bootstrapper not found: $setup"
}
Invoke-BurnBundleSigning -BundlePath $setup -SigningMaterial $signing

Write-Host "MSI created: $msi"
Write-Host "Setup bootstrapper created: $setup"
Write-Host "Identity MSIX created: $identityMsix"
Write-Host "Identity certificate created: $identityCer"
if ($signing.IsTemporary) {
    Write-Host "Signed with a temporary self-signed certificate. Set FILETOOLS_SIGNING_PFX_BASE64 and FILETOOLS_SIGNING_PASSWORD for reproducible release signing."
}
