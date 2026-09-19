# Changelog

## 1.5.1.0 - Test Build (2026-09-19)

- Fix all unwrap commands remaining visible on fast-only Explorer state callbacks.
- Share selection analysis by paths, classify on background workers with one bounded
  wait per selection, and refresh when a new menu opens.
- Add exact visibility, selection identity, timeout, and worker-lifetime regression checks.
- Increment the patch version from 1.5.0.1 to 1.5.1.0 and produce a local test installer.

## 1.5.0.1 - Unreleased (2026-09-19)

- Add CSV/TXT file-list export and conditional file collection into the target list.
- Add a content-only comparison preset and explicit whole/range/archive/metadata scope.
- Require whole-file evidence for duplicate cleanup, protect keepers, and recheck identity
  and full content before Recycle Bin execution. Export comparison scope in JSON schema 3.
- Add 39 comparison/cleanup checks; the managed Release suite passes 279 tests.
  Actual dialogs, planner handoff and temporary-file recycle/restore are verified.
- Increment app, ShellExt, installer, bundle and build/documentation version metadata
  from 1.5.0.0 to 1.5.0.1 for this checkpoint. No installation package is produced.
- Explorer menu visibility is being investigated separately after the checkpoint.
## 1.5.0.0 - Unreleased (2026-09-19)

### Added

- Empty-folder cleanup with candidate review, selected-root preservation,
  Recycle Bin handling, execution-time checks and partial-failure retry.
- Batch filename editing with literal replacement, prefix/suffix edits,
  name patterns, ordered numbering, extension preservation and conflict review.
- Planner integration, Korean/English dialogs and usage screenshots for both features.

### Changed

- Synchronize app, ShellExt, installer, bundle, resource and documentation versions
  to `1.5.0.0` for this minor-version commit.
- Preserve reviewed batch names and verify source identity/metadata before renaming;
  update completed target paths, including case-only changes.

### Verification

- Release x64 solution build passed; app and ShellExt binaries both report `1.5.0.0`.
- Release regression suite: 197 tests, including 40 tests for the new features.
- Actual local empty-folder recycle/restore and batch rename/planner integration passed.
- Installation, package publishing and live Explorer verification remain separate checks.

## 1.4.7.1 - Context menu hotfix (2026-09-19)

- Fix all Explorer subcommands remaining disabled when no slow state callback arrives.
  Fast callbacks now return usable conservative states from settings and Shell selection
  attributes without directory enumeration or waiting for the analysis mutex.
- Preserve detailed deduplication when a slow analysis is supplied. Without it, extra
  applicable commands may remain visible; invocation still validates the actual selection.
- Cover repeated fast-only calls, file selections, disabled settings and busy analysis.
  See [hotfix notes](docs/release-notes/v1.4.7.1.md).

## 1.4.7.0 - Test build (2026-09-19)

### Fixed

- Continue folder unwrapping and recursive content merges after a locked item;
  retain successful move counts and stop dependent plan steps after partial failure.
- Reject overlapping parent/child selections before moving any item and reject
  destination changes after confirmation.

### Changed

- Set app, ShellExt, installer, bundle, and documentation versions to `1.4.7.0`
  using the major.minor.patch.build convention.
- Folder merge now combines contents of multiple folders, including matching
  subfolders. Selection wrapping moves files and whole folders into one new folder.
- Default collision handling keeps both entries with a numbered name; skipping
  is selectable. Existing per-file wrapping remains a separately named action.
- Align Explorer visibility, installer fallback entries, Korean/English labels,
  and confirmation dialogs; rename move-up to “Unwrap folder — all contents”.
- Reduce equivalent native Explorer unwrap commands using selection scope, names,
  enabled settings, and collision policy. Share one bounded analysis per menu;
  fast state callbacks defer I/O and uncertain selections preserve commands.


### Verification

- Built the `1.4.7.0` test EXE/MSI/MSIX on 2026-09-19. Checked embedded versions,
  SHA-256 checksums, signer presence, and signed Burn payload extraction.
  Temporary self-signed certificates are not trusted locally; installation remains untested.
- Release tests passed 157/157 on 2026-09-19, including real Windows file locks.
- Release x64 solution build passed, including ShellExt. Korean dialogs and
  selection-dependent command states were checked; installation verification remains pending.
- Cross-checked 1,024 native menu policy combinations against 960 real command
  executions, plus native cache/early-exit/error tests. A 1,000-file folder required
  four enumeration callbacks (including dot entries), not a full listing.

## 1.4.6.4 - Unreleased

### Fixed

- Fixed folder move-up so direct child folders move upward with files, including
  same-name nested folder layouts such as `A\A\B`.
- Added an `자동교정` button beside `고급` in the simple rename confirmation;
  it shares the advanced editor's correction fallback order.

### Changed

- Updated app, installer, bundle, ShellExt, README, release guide, release
  notes, UX documentation, and Program info version metadata to `1.4.6.4`.

