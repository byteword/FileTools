# FileTools Next Release Notes Draft

This draft is for the next release tag. After the tag is chosen, copy this file
to `docs/release-notes/<tag>.md` so the release workflow can use the tag-specific
notes.

## Highlights

- Added archive merge for ZIP input archives with ZIP output.
- Added archive merge collision handling for skip, replace, rename, and ask decisions.
- Added ZIP filename encoding selection for archive merge input.
- Preserved ZIP entry metadata, comments, external attributes, and local/central extra field bytes during ZIP merge.
- Added regression coverage for archive merge duplicate content, internal path collisions, cancellation cleanup, filesystem failures, temp ZIP cleanup, and metadata preservation.
- Added the first file-comparison workflow for issue #6, including the compare engine, dedicated compare dialog, grouped settings UI, modeless progress, result dialog, duplicate-delete step handoff, keep-mode selection, and JSON export.
- Prepared an internal-only `/context FileCompare` launch route for Explorer smoke testing; it is not registered in the Explorer menu yet.
- Expanded file comparison options with common-name thresholds, middle-part start/length ranges, byte/KiB/MiB unit conversion, first-N archive entry scope, same-relative-path archive entry pairing, and a result-dialog splitter initialization fix.
- Clarified duplicate-delete work-plan steps with delete-candidate preview text, an edit-step button, a two-pane editor for choosing which loaded file targets are deleted or kept, result handoff that includes kept same-content files, large/old-file default keep selection, and group-scoped delete-step resynchronization after edits.
- Improved archive merge common filename handling so numbered archive families such as `A 01.zip` and `A 02.zip` default to `A.zip`, and added a detailed internal entry preview that shows final target paths after collision auto-numbering.

## Support Scope

- ZIP input and ZIP output are supported for archive merge.
- File comparison is under active development; the current release draft includes the first dedicated UI workflow, expanded compare options, modeless progress, JSON export, Recycle-Bin-only duplicate-delete step handoff with a two-pane delete/keep editor, and an unregistered internal context launch route. JSON result import/reload and actual Explorer menu exposure are still deferred.
- 7Z input is not supported in this release scope. Track 7Z input archive merge in GitHub issue #8.
- Archive-first common-filename merge is implemented through archive merge output naming and entry preview. General file-content merge remains deferred under GitHub issue #9.

## Verification Before Publishing

- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`. Last automated pass on 2026-06-07 passed 57 managed tests.
- Build the full solution with Visual Studio MSBuild because `FileTools.ShellExt` requires Visual C++ targets.
- Validate real ZIP samples with legacy filename encodings, comments, directory entries, external attributes, and local/central extra fields.
- Check large ZIP merge progress, cancellation, temp-file cleanup, and final move failure behavior.
- Verify release assets, checksums, signatures, and GitHub artifact attestations before publishing the draft release.
