# FileTools Next Release Notes Draft

This draft is for the next release tag. After the tag is chosen, copy this file
to `docs/release-notes/<tag>.md` so the release workflow can use the tag-specific
notes.

## Highlights

- Added archive merge for ZIP input archives with ZIP output.
- Added archive merge collision handling for skip, replace, rename, and ask decisions.
- Added ZIP filename encoding selection for archive merge input.
- Preserved ZIP entry metadata, comments, external attributes, and local/central extra field bytes during ZIP merge.
- Added regression coverage for archive merge duplicate content, filesystem failures, temp ZIP cleanup, and metadata preservation.

## Support Scope

- ZIP input and ZIP output are supported for archive merge.
- 7Z input is not supported in this release scope. Track 7Z input archive merge in GitHub issue #8.
- Common-filename-based file merge is not part of the selected-target folder merge scope. Track that follow-up in GitHub issue #9.

## Verification Before Publishing

- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`.
- Build the full solution with Visual Studio MSBuild because `FileTools.ShellExt` requires Visual C++ targets.
- Validate real ZIP samples with legacy filename encodings, comments, directory entries, external attributes, and local/central extra fields.
- Check large ZIP merge progress, cancellation, temp-file cleanup, and final move failure behavior.
- Verify release assets, checksums, signatures, and GitHub artifact attestations before publishing the draft release.
