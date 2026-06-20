# Next Tasks

Review date: 2026-06-06
Last updated: 2026-06-20

Scope reviewed:

- Local commits after `v1.2.0.0`, including archive merge, issue split
  documentation, 1.4 beta planner work, generated ZIP release-sample coverage,
  context-menu exposure updates, and release documentation through `v1.4.3.0`.
- GitHub issues #1 through #9 in `byteword/FileTools`.
- README, `docs/name-template-and-collision-policy.md`, `docs/release.md`, and `docs/release-notes/next.md`.
- `docs/ux-mainform-review.md` and `docs/mainform-plan-list-layout-proposal.md`.

## GitHub Issue Status

- #1 app-level rename common phrases: closed as completed before this review.
- #2 app-level rename correction rule management: closed as completed during this review. The rule model, built-in/user rules, review trace, and script-rule constraints are now implemented and documented.
- #3 internet dictionary or AI-assisted rename correction: continue as a
  plugin-based candidate-provider track. The first slice defines the plugin API,
  language setting, review-only candidate boundary, and a low-license SymSpell
  sample plugin without bundled dictionary data. The local learning track now
  starts with filename-structure pattern discovery, render-pattern candidate
  generation, confirmed-feedback persistence, statistical personalization, and a
  later shadow-validated neural ranker instead of content-inference naming.
- #4 selected-target folder merge: closed as completed. Common-filename-based file merge and preview scope were split to #9.
- #5 ZIP archive merge: closed as completed. 7Z input archive merge was split to #8.
- #6 compare two or more files: active. The engine/options slice is implemented,
  the dedicated compare dialog is wired, and the result dialog now includes
  content-confirmed duplicate groups, duplicate-delete step handoff, keep-mode
  selection, JSON export, modeless progress reporting, and Explorer/native
  ShellExt context-menu exposure. JSON import/reload and broader manual
  validation remain open.
- #7 Windows ARM64 build and installer support: keep open and deferred until there is ARM64 Windows hardware or VM validation.
- #8 7Z input archive merge support: keep open as the archive merge follow-up.
- #9 common-filename-based file merge flow: archive-first implementation slice
  is done. Common logical output names and detailed archive-entry preview are
  implemented; general file-content merge remains deferred.

## Completed Follow-Up

- Added `docs/mainform-plan-list-layout-proposal.md` and
  `docs/images/mainform-plan-list-layout-proposal.svg` on 2026-06-12 to plan
  the safer MainForm layout branch. The proposal moves task commands/log to the
  top-right pane, moves plan review to a bottom execution-order list, places
  run/stop/progress state in the same work-plan group, and defines all-plan,
  selected-target, and warning filters before implementation.
- Started the MainForm plan-list layout branch on 2026-06-12:
  - Reworked the first layout slice into a top target/task-log split and a
    bottom work-plan group.
  - Moved run/stop and lightweight progress state into the work-plan group while
    keeping the existing selected-target plan grid behavior.
  - Refreshed `docs/images/current-mainform-designer-layout.svg`, README UI
    notes, and MainForm UX tracking docs to match the new layout skeleton.
  - Added the UI-independent `WorkPlanDisplayBuilder` projection with coverage
    for execution order, selected-target filtering, shared archive-merge
    de-duplication, and warning propagation.
  - Connected the projection to the bottom plan grid with all-plan,
    selected-target, and warning filter buttons. The grid now shows order,
    action, input, and output/expected-result columns, with shared archive merge
    displayed once per plan ID plus source input rows.
- Added `docs/in-app-context-menu-folder-merge-design.md` and
  `docs/images/in-app-context-menu-folder-merge-design.svg` on 2026-06-11 to
  prepare the in-app right-click menu implementation and the safer selected-target
  folder merge pass.
- Implemented the in-app context-menu wiring for target/plan grids and the confirm-first folder-merge preview flow on 2026-06-11:
  - Added right-click-aware context menus in `src/FileTools.App/Ui/MainForm.Designer.cs` and
    `src/FileTools.App/Ui/MainForm.cs`.
  - Added shared folder-merge preview/result type in `src/FileTools.App/Operations/FolderMergeOperations.cs`.
  - Added `/context FolderMergeSelectedTargets` confirmation with user-cancel path and shared message formatting.
  - Updated `FolderMergeOperations` naming logic to remove shared numeric suffixes (`Series 01`, `Series 02` -> `Series`).
- 2026-06-11 follow-up on this thread:
  - Added in-app folder-merge options flow (`FolderMergeOptionsDialog`) supporting target folder name edit and split-button mode selection (`Merge folders` / `Move folder contents only`).
  - Moved the main action split button for merge from the target toolbar to the action toolbar.
  - Linked merge option dialog into `/context FolderMergeSelectedTargets` as well.
