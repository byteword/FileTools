# Release and Verification

FileTools releases are prepared for GitHub Releases. The free distribution path
uses a FileTools self-signed certificate, not a paid public code-signing
certificate.

The setup bootstrapper, MSI, and sparse MSIX identity package are signed. The
public CER is attached to the release so users can inspect or import the
certificate. Setup installs the Windows 11 native context menu support files but
does not import the certificate or register the sparse package automatically;
FileTools settings exposes that as an explicit manual action.

The Burn setup bootstrapper must be signed with the WiX Burn engine
detach/sign/reattach/sign sequence. Signing the bundle EXE directly can corrupt
the attached container and make setup prompt for missing payload files during
installation.

This does not make FileTools a publicly trusted Windows publisher. Windows may
still show SmartScreen or trust warnings, especially before the project builds
reputation.

## Release Workflow

The release workflow is manual-only:

```text
.github/workflows/release.yml
```

Repository prerequisites:

- The repository must be public when using GitHub Free, Pro, or Team artifact
  attestations.
- GitHub Actions must be enabled for the repository.
- The release workflow currently uses the `windows-2025-vs2026` hosted runner
  because `FileTools.ShellExt` targets the MSVC `v145` platform toolset.
- The workflow declares `contents: write`, `id-token: write`,
  `attestations: write`, and `artifact-metadata: write` permissions.
- `FILETOOLS_SIGNING_PFX_BASE64` and `FILETOOLS_SIGNING_PASSWORD` must be set in
  GitHub Secrets.

Before running it, update repository docs, wiki docs, and the tag-specific
release notes, then create and push a four-part version tag such as
`v1.4.5.1`. Then run the `Release` workflow from GitHub Actions and provide
that existing tag. The workflow strips the leading `v` and passes that value to
`build_msi.ps1 -Version`, so the app binary, generated app manifest, MSI, Burn
bundle, and sparse MSIX identity use the same release version.

The workflow builds and uploads:

```text
FileTools-<version>-win-x64-setup.exe
FileTools-<version>-win-x64.msi
FileTools-<version>-win-x64-identity.msix
FileTools-<version>-msix-self-signed.cer
checksums.txt
```

The setup executable is the normal distribution target. The MSI is provided as a
direct installer package, and the identity MSIX/CER are included for inspection
and troubleshooting.

By default, the workflow creates a draft GitHub Release. Publish the draft only
after checking the assets and release notes.

For beta distribution, keep the workflow `prerelease` input enabled so GitHub
marks the Release as a prerelease. Switch it off only for the later stable
release after the stabilization pass.

If `docs/release-notes/<tag>.md` exists, the workflow uses that file as the
GitHub Release notes. Otherwise it falls back to generated asset notes.

During development, keep the next release draft in `docs/release-notes/next.md`.
Before tagging, copy that draft to `docs/release-notes/<tag>.md` and adjust the
title, asset names, and verification notes for the final tag.

## Maintainer Verification Checklist

Use this checklist before publishing a GitHub Release. Keep the release as a
draft until every required item below is checked.

### Before Tagging

- Confirm the working tree is clean:

```powershell
git status --short
```

- Prepare release-facing repository docs and local wiki files:

```powershell
.\scripts\prepare_release.ps1 -Tag v1.4.5.1 -Channel stable
```

Use `-WhatIf` to preview file changes, and use `-Force` only when the
tag-specific release note should be regenerated from
`docs\release-notes\next.md`. Existing tag-specific release notes are preserved
by default.

- Confirm the release tag uses a four-part version and the build script accepts
  the same value:

```powershell
.\build_msi.ps1 -Version v1.4.5.1
```

For a full release build, `build_msi.ps1` validates the version and passes it
through `FileToolsVersion`/`ProductVersion` MSBuild properties. The app project
generates its application manifest under `obj` with the same version before
compilation. The native `FileTools.ShellExt.dll` project receives the same
`FileToolsVersion`, embeds it in the DLL version resource, and is signed before
the MSI consumes it.

- Run the managed regression suite:

```powershell
dotnet test tests\FileTools.Tests\FileTools.Tests.csproj
```

- Build the full mixed solution with Visual Studio MSBuild, not `dotnet build
  FileTools.sln`. The root solution includes the native C++ ShellExt project and
  needs Visual C++ targets:

```powershell
MSBuild.exe FileTools.sln /p:Configuration=Release /p:Platform=x64
```

- Review the repository documentation and local wiki changes, then commit and
  push the wiki documentation before tagging:

```powershell
git -C .wiki status --short
git -C .wiki add .
git -C .wiki commit -m "Update wiki for FileTools 1.4.5.1 stable"
git -C .wiki push origin master
```

- Commit the repository documentation and release-note changes, then create and
  push the tag:

```powershell
git tag v1.4.5.1
git push origin master
git push origin v1.4.5.1
```

### After The Release Workflow Finishes

- Confirm the workflow produced a draft release and these assets:

```text
FileTools-<version>-win-x64-setup.exe
FileTools-<version>-win-x64.msi
FileTools-<version>-win-x64-identity.msix
FileTools-<version>-msix-self-signed.cer
checksums.txt
```

- Download the draft assets to a clean verification directory.

- Verify downloaded asset checksums, local signatures, and GitHub artifact
  attestations:

```powershell
.\scripts\verify_release_assets.ps1 -Path .\downloaded-release-assets -VerifyAttestations
```

