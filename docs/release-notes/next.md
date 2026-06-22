# FileTools Next Release Notes Draft

This draft is for changes after `v1.4.5.2`. Add future changes here during
development, then copy or regenerate it into `docs/release-notes/<tag>.md` for
the next release tag.

## Highlights

- No unreleased changes yet.

## Support Scope

- No runtime support scope change after `v1.4.5.2` yet.

## Verification Before Publishing

- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`.
- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release`.
- Build `FileTools.sln` with Visual Studio MSBuild in `Release|x64`.
- Build or dry-run `build_msi.ps1 -Version <tag>` before tagging to confirm the
  release tag is accepted and propagated into the installer build.
- Confirm `src\FileTools.ShellExt\x64\Release\FileTools.ShellExt.dll` has the
  expected file/product version and an Authenticode signature after the
  installer build.
- Verify release assets, checksums, signatures, and GitHub artifact attestations
  before publishing the draft release.

Latest release baseline:

- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` passed 137/137 on
  2026-06-22 after the `1.4.5.2` context-menu unwrap filtering fix.
- `MSBuild.exe FileTools.sln /p:Configuration=Debug /p:Platform=x64 /m`
  passed with 0 warnings and 0 errors on 2026-06-22 using VS 18 MSBuild.
- `.\build_msi.ps1 -Version v1.4.4.0` completed on 2026-06-15. The generated
  `FileTools.ShellExt.dll` reports file/product version `1.4.4.0` and has an
  Authenticode signature from the temporary `CN=FileTools Self-Signed`
  certificate. This remains the latest local package-build baseline; rerun it
  with `v1.4.5.2` before publishing the release assets. Local trust still
  reports an untrusted-root status until the CER is trusted.
