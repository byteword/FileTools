# Next Tasks

Review date: 2026-06-06
Last updated: 2026-06-07 after common filename archive merge implementation

Scope reviewed:

- Local commits after `v1.2.0.0`, including archive merge and issue split documentation through this review.
- GitHub issues #1 through #9 in `byteword/FileTools`.
- README, `docs/name-template-and-collision-policy.md`, `docs/release.md`, and `docs/release-notes/next.md`.

## GitHub Issue Status

- #1 app-level rename common phrases: closed as completed before this review.
- #2 app-level rename correction rule management: closed as completed during this review. The rule model, built-in/user rules, review trace, and script-rule constraints are now implemented and documented.
- #3 internet dictionary or AI-assisted rename correction: keep open as a long-term research item.
- #4 selected-target folder merge: closed as completed. Common-filename-based file merge and preview scope were split to #9.
- #5 ZIP archive merge: closed as completed. 7Z input archive merge was split to #8.
- #6 compare two or more files: active. The engine/options slice is implemented,
  the dedicated compare dialog is wired, and the result dialog now includes
  content-confirmed duplicate groups, duplicate-delete step handoff, keep-mode
  selection, JSON export, and modeless progress reporting. JSON import/reload,
  manual validation, and optional Explorer context menu integration remain open.
- #7 Windows ARM64 build and installer support: keep open and deferred until there is ARM64 Windows hardware or VM validation.
- #8 7Z input archive merge support: keep open as the archive merge follow-up.
- #9 common-filename-based file merge flow: archive-first implementation slice
  is done. Common logical output names and detailed archive-entry preview are
  implemented; general file-content merge remains deferred.

## Completed Follow-Up

- Split 7Z input archive merge from #5 into #8 and closed #5 as ZIP input/ZIP output archive merge.
- Split common-filename-based file merge from #4 into #9 and closed #4 as selected-target folder merge.
- Added `docs/release-notes/next.md` as the working release note draft for the archive merge scope.
- Added explicit deferred status and resume conditions to #3, #6, #7, #8, and #9.
- Documented the maintainer release verification checklist in `docs/release.md`.
- Added automated archive merge regression coverage for write-stage cancellation
  temp cleanup, same-content duplicate skipping, and internal path collision
  auto-numbering.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07; all 31 managed tests passed.
- Added `docs/file-compare-design.md` and
  `docs/images/file-compare-settings-options.svg` for issue #6.
- Added the file compare option model and engine foundation, including folder
  expansion into pairwise file comparisons, filename/metadata/content criteria,
  content range selection, byte-to-byte prefiltering, per-run hash caching, and
  ZIP entry-order comparison.
- Added the selected-target file compare command, grouped settings controls, and
  result dialog with status filtering and per-criterion details.
- Added `docs/images/file-compare-result-dialog.svg` to document the result UI
  layout.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the UI wiring; all 36 managed tests passed.
- Reworked file compare into a dedicated execution dialog with per-run options.
- Added result action handoffs for content-confirmed duplicate candidates,
  selected pair paths, target-list transfer, and folder opening.
- Added `docs/images/file-compare-workflow-actions.svg` to document the compare
  workflow and result action hub.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the workflow/action wiring; all 38 managed tests passed.
- Added modeless file compare progress with reopen support, JSON result export,
  duplicate keep-mode selection, and duplicate-delete work-plan step handoff.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the progress/export/delete-step wiring; all 40 managed tests
  passed.
- Prepared the internal `/context FileCompare` smoke-test route without adding
  it to Explorer registration or settings.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the internal context launch wiring; all 42 managed tests
  passed.
- Expanded file comparison options with common-name thresholds, middle-part
  start/length ranges, byte/KiB/MiB unit conversion, archive entry scope, and
  same-relative-path archive entry pairing.
- Fixed the file compare result dialog splitter initialization so small initial
  layouts do not throw before the action panel is measured.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the option expansion and splitter fix; all 48 managed tests
  passed.
- Added `docs/common-file-merge-design.md` and
  `docs/images/common-file-merge-flow.svg` for the archive-first issue #9
  design, centered on `A 01.zip + A 02.zip -> A.zip`.
- Implemented the archive-first #9 slice: numbered archive families now produce
  common logical output names, and the archive merge options dialog previews
  internal entry target names including collision auto-numbering.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the #9 slice; all 57 managed tests passed.

## Deferred Follow-Up Tracks

- #3 internet dictionary or AI-assisted rename correction: resume only after privacy, cost, network failure, opt-in, and review UX policies are defined.
- #6 file comparison: continue with JSON result import/reload, manual UI
  validation with large mixed file sets and narrow result dialog sizes, and
  eventual Explorer menu exposure after the internal `/context FileCompare`
  route is smoke-tested.
- #7 Windows ARM64 build and installer support: resume only when ARM64 Windows hardware or a VM is available for end-to-end installer and Explorer validation.
- #8 7Z input archive merge support: resume after ZIP archive merge real-sample validation and release notes are finished, then decide ZIP-only output versus 7Z output scope.
- #9 common-filename-based file merge flow: archive-first slice is implemented.
  General file-content merge is deferred until overlap/duplicate content policy
  is defined.

## Next Priority

1. Stabilize ZIP archive merge release readiness.
   - Re-run the managed regression suite before tagging if archive merge changes again.
   - Validate with real ZIP samples that include legacy filename encodings, comments, directory entries, external attributes, and local/central extra fields.
   - Verify cancellation, temp-file cleanup, and final move failure behavior with large archives.
   - Decide whether ZIP32 limits are acceptable for the first release or whether Zip64 output support must be added before release.

2. Finalize the next release notes.
   - Copy `docs/release-notes/next.md` to `docs/release-notes/<tag>.md` after the next version tag is chosen.
   - Keep the archive merge support note explicit: ZIP input and ZIP output are supported; 7Z input is not yet supported and is tracked by #8.
   - Keep the #9 release note scoped to archive-first common output naming and
     entry preview; general file-content merge remains deferred.

3. Run the release verification checklist during the release pass.
   - Follow the maintainer checklist in `docs/release.md` before publishing the draft GitHub Release.
   - Keep release notes and external wiki updates gated on verified assets, checksums, signatures, attestations, and install smoke testing.

4. Defer lower-priority feature tracks.
   - #3, #7, and #8 still carry explicit deferred status and resume conditions in GitHub.
   - #9 general file-content merge remains deferred; the archive-first output
     naming and preview slice is implemented.
   - Do not pull these into the active work queue until the resume conditions in each issue are satisfied.

5. Continue issue #6 UI validation when file comparison is exercised manually.
   - Use mixed files and folders to verify pair counts, status filtering, and
     criterion details.
   - Validate hash and byte-to-byte range settings with large files before
     adding JSON import/reload or exposing the prepared Explorer context command.
   - Validate the expanded option UI manually, especially common-name thresholds,
     middle-part start/length units, and archive same-relative-path pairing.
