# Changelog

## 1.4.2.0-beta - 2026-06-14

### Added

- Added Settings > Program info with the running app version and bundled MIT
  license text.
- Added release notes for `v1.4.2.0`.

### Fixed

- Expanded obfuscated Hangul rename candidates so mixed precomposed syllable and
  jamo/Latin fragments such as `혀ㄴ주ㅇ구l호rㄴ로ㄱ` are proposed as
  `현중귀환록` review candidates.

### Verification

- Debug managed tests passed 109/109 on 2026-06-14.

## 1.4.1.0-beta - 2026-06-14

### Added

- Standalone planner work-plan review area with all-plan, selected-target, and
  warning filters.
- Grouped work-plan display projection and connector-style input row rendering
  for multi-input operations.
- In-app right-click context menus for target and plan grids.
- Folder-merge options dialog with editable target folder name and merge mode
  selection.
- Local rename pattern-learning foundation with pattern discovery, render
  candidates, bounded feedback storage, statistical ranking, and settings.
- Root changelog and tag-specific release notes for `v1.4.1.0`.

### Changed

- Reorganized the standalone MainForm layout into a top target/task-log split
  and bottom work-plan execution group.
- Moved folder merge command access into the action toolbar and shared the
  options flow with context launch paths.
- Pinned release builds to .NET SDK 8.0.422 through `global.json` and the
  GitHub release workflow.
- Updated release workflow behavior so rerunning against an existing GitHub
  Release refreshes notes, title, draft state, and prerelease state.
- Updated the 1.4 prerelease line to replace the previous `v1.4.0.0` beta with
  `v1.4.1.0`.

### Fixed

- Folder-merge options dialog layout no longer overlaps the OK/Cancel button row
  with Korean localized text.
- Short Yaminjeongeum/obfuscated Hangul candidates such as `ㅇr -> 아` and
  `ㅎH -> 해` are proposed again in rename correction review.
- Completed work-plan steps are removed after successful execution, moved or
  renamed targets refresh to the new path, deleted targets are removed from the
  target list, and multiple plan rows can be removed together.
- Target selection changes now rebuild the work-plan grid only in the
  selected-target filter view.
- Remaining xUnit analyzer warning in the managed regression suite was removed.
- Release workflow artifact attestation permissions now include
  `artifact-metadata: write`.

### Verification

- Debug managed tests passed 105/105 on 2026-06-14.
- Full `Release|x64` solution build passed with 0 warnings and 0 errors on
  2026-06-14.
- `build_msi.ps1 -Version v1.4.1.0` should produce the setup EXE, MSI, sparse MSIX
  identity, and CER with 0 warnings and 0 errors on 2026-06-14.
- Local release asset checksums verified successfully. Self-signed signatures
  were present and reported expected local trust warnings until the CER is
  trusted.

## 1.3.0.0-beta - 2026-06-14

- Added ZIP input to ZIP output archive merge with collision handling, filename
  encoding selection, metadata preservation, and detailed internal entry
  preview.
- Added the first file-comparison beta workflow with modeless progress, JSON
  export, and duplicate-delete handoff.
- Added the first rename-correction plugin boundary with a review-only SymSpell
  sample provider.
- Aligned tag-driven versioning across app, MSI, setup bootstrapper, and sparse
  MSIX identity assets.
