# Settings Dialog UX Review

Review date: 2026-06-02

Scope:

- `src/FileTools.App/Ui/SettingsForm.cs`
- `src/FileTools.App/Configuration/SettingsStore.cs`
- `src/FileTools.App/Program.cs`
- `src/FileTools.App/Shell/ContextMenuRegistrar.cs`
- Related standalone action dialogs in `PlanStepDialog.cs`

![Settings dialog UX structure](images/settings-dialog-ux-structure.svg)

## Summary

The settings dialog is functionally compact and covers the current core defaults: Explorer ContextMenu registration, rename dictionary behavior, AutoRelocation default template, and folder structure defaults. The strongest UX risk is not visual density itself, but that users cannot always tell whether they are changing an app preference, an Explorer shell registration, or a per-run operation default.

The dialog should keep settings scoped to repeatable defaults. One-off execution choices should stay in action dialogs or the main planner. If the settings are reorganized into one vertically listed panel, the groups should be collapsible rather than a long flat list.

## Current Strengths

- The tab split maps to the current feature areas: Context Menu, Rename, Auto Relocation, and Folder Structure.
- Context menu commands can already be enabled independently, which is useful for reducing Explorer menu clutter.
- Rename dictionaries, common phrases, and relocation templates are reachable from the settings window without exposing their implementation files.
- AutoRelocation and folder unwrap options are also available at the action-step level through `PlanStepDialog`, so the app already has a path for per-run overrides.

## UX Issues

### 1. Context menu settings mix state, command visibility, and installation actions

`SettingsForm` puts the registration checkbox, layout combo, command checkboxes, and Install/Remove buttons into one tab. Pressing OK also synchronizes Explorer registration through `SyncContextMenuRegistration`, so users can change system registration even if they never press Install or Remove.

Recommended change:

- Add a sticky status header such as `Explorer menu: registered / not registered`.
- State that `OK` applies Explorer menu changes.
- Disable command checkboxes when registration is off, or show that they are saved but inactive.
- Replace separate Install/Remove buttons with one context-sensitive primary action, or move them into an advanced section.

The status header should sit outside the scrollable settings body. It should remain visible while vertical scrolling through option groups, because it explains the current shell-registration state and the side effect of pressing OK.

### 2. The first tab is the most technical tab

The first visible tab is `Context Menu`, but many users will open settings to change operation defaults. Explorer registration is important, but it is more system-level than task-level.

Recommended change:

- Consider a left navigation or reordered tabs: `기본값`, `Explorer 메뉴`, `사전/템플릿`, `고급`.
- If tabs remain, keep operation defaults before shell integration unless most users primarily use Explorer.

### 3. Some labels need examples, not just names

Options such as `단일파일 불일치`, `파일명 유지`, `폴더명으로 변경`, and `폴더명-파일명으로 변경` are correct but abstract. This setting affects actual file names and should show an example.

Recommended change:

- Add inline examples such as `FolderA\Image01.jpg -> Image01.jpg`, `FolderA.jpg`, or `FolderA-Image01.jpg`.
- Add a context menu layout preview for `묶음형` vs `펼침형`.
- Use short helper text for `현재 폴더에서 자동 재배치` versus `선택한 폴더로 자동 재배치`.

### 4. Rename review mode is now explicit

The old boolean rename-review setting has been replaced with a `RenameReviewMode` selection. The default is always review, and the secondary option opens the review dialog only when a generated row needs review or has a conflict.

Remaining note:

- Keep validation mandatory for invalid names, duplicate targets, and dangerous conflicts even when the secondary automation mode is enabled.

### 5. Hidden group flags are forced on

`ContextMenuFolderStructure` and `ContextMenuAutoRelocation` exist in settings, but `SettingsForm.SaveSettingsFromUi` forces them to `true`. The UI only exposes child command toggles.

Recommended change:

- This is acceptable if group-level disable is not needed.
- If menu clutter is a common problem, expose group header toggles: `폴더 작업 표시`, `자동 재배치 표시`.

### 6. Fixed dimensions may age poorly

The dialog uses a fixed starting size and many hard-coded widths. Current Korean and English strings mostly fit, but new explanatory copy or longer template names will make this brittle.

Recommended change:

- Use wider combo rows or dynamic sizing for template names.
- Put explanatory text under each row and allow wrapping.
- Keep the minimum size, but avoid assuming 660px row widths.

### 7. A single-panel layout needs collapsible option groups

If the tabbed layout is replaced or supplemented with one panel that lists every option group vertically, a flat list will become hard to scan. Collapsible groups are the better fit.

Recommended behavior:

- Each option group has a title row that remains visible when collapsed.
- A small marker shows state: for example right-pointing chevron when collapsed and down-pointing chevron when expanded.
- The collapsed title row should include a compact summary such as `등록됨`, `6개 활성`, `항상 검토`, or the selected template name.
- Expanding and collapsing should be possible by clicking the title row and by keyboard focus with Enter or Space.
- Vertical scrolling is expected, but horizontal scrolling should be avoided.
- The bottom OK/Cancel buttons should stay fixed outside the scrollable settings body.

Recommended styling:

- Give each group a distinct but restrained identity color.
- When expanded, apply the group color to the title row background.
- Keep the body background neutral and use the group color only for the border or a subtle side accent.
- When collapsed, show only the title row, the marker, and the compact summary.
- Avoid using color alone to communicate state; combine color with text, marker, or disabled control state.

## Feature Granularity And Settings Additions

Add or change settings only where they protect repeat workflows:

- Rename review mode: implemented as a selection, with `항상 검토` as the default and `검토 필요/충돌이 있을 때만 검토` as the safer automation option.
- Explorer menu group toggles: useful if users want to hide whole feature families, not just individual commands.
- AutoRelocation default target root: useful only if users repeatedly send files to one library folder. Otherwise keep target selection per run.
- Collision handling: consider `skip`, `unique suffix`, or `ask` only if users need consistent behavior across rename, wrap, unwrap, and relocation. Today the app uses mixed safety behavior by operation.
- Folder unwrap preview examples: this is more important than adding more folder settings.

Avoid adding these as global settings for now:

- Per-step AutoRelocation target root, because it already belongs in the action dialog.
- Per-step folder unwrap mode beyond the existing action dialog override.
- Template-rule internals in the main settings dialog; keep them in the dedicated template editor.

## Suggested Priority

1. Add a sticky Explorer registration status row and make OK/apply behavior explicit.
2. If settings move into one vertical panel, use collapsible option groups with summaries.
3. Add examples/previews for folder unwrap mismatch and context menu layout.
4. Decide whether group-level context menu toggles should be surfaced.
