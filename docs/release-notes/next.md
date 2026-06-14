# FileTools Next Release Notes Draft

This draft is for changes after `v1.4.3.0`. Add future changes here during
development, then copy or regenerate it into `docs/release-notes/<tag>.md` for
the next release tag.

## Highlights

- No post-`v1.4.3.0` changes yet.

## Support Scope

- No runtime support scope change yet. ZIP input and ZIP output remain the
  supported archive merge scope; 7Z input remains deferred under issue #8.

## Verification Before Publishing

- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`.
- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release`.
- Build `FileTools.sln` with Visual Studio MSBuild in `Release|x64`.
- Build or dry-run `build_msi.ps1 -Version <tag>` before tagging to confirm the
  release tag is accepted and propagated into the installer build.
- Verify release assets, checksums, signatures, and GitHub artifact attestations
  before publishing the draft release.

Latest local verification:

- No post-`v1.4.3.0` verification yet.
