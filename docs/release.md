# Release and Verification

FileTools releases are prepared for GitHub Releases. The free distribution path
uses a FileTools self-signed certificate, not a paid public code-signing
certificate.

The setup bootstrapper, MSI, and sparse MSIX identity package are signed. The
public CER is attached to the release so users can inspect or import the
certificate, and the setup can import it automatically when the Windows 11 native
context menu option is selected.

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
- The workflow declares `contents: write`, `id-token: write`, and
  `attestations: write` permissions.
- `FILETOOLS_SIGNING_PFX_BASE64` and `FILETOOLS_SIGNING_PASSWORD` must be set in
  GitHub Secrets.

Before running it, create and push a version tag such as `v1.1.0.0`. Then run
the `Release` workflow from GitHub Actions and provide that existing tag.

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

When the setup option is selected, `FileTools.IdentityHelper.exe` imports
`FileTools.Identity.cer` into the current user's Trusted People store and runs:

```powershell
Add-AppxPackage -Path <identity.msix> -ExternalLocation <FileTools install folder>
```

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
Get-FileHash .\FileTools-1.1.0.0-win-x64-setup.exe -Algorithm SHA256
```

Compare the result with `checksums.txt`.

Users with GitHub CLI can also verify artifact attestations:

```powershell
gh attestation verify .\FileTools-1.1.0.0-win-x64-setup.exe -R byteword/FileTools
gh attestation verify .\FileTools-1.1.0.0-win-x64.msi -R byteword/FileTools
gh attestation verify .\FileTools-1.1.0.0-win-x64-identity.msix -R byteword/FileTools
gh attestation verify .\FileTools-1.1.0.0-msix-self-signed.cer -R byteword/FileTools
gh attestation verify .\checksums.txt -R byteword/FileTools
```

On Windows, the self-signed Authenticode/MSIX signatures can also be inspected:

```powershell
Get-AuthenticodeSignature .\FileTools-1.1.0.0-win-x64-setup.exe
Get-AuthenticodeSignature .\FileTools-1.1.0.0-win-x64.msi
Get-AuthenticodeSignature .\FileTools-1.1.0.0-win-x64-identity.msix
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
