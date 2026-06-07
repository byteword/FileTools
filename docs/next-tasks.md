# Next Tasks

Review date: 2026-06-06
Last updated: 2026-06-07 after common file merge design

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
  content-confirmed duplicate groups plus target-list handoff actions. Visible
  progress reporting, actual duplicate deletion, full result export, and optional
  Explorer context menu integration remain open.
- #7 Windows ARM64 build and installer support: keep open and deferred until there is ARM64 Windows hardware or VM validation.
- #8 7Z input archive merge support: keep open as the archive merge follow-up.
- #9 common-filename-based file merge flow: design drafted. Keep open for the
  preview dialog, rename-plan engine, collision handling, and regression tests.

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
- Added `docs/common-file-merge-design.md` and
  `docs/images/common-file-merge-flow.svg` for issue #9 design.

## Deferred Follow-Up Tracks

- #3 internet dictionary or AI-assisted rename correction: resume only after privacy, cost, network failure, opt-in, and review UX policies are defined.
- #6 file comparison: continue with visible progress reporting, a real
  duplicate-delete operation, full result export, optional Explorer context menu
  integration, and manual UI validation with large mixed file sets.
- #7 Windows ARM64 build and installer support: resume only when ARM64 Windows hardware or a VM is available for end-to-end installer and Explorer validation.
- #8 7Z input archive merge support: resume after ZIP archive merge real-sample validation and release notes are finished, then decide ZIP-only output versus 7Z output scope.
- #9 common-filename-based file merge flow: design decisions are documented;
  implementation remains pending until this feature is pulled into the active
  work queue.

## Next Priority

1. Stabilize ZIP archive merge release readiness.
   - Re-run the managed regression suite before tagging if archive merge changes again.
   - Validate with real ZIP samples that include legacy filename encodings, comments, directory entries, external attributes, and local/central extra fields.
   - Verify cancellation, temp-file cleanup, and final move failure behavior with large archives.
   - Decide whether ZIP32 limits are acceptable for the first release or whether Zip64 output support must be added before release.

2. Finalize the next release notes.
   - Copy `docs/release-notes/next.md` to `docs/release-notes/<tag>.md` after the next version tag is chosen.
   - Keep the archive merge support note explicit: ZIP input and ZIP output are supported; 7Z input is not yet supported and is tracked by #8.
   - Keep #9 out of the release notes until the common-filename-based file merge flow is actually implemented.

3. Run the release verification checklist during the release pass.
   - Follow the maintainer checklist in `docs/release.md` before publishing the draft GitHub Release.
   - Keep release notes and external wiki updates gated on verified assets, checksums, signatures, attestations, and install smoke testing.

4. Defer lower-priority feature tracks.
   - #3, #7, and #8 still carry explicit deferred status and resume conditions in GitHub.
   - #9 now has a local design draft, but should stay out of release scope until
     the preview/apply implementation and tests exist.
   - Do not pull these into the active work queue until the resume conditions in each issue are satisfied.

5. Continue issue #6 UI validation when file comparison is exercised manually.
   - Use mixed files and folders to verify pair counts, status filtering, and
     criterion details.
   - Validate hash and byte-to-byte range settings with large files before
     adding full export or destructive duplicate deletion.