### Verification

- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release`
  passed 144/144 on 2026-07-20.
- `build_msi.ps1 -Configuration Release` completed for `1.4.6.4` on
  2026-07-20; local installer verification is pending.

## 1.4.6.2 - 2026-06-24

### Fixed

- Fixed the simple rename review dialog so single and multi-file filename
  correction no longer fails with a `SelectedIndex` out-of-range exception while
  the dialog is being constructed.

### Changed

- Updated app, installer, bundle, ShellExt, README, release guide, release
  notes, UX documentation, and Program info version metadata to `1.4.6.2`.
- `build_msi.ps1` now reads `FileToolsVersion` from project files when
  `-Version` is omitted and stops before building if app, installer, bundle,
  and ShellExt version metadata disagree.

### Verification

- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` passed 140/140 on
  2026-06-24.
- `MSBuild.exe FileTools.sln /p:Configuration=Debug /p:Platform=x64 /m` passed
  with 0 warnings and 0 errors on 2026-06-24 using VS 18 MSBuild.

## 1.4.6.1 - 2026-06-24

### Fixed

- Fixed advanced name editor recommendations so they are generated from tokens
  in the current original name only, rather than from every referenced source
  file name.
- Fixed the advanced name editor automatic correction button so it applies
  obfuscated Hangul/Yaminjeongeum restoration candidates before falling back to
  the stored automatic name.

### Changed

- Updated app, installer, bundle, ShellExt, build script, README, release
  guide, release notes, and Program info version metadata to `1.4.6.1`.

### Verification

- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` passed 139/139 on
  2026-06-24.
- `MSBuild.exe FileTools.sln /p:Configuration=Debug /p:Platform=x64 /m` passed
  with 0 warnings and 0 errors on 2026-06-24 using VS 18 MSBuild.

## 1.4.6.0 - 2026-06-23

### Added

- Added a simple name confirmation dialog for file rename operations with
  original name, new name, Advanced, OK, and Cancel controls.
- Added a single-name advanced editor with original-name restore, automatic
  correction, and recommended text chips for file rename, folder wrap, folder
  merge, and ZIP archive merge output names.

### Changed

- The Advanced button now edits only the currently selected final name instead
  of opening a multi-item rename review surface.
- Folder wrap, folder merge, and ZIP archive merge option dialogs now expose an
  Advanced name editing button at their name confirmation points.
- Updated app, installer, bundle, ShellExt, build script, README, release
  guide, release notes, and Program info version metadata to `1.4.6.0`.

### Verification

- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` passed 137/137 on
  2026-06-23.
- `MSBuild.exe FileTools.sln /p:Configuration=Debug /p:Platform=x64 /m` passed
  with 0 warnings and 0 errors on 2026-06-23 using VS 18 MSBuild.

## 1.4.5.2 - 2026-06-22

### Fixed

- Fixed native Explorer ContextMenu visibility for mixed multi-folder
  selections so unwrap commands appear when at least one selected folder
  matches the command condition.
- Fixed ContextMenu unwrap execution so queued multi-folder selections process
  only folders that match the selected unwrap command.

### Changed

- Updated app, installer, bundle, ShellExt, build script, README, release
  guide, release notes, and Program info version metadata to `1.4.5.2`.

### Verification

- Managed tests passed 137/137 on 2026-06-22.
- Debug x64 mixed solution build passed with 0 warnings and 0 errors on
  2026-06-22 using VS 18 MSBuild.

## 1.4.5.1 - 2026-06-20

### Fixed

- Fixed the folder merge options dialog footer so localized `확인` and `취소`
  buttons keep a stable bottom-right layout under resize and DPI scaling.
- Fixed merge destination name extraction for unit-bearing numeric ranges such
  as `01권 - 20권` plus `21권 - 38권`, preventing repeated suffixes like
  `권[총 38권][완결]` from being chosen as the common folder name.

### Changed

- Common-name fallback now prefers a useful common prefix before using a
  middle common token.
- Updated app, installer, bundle, ShellExt, build script, README, release
  guide, release notes, and Program info version metadata to `1.4.5.1`.

### Verification

- Managed tests passed 136/136 on 2026-06-20.
- Release tests passed 136/136 on 2026-06-20.

## 1.4.5.0 - 2026-06-20

### Added

- Added a shared merge-name proposal builder that applies safe rename
  correction before common-stem and range analysis for merge destination names.
- Added a folder merge options item list that previews the selected files and
  folders and their merged top-level locations.

### Changed

- Reworked the folder merge options dialog to use a wider resizable layout,
  remove the low-value message box, and keep long destination paths readable.
- Folder contents-only merge mode is now shown consistently and is available
  whenever at least one selected item is a folder.
- Aligned folder merge mode labels to `폴더 단위로 병합` and `폴더 내용만 이동`.
- Updated app, installer, bundle, ShellExt, build script, README, release
  guide, release notes, and Program info version metadata to `1.4.5.0`.

