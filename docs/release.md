# Release and Verification

FileTools releases are prepared for GitHub Releases. The current free distribution
path does not use a paid Windows Authenticode code-signing certificate.

This means the release assets can be verified for origin and integrity through
GitHub, but Windows may still show `Unknown Publisher` or Microsoft Defender
SmartScreen warnings.

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

Before running it, create and push a version tag such as `v1.0.1.8`. Then run
the `Release` workflow from GitHub Actions and provide that existing tag.

The workflow builds the Windows installer artifacts and uploads them to GitHub
Releases:

```text
FileTools-<version>-win-x64-setup.exe
FileTools-<version>-win-x64.msi
checksums.txt
```

The setup executable is the normal distribution target. The MSI is provided as a
direct installer package for users who do not need the bootstrapper.

By default, the workflow creates a draft GitHub Release. Publish the draft only
after checking the assets and release notes.

## Artifact Attestations

The workflow generates GitHub artifact attestations for the setup executable, the
MSI, and `checksums.txt`.

GitHub Artifact Attestations are provenance and integrity evidence. They are not
Windows Authenticode signatures and do not make Windows show FileTools as a
trusted publisher.

For GitHub Free, Pro, and Team plans, artifact attestations are available for
public repositories. FileTools uses this path while the repository remains public.

## User Verification

After downloading a release asset, verify its SHA256 hash:

```powershell
Get-FileHash .\FileTools-1.0.1.8-win-x64-setup.exe -Algorithm SHA256
```

Compare the result with `checksums.txt`.

Users with GitHub CLI can also verify the artifact attestation:

```powershell
gh attestation verify .\FileTools-1.0.1.8-win-x64-setup.exe -R byteword/FileTools
gh attestation verify .\FileTools-1.0.1.8-win-x64.msi -R byteword/FileTools
gh attestation verify .\checksums.txt -R byteword/FileTools
```

Attestation verification confirms that the artifact is linked to the
`byteword/FileTools` repository workflow. It does not prove that the program is
safe, bug-free, or trusted by Windows.

## Future Store Distribution

When FileTools is ready for Microsoft Store distribution, prefer an MSIX package
submitted through the Store. Store-submitted packages are signed by Microsoft
after certification, which is a better fit for free public distribution than a
self-signed certificate.

## References

- GitHub Artifact Attestations:
  <https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations>
- GitHub `actions/attest`:
  <https://github.com/actions/attest>