- 2026-06-20 follow-up on the folder merge options flow:
  - Reworked `FolderMergeOptionsDialog` into a wider resizable final-name review
    dialog with a selected item list instead of the old explanatory message box.
  - Changed folder contents-only mode so it is visible consistently and enabled
    whenever the selection includes at least one folder.
  - Added `MergeNameProposalBuilder` so safe rename correction can participate
    before common-stem and range analysis for merge destination names.
- Added regression coverage on 2026-06-11 for folder-merge naming/preview behaviors in
  `tests/FileTools.Tests/FolderAndRenameOperationTests.cs`:
  - Numeric suffix stripping for sequence naming,
  - cross-parent preview safety metadata,
  - mixed file-folder merge structure preservation.

- Added Korean readability annotations to operations-layer code first on 2026-06-11 as
  the initial phase of the "default comment-first" hardening pass:
  `src/FileTools.App/Operations/ArchiveMergeOperations.cs` and
  `src/FileTools.App/Operations/FileCompareOperations.cs` received focused
  function-level and complex-block comments where interpretation burden was high.
  Remaining non-operations files are intentionally deferred to the next phase.

- Added Korean readability comments on 2026-06-11 to additional operations modules
  (`DuplicateDeleteOperations.cs`, `DuplicateDeleteStepSelection.cs`,
  `FileCompareResultActions.cs`, `FileCompareResultExport.cs`, `FolderMergeOperations.cs`,
  `WorkPlan.cs`, `WorkPlanExecutor.cs`, `WorkPlanPreviewBuilder.cs`,
  `RenameOperations.cs`) for member fields, helper methods, and complex
  flow blocks where manual interpretation costs were highest.

- Started and completed the shell-extension-only readability pass on 2026-06-11:
  `src/FileTools.ShellExt/FileToolsShellExt.cpp` now has focused Korean
  comments for core command definitions, COM lifecycle points, selection/visibility
  checks, and command launch/registration paths where non-trivial control flow
  is present.

- Added the 2026-06-15 MainForm grid/toolbar follow-up:
  - Target and work-plan grids now keep fixed/user column widths and rely on
    horizontal scrolling for long paths.
  - Settings gained a small/medium/large top-right task toolbar size option;
    toolbar icons are rendered at the selected size rather than bitmap-scaled.
  - The main window now explicitly applies the embedded FileTools ICO for title
    bar and taskbar icon consistency.
  - File compare request/progress/result dialogs now share a stable bottom
    button layout, completed comparisons hide the progress window, and the main
    run/progress state returns to idle before the result dialog opens.
  - The File menu now includes a program exit command.
  - `FileTools.ShellExt.dll` now receives the same file/product version as the
    app, and `build_msi.ps1` signs the ShellExt DLL before MSI packaging.

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
  design, centered on `A 01.zip + A 02.zip -> A 01~02.zip`.
- Implemented the archive-first #9 slice: numbered archive families now produce
  common logical output names, and the archive merge options dialog previews
  internal entry target names including collision auto-numbering.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the #9 slice; all 57 managed tests passed.
- Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` on
  2026-06-07 after the rename-correction plugin slice; all 62 managed tests
  passed.
- Ran version-up readiness verification on 2026-06-07: Debug managed tests
  passed 62/62, Visual Studio MSBuild `Release|x64` full solution build passed
  with 0 warnings and 0 errors, and Release managed tests passed 62/62 with
  `--no-build`.
- Implemented tag-driven release version injection on 2026-06-07. The
  `build_msi.ps1 -Version v1.2.0.3` check completed with 0 warnings and 0
  errors after sandbox escalation for Windows SDK access, and the generated app
  manifest, app EXE, MSI `ProductVersion`, Burn setup EXE, and sparse MSIX
  identity all reported `1.2.0.3`. The next beta release target is
  `v1.3.0.0`, with repository docs, wiki docs, and tag-specific release notes
  updated before tagging.
- Ran the `v1.3.0.0` beta release build check on 2026-06-07:
  `build_msi.ps1 -Version v1.3.0.0` completed with 0 warnings and 0 errors,
  and the generated app manifest, app EXE, MSI `ProductVersion`, Burn setup
  EXE, and sparse MSIX identity all reported `1.3.0.0`.
- Added release preparation and verification helpers on 2026-06-08:
  `scripts/prepare_release.ps1` updates release-facing docs/wiki version
  references and creates tag-specific release notes when needed, while
  `scripts/verify_release_assets.ps1` validates downloaded asset checksums,
  local signatures, and optional GitHub artifact attestations.
- Added `docs/neural-rename-training-design.md` and the first internal
  `FileNamePatternDiscovery` slice on 2026-06-08. The current slice tokenizes
  selected filename stems, discovers structural parse-pattern candidates, scores
  batch coverage, sequential number slots, stable value slots, and simplicity,
  and keeps the feature disconnected from UI and automatic rename execution.
- Added the internal `FileNameRenderPatternGenerator` slice on 2026-06-08. It
  turns conservative bracketed-text/text/number/extension fields into render
  candidates such as `{BracketedText} - {Text} {Number:000}{Extension}`, without
  semantic content inference or UI integration.
- Added the internal feedback normalization and statistical ranker slice on
  2026-06-08. `FileNamePatternFeedbackNormalizer` defines confirmed selection
  rows, and `FileNamePatternStatisticsRanker` recency-weights prior
  parse/render selections while keeping UI and automatic rename execution
  disconnected.
- Added the internal `FileNamePatternFeedbackStore` slice on 2026-06-08. It
  stores normalized confirmed selections as JSONL at
  `%APPDATA%\FileTools\rename-pattern-feedback.jsonl`, supports append and
  overwrite operations, and skips malformed lines during load while keeping UI
  and automatic rename execution disconnected.
- Added personal pattern learning settings on 2026-06-08. `FileToolsSettings`
  now stores `RenamePatternLearningEnabled` and `RenamePatternFeedbackLimit`,
  the settings dialog exposes the toggle and row limit in the Rename group, and
  the feedback store honors the enabled state plus a minimum bounded limit of
  100 rows with a default of 2000 rows.
- Extended Korean readability comment coverage on 2026-06-11 beyond Operations to
  core non-UI layers: `FileTools.Correction.SymSpellPlugin` plugin, correction
  plugin host/catalog, environment helper, settings/stores, and naming/pattern
  modules (`FileNamePatternDiscovery`, `FileNamePatternLearning`,
  `FileNameRenderPatterns`, `NameTemplate`). The same member/function/block
  criteria were applied: member/함수 설명을 먼저 추가하고, 해석이
  난해한 루프/필터 블록은 요약 블록 주석을 추가했습니다.
- Ran the 2026-06-14 release-readiness fix pass:
  - Added `artifact-metadata: write` to the release workflow for
    `actions/attest@v4`.
  - Updated the existing-release workflow branch to refresh release notes,
    title, draft state, and prerelease state after asset replacement.
  - Added `global.json` to pin local and CI SDK selection to .NET SDK 8.0.422
    with feature-band roll-forward.
  - Removed the remaining xUnit analyzer warning in
    `tests\FileTools.Tests\WorkPlanDisplayBuilderTests.cs`.
  - Refreshed release-note verification text to the 2026-06-14 99-test pass.
  - Re-ran Debug and Release managed tests on .NET SDK 8.0.422; both passed
    99/99.
  - Re-ran `MSBuild.exe FileTools.sln /p:Configuration=Release /p:Platform=x64`;
    it passed with 0 warnings and 0 errors after sandbox escalation for Windows
    SDK lookup.
  - Re-ran `build_msi.ps1 -Version v1.3.0.0`; it produced the MSI, setup
    bootstrapper, sparse MSIX identity, and CER with 0 warnings and 0 errors
    using a temporary self-signed certificate.
- Prepared the 1.4 prerelease documentation and changelog passes on
  2026-06-14:
  - `v1.4.1.0` replaced the earlier 1.4 beta target and scoped the beta line to
    the standalone planner refresh, grouped input rows, in-app context menus,
    folder-merge options flow, local rename pattern-learning foundation, and
    release workflow hardening.
  - `v1.4.2.0` added the Program info dialog surface and fixed mixed Hangul
    syllable/jamo obfuscation candidates such as
    `혀ㄴ주ㅇ구l호rㄴ로ㄱ -> 현중귀환록`.
  - README, `CHANGELOG.md`, `docs/release.md`, tag-specific release notes, and
    `docs/release-notes/next.md` used `v1.4.2.0` as the prerelease baseline for
    that pass before being superseded by `v1.4.3.0`.
  - Debug managed tests passed 109/109 for the `v1.4.2.0` documentation/fix
    pass. Release asset verification and install smoke testing remain the
    publish gate for the `v1.4.2.0` prerelease before the `v1.4.3.0` context
    menu exposure pass superseded it.
- Added generated ZIP release-sample regression coverage on 2026-06-14:
  - The test corpus is created in the test temp folder instead of storing binary
    ZIP fixtures in the repository.
  - Coverage includes UTF-8 names, legacy CP949 names, legacy Shift-JIS names,
    archive comments, entry comments, directory entries, external attributes,
    local/central extra fields, internal path collision auto-numbering, and
    same-content duplicate skipping.
  - Ran `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`; all 110
    managed tests passed.
- Exposed the remaining implemented context-menu entry points for `v1.4.3.0` on
  2026-06-14:
  - Added Explorer/settings/native ShellExt exposure for file comparison.
  - Added the explicit folder-file-name single-file unwrap context command.
  - Updated app, installer, bundle, release, Program info, changelog, and
    tag-specific release notes to the `v1.4.3.0` beta baseline.
  - Ran Debug and Release managed tests; both passed 112/112.
  - Ran Visual Studio MSBuild `Release|x64`; it passed with 0 warnings and 0
    errors after sandbox escalation for Windows SDK lookup.
  - Ran `build_msi.ps1 -Version v1.4.3.0`; it produced the MSI, setup
    bootstrapper, sparse MSIX identity, and CER with 0 warnings and 0 errors
    using a temporary self-signed certificate.
- Published `v1.4.3.0` as a public GitHub beta/prerelease on 2026-06-14:
  - Release workflow run 27500465277 completed successfully.
  - The release is public, not draft, and keeps `Prerelease=true`.
  - Uploaded setup EXE, MSI, sparse MSIX identity, CER, and `checksums.txt`.
- Completed the `v1.4.3.0` post-release asset/signature/install smoke
  verification on 2026-06-15.
- Fixed a post-`v1.4.3.0` ZIP context-menu merge launch issue on 2026-06-15:
  - `ArchiveMergeProgressDialog` now clamps its splitter after layout instead
    of assigning a fixed splitter distance before the dialog width is known.
  - This avoids the WinForms `SplitterDistance` range exception seen when ZIP
    merge is launched from Explorer context menus.
  - Debug and Release managed tests passed 113/113.

## Deferred Follow-Up Tracks

- #3 internet dictionary or AI-assisted rename correction: resume internet or
  local-LLM providers only after privacy, cost, network failure, opt-in, and
  review UX policies are defined. The local personalization track should proceed
  through deterministic pattern discovery, render-pattern candidates, persisted
  bounded local feedback history, and review UI integration before any neural
  ranker affects candidate order.
- #6 file comparison: continue with JSON result import/reload and manual UI
  validation with large mixed file sets and narrow result dialog sizes. The
  `/context FileCompare` route is exposed through Explorer registration,
  settings, and native ShellExt; continue packaged menu smoke testing as part of
  release validation.
- #7 Windows ARM64 build and installer support: resume only when ARM64 Windows
  hardware or a VM is available for end-to-end installer and Explorer
  validation.
- #8 7Z input archive merge support: resume after ZIP archive merge real-sample
  validation and the `v1.4.3.0` release gate, then decide ZIP-only output
  versus 7Z output scope.
- #9 common-filename-based file merge flow: archive-first slice is implemented.
  General file-content merge is deferred until overlap/duplicate content policy
  is defined.

## Next Priority

1. Keep post-`v1.4.3.0` release notes current as new work starts.
   - `docs/release-notes/next.md` is the draft for changes after `v1.4.3.0`
     and now tracks the ZIP context-menu merge splitter fix.
   - For the next tag, use `scripts/prepare_release.ps1 -Tag <tag> -Channel beta`
     to update release-facing README/wiki version references and create
     `docs/release-notes/<tag>.md` when it does not already exist.
   - Use `-WhatIf` for review and `-Force` only when the tag-specific notes
     should be regenerated from `docs/release-notes/next.md`.
   - Keep the archive merge support note explicit: ZIP input and ZIP output are
     supported; 7Z input is not yet supported and is tracked by #8.
   - ZIP caution notes only: very large ZIPs and third-party ZIPs from external
     tools may still expose producer-specific behavior that is not represented
     by the generated corpus. Treat those as beta caution items unless a
     concrete failure is found.

2. Continue issue #6 file comparison validation and reload work after the
   release gate.
   - Use mixed files and folders to verify pair counts, status filtering, and
     criterion details.
   - Validate hash and byte-to-byte range settings with large files before
     adding JSON import/reload.
   - Validate the expanded option UI manually, especially common-name thresholds,
     middle-part start/length units, archive same-relative-path pairing, narrow
     result dialog sizes, and the duplicate-delete step editor.
   - Continue Explorer and native ShellExt smoke testing for the exposed
     `/context FileCompare` route.

3. Continue UI validation for the 1.4 beta surfaces when manually exercised.
   - MainForm planner: validate small, default, and wide window sizes, then
     revisit splitter constraints, toolbar scale fit, and grouped-row visual
     polish.
   - Rename review dialog: decide whether generated conflict suffixes should
     remain `Conflict` or become `Auto-resolved conflict`, then consider
     persisting the last dialog size if long filenames are common in real use.

4. Defer lower-priority feature tracks.
   - #3 external providers, #7, and #8 still carry explicit deferred status and
     resume conditions in GitHub.
   - #3 local pattern learning may continue independently because it stays local,
     review-first, and disconnected from network/content-inference providers.
   - #9 general file-content merge remains deferred; the archive-first output
     naming and preview slice is implemented.
   - Do not pull these into the active work queue until the resume conditions in each issue are satisfied.