### Verification

- Managed tests passed 135/135 on 2026-06-20.
- Release tests passed 135/135 on 2026-06-20.
- `MSBuild.exe FileTools.sln /p:Configuration=Release /p:Platform=x64 /m`
  passed with 0 warnings and 0 errors on 2026-06-20 using VS 18 MSBuild.

## 1.4.4.3 - 2026-06-20

### Added

- Added shared merge-name generation for folder merge, folder wrapping, and
  archive merge so final destination names can include compact numeric or text
  ranges such as `01~06`.
- Added final destination-name review for folder wrapping and archive/folder
  merge flows before execution.

### Changed

- Aligned Korean folder wrapping/unwrapping menu labels to `폴더 씌우기` and
  `폴더 벗기기` across the app, ShellExt, installer fallback menus, and docs.
- Updated app, installer, bundle, ShellExt, build script, README, release
  guide, release notes, and Program info version metadata to `1.4.4.3`.

### Verification

- Managed tests passed 134/134 on 2026-06-20.

## 1.4.4.2 - 2026-06-19

### Changed

- Updated app, installer, bundle, ShellExt, README, release guide, and Program
  info version metadata to `1.4.4.2`.
- Archive merge decision buttons now size to their localized labels when a
  pending decision is shown.

### Fixed

- Hid the archive merge decision button row while there are no pending merge
  decisions so the progress dialog no longer shows three empty disabled button
  slots.

### Verification

- Debug managed tests passed 124/124 on 2026-06-19.
- Release managed tests passed 124/124 on 2026-06-19.

## 1.4.4.1 - 2026-06-16

### Added

- Added native-sized rendered toolbar icons for the small/medium/large
  top-right task toolbar options.
- Added an embedded app-icon loader so the main window explicitly sets the
  FileTools title bar and taskbar icon at runtime.
- Added File > Exit to the standalone planner menu.

### Changed

- Updated app, installer, bundle, ShellExt, README, release guide, and Program
  info version metadata to `1.4.4.1`.
- File compare request, progress, and result dialogs now share a fixed
  right-aligned bottom button layout.

### Fixed

- Fixed the file compare progress dialog layout so the Cancel/Hide row is not
  clipped by the initial dialog height.
- Fixed completed file comparisons leaving the main progress bar spinning and
  the stop command enabled while the result dialog was open.
- Completed file comparisons now hide the progress window before showing the
  result dialog.

### Verification

- Debug managed tests passed 124/124 on 2026-06-15.
- Release managed tests passed 124/124 on 2026-06-15.

## 1.4.4.0-beta - 2026-06-15

### Added

- Added horizontal scrolling for the MainForm target and work-plan grids so
  user-resized column widths are preserved.
- Added a small/medium/large setting for the top-right task toolbar.
- Added a native ShellExt version resource and release-build signing step for
  `FileTools.ShellExt.dll`.

### Changed

- Updated app, installer, bundle, ShellExt, README, release guide, and Program
  info version metadata to `1.4.4.0`.

### Fixed

- Fixed ZIP archive merge launched from Explorer context menus so the progress
  dialog no longer fails during initial splitter layout.

### Verification

- Debug managed tests passed 116/116 on 2026-06-15.
- Release managed tests passed 116/116 on 2026-06-15.
- Full `Release|x64` solution build passed with 0 warnings and 0 errors on
  2026-06-15 after sandbox escalation for Windows SDK lookup.
- `build_msi.ps1 -Version v1.4.4.0` produced the MSI, setup bootstrapper,
  sparse MSIX identity, and CER with 0 warnings and 0 errors on 2026-06-15
  using a temporary self-signed certificate.

## 1.4.3.0-beta - 2026-06-14

### Added

- Exposed file comparison through Explorer registration, the settings command
  list, and the native Windows 11 ShellExt submenu.
- Added a context-menu command for single-file-folder unwrap using the
  folder-file naming mode.
- Added generated ZIP release-sample regression coverage for UTF-8 filenames,
  legacy CP949 filenames, legacy Shift-JIS filenames, archive and entry
  comments, directory entries, external attributes, local/central extra fields,
  collision auto-numbering, and same-content duplicate skipping.
- Added release notes for `v1.4.3.0`.

### Changed

- Updated app, installer, bundle, release, and Program info version metadata to
  `1.4.3.0`.

### Verification

- Debug managed tests passed 112/112 on 2026-06-14.
- Release managed tests passed 112/112 on 2026-06-14.
- Full `Release|x64` solution build passed with 0 warnings and 0 errors on
  2026-06-14 after sandbox escalation for Windows SDK lookup.
- `build_msi.ps1 -Version v1.4.3.0` produced the MSI, setup bootstrapper,
  sparse MSIX identity, and CER with 0 warnings and 0 errors on 2026-06-14
  using a temporary self-signed certificate.

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
