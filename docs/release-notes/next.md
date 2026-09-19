# FileTools Next Release Notes Draft

This draft tracks development version v1.5.0.0, following v1.4.7.0. The previous test build is documented in
[v1.4.7.0 release notes](v1.4.7.0.md).

## Highlights

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

Implementation is on `codex/1.5.0.0`. Phases 1 and 2 are implemented locally;
phases 3 through 5 remain planned. None of these changes has shipped yet.

1. Empty-folder cleanup — implemented, Recycle Bin only.
2. Batch filename editing — implemented.
3. Duplicate-file inspection improvements, including whole-file verification.
4. CSV/TXT file-list export.
5. Conditional file collection by kind, name, modified/created date, and related metadata.

See the [implementation plan](../v1.5.0.0-implementation-plan.md) for scope and
completion criteria. Record implemented changes under Highlights as each phase
passes validation. Runtime/package version sources are synchronized to `1.5.0.0`
for the requested commit and push. See [v1.5.0.0 unreleased notes](v1.5.0.0.md).

## Support Scope

The current release draft includes app-level empty-folder recycling and batch
filename editing. Dedicated Explorer commands and the remaining three planned
features are outside this first implementation. Windows installation and live
Explorer validation remain required before publishing.

## Verification Before Publishing

- Run the Release regression suite and scripts/test_unwrap_menu.ps1.
- Build the Release x64 solution and build_msi.ps1 with synchronized versions.
- Verify installation, live Explorer behavior, package signatures and checksums.
