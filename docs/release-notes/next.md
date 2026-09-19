# FileTools Next Release Notes Draft

This draft tracks development version v1.5.0.1, including the v1.4.7.1 hotfix. The previous test build is documented in
[v1.4.7.0 release notes](v1.4.7.0.md).

## Highlights

- Existing file comparison now has a content-only preset and explicit comparison scope.
  Whole-file evidence is required for cleanup; keeper/identity/content checks guard
  Recycle Bin execution. JSON export is schema 3. See [usage](../file-compare.md).
- Latest validation: managed Release build and 279 tests, including 39 new checks;
  real dialog/planner handoff and temporary-file recycle/restore passed.

- File-list export: shared recursive scanning, selectable CSV columns or TXT paths,
  sorting, UTF-8 BOM, CSV formula protection and cancellation-safe destination replacement.
- Conditional collection: kind/extension, filename modes, created/modified local-date
  ranges, size, exclusions and hidden/system options; checked matches become app targets.
  Existing targets and their plans are preserved. See [usage and screenshots](../file-catalog.md).
- Earlier phase 4/5 validation: managed Release build and 240 tests passed, with 43 new catalog/
  collection checks and actual dialog/target handoff verification.
- Includes the master hotfix for disabled Explorer subcommands: fast-only callbacks remain usable without waiting for detailed analysis.

- Empty-folder cleanup in the app: review empty branches, preserve selected roots
  by default, and send confirmed folders to the Recycle Bin. Recheck emptiness and
  linked paths before each operation; retain unsuccessful candidates for retry.
- Batch filename editing in the app: literal replacement, prefix/suffix removal
  and addition, name patterns, extension preservation, and ordered numbering.
  Review the whole batch for invalid names and collisions before adding steps.
  Execution preserves the approved names, checks file identity/metadata again,
  never overwrites destinations, and updates successful targets in the planner.
- Both dialogs have Korean/English resources and reopen from their planned steps.
  Batch edits reopen the remaining original selection together. See
  [usage and screenshots](../file-organization.md).
- Validation: 40 new tests and the complete 197-test Release suite passed;
  actual empty-folder recycling and restoration passed on the local Windows drive.

## Planned Scope for 1.5.0.0

Implementation is on `codex/1.5.0.0`. Phases 1, 2, 4 and 5 are implemented locally;
phase 3 is implemented within the approved reduced scope. None of these changes has shipped yet.

1. Empty-folder cleanup — implemented, Recycle Bin only.
2. Batch filename editing — implemented.
3. Existing file-compare improvements — implemented; further enhancements deferred.
4. CSV/TXT file-list export — implemented.
5. Conditional file collection — implemented as target-list collection.

See the [implementation plan](../v1.5.0.0-implementation-plan.md) for scope and
completion criteria. Record implemented changes under Highlights as each phase
passes validation. Runtime/package version sources are synchronized to `1.5.0.1`
for the requested checkpoint commit. See [v1.5.0.1 unreleased notes](v1.5.0.1.md).

## Support Scope

The current release draft includes app-level empty-folder recycling, batch
filename editing, file-list export, conditional target collection and existing-comparison improvements. Dedicated
Explorer commands and dedicated duplicate-management features remain outside this implementation. Windows installation and live
Explorer validation remain required before publishing.

## Verification Before Publishing

- Run the Release regression suite and scripts/test_unwrap_menu.ps1.
- Build the Release x64 solution and build_msi.ps1 with synchronized versions.
- Verify installation, live Explorer behavior, package signatures and checksums.