The script verifies every `checksums.txt` entry, checks the setup EXE, MSI, and
identity MSIX signer subject, and verifies attestations only when
`-VerifyAttestations` is supplied. A self-signed certificate may still report as
locally untrusted until the public CER is trusted; the file must still be signed
by `CN=FileTools Self-Signed`.

### Install Smoke Test

Run the install smoke test on a disposable Windows account, VM, or test machine
when possible:

- Install with `FileTools-<version>-win-x64-setup.exe`.
- Launch FileTools from the Start Menu or installed shortcut.
- Open settings and confirm the Windows 11 native context menu action is
  present but not automatically executed by setup.
- Register the legacy Explorer context menu and confirm it appears for files and
  directories.
- If testing Windows 11 native context menus, import/register the sparse MSIX
  identity from FileTools settings, restart Explorer, and confirm the native
  menu entry appears.
- Uninstall FileTools and confirm shortcuts, installed files, legacy context menu
  registration, and sparse identity registration are removed or removable.

### Publish Gate

Publish the draft release only after:

- checksums, signatures, and attestations are verified;
- the setup smoke test passed or any skipped smoke-test scope is written in the
  release notes;
- release notes accurately state supported archive merge scope;
- the external wiki update has already been committed and pushed.

## Documentation And Wiki Timing

Keep repository docs current during feature work, but update and push the
external wiki during the release pass before creating the release tag. The tag
should point at repository docs that already match the wiki and tag-specific
release notes. If asset verification later changes user-facing support scope,
amend the release notes before publishing the draft release.

## Signing Secrets

Create the persistent self-signed release certificate once:

```powershell
.\scripts\create_signing_certificate.ps1
```

Then set GitHub Secrets:

```powershell
$base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("artifacts\signing\FileTools.Signing.pfx"))
$base64 | gh secret set FILETOOLS_SIGNING_PFX_BASE64
gh secret set FILETOOLS_SIGNING_PASSWORD
```

Keep the PFX and password private. The CER is public and may be attached to
releases. Local `build_msi.ps1` builds create a temporary self-signed certificate
when secrets are not present, but the GitHub release workflow refuses to run
without persistent signing secrets.

The certificate subject must stay aligned with both the app manifest and MSIX
manifest publisher:

```text
CN=FileTools Self-Signed
```

## Windows 11 Native Context Menu

The Windows 11 native context menu path uses a sparse MSIX identity package. The
identity manifest declares `desktop4:FileExplorerContextMenus` and
`windows.comServer`, while the installed WinForms executable carries matching
MSIX identity metadata in its application manifest.

Setup places `FileTools.Identity.msix` and `FileTools.Identity.cer` next to the
installed application, but it does not trust the certificate or register the
identity automatically. In FileTools settings, the Windows 11 native context
menu action imports `FileTools.Identity.cer` into the current user's Trusted
People store and calls `PackageManager.AddPackageByUriAsync` with
`AddPackageOptions.ExternalLocationUri`.

This is per-user and matches the current `%LOCALAPPDATA%\Programs\FileTools`
installation path. Restart Explorer if the native menu does not appear or clear
immediately after install/uninstall.

## Artifact Attestations

The workflow generates GitHub artifact attestations for the setup executable,
MSI, identity MSIX, CER, and `checksums.txt`.

GitHub Artifact Attestations are provenance and integrity evidence. They do not
make Windows trust the self-signed certificate.

## User Verification

After downloading a release asset, verify its SHA256 hash:

```powershell
Get-FileHash .\FileTools-1.4.5.1-win-x64-setup.exe -Algorithm SHA256
```

Compare the result with `checksums.txt`.

Users with GitHub CLI can also verify artifact attestations:

```powershell
gh attestation verify .\FileTools-1.4.5.1-win-x64-setup.exe -R byteword/FileTools
gh attestation verify .\FileTools-1.4.5.1-win-x64.msi -R byteword/FileTools
gh attestation verify .\FileTools-1.4.5.1-win-x64-identity.msix -R byteword/FileTools
gh attestation verify .\FileTools-1.4.5.1-msix-self-signed.cer -R byteword/FileTools
gh attestation verify .\checksums.txt -R byteword/FileTools
```

On Windows, the self-signed Authenticode/MSIX signatures can also be inspected:

```powershell
Get-AuthenticodeSignature .\FileTools-1.4.5.1-win-x64-setup.exe
Get-AuthenticodeSignature .\FileTools-1.4.5.1-win-x64.msi
Get-AuthenticodeSignature .\FileTools-1.4.5.1-win-x64-identity.msix
```

Before the self-signed CER is trusted, `Get-AuthenticodeSignature` may report an
untrusted root status even though the files are signed.

Attestation verification confirms that the artifact is linked to the
`byteword/FileTools` repository workflow. It does not prove that the program is
safe, bug-free, or publicly trusted by Windows.

## Future Store Distribution

The GitHub sparse identity package is not a Store submission package. When
FileTools is ready for Microsoft Store distribution, review the Store packaging
and signing path separately.

## References

- Package identity with external location:
  <https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps>
- File Explorer integration for packaged desktop apps:
  <https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer>
- MSIX package signing:
  <https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide>
- GitHub Artifact Attestations:
  <https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations>
- GitHub `actions/attest`:
  <https://github.com/actions/attest>
