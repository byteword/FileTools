# Rename Dialog UX Review

Review date: 2026-06-03

Scope:

- `src/FileTools.App/Ui/RenameReviewDialog.cs`
- `src/FileTools.App/Configuration/SettingsStore.cs`
- `src/FileTools.App/Ui/SettingsForm.cs`
- `src/FileTools.App/Resources/Strings.resx`
- `src/FileTools.App/Resources/Strings.ko.resx`

Current reference, refreshed on 2026-06-03 to match the current WinForms dialog:

![Current rename editor dialog](images/rename-editor-dialog-concept.svg)

## Summary

The rename dialog now supports two review modes:

- Always review before applying. This is the default.
- Review only when at least one generated rename needs review or has a conflict.

The ContextMenu execution flow still uses the dialog as the apply surface when review is required. The main application planner still uses the same dialog as a plan-editing surface, where OK stores the manual target filename instead of mutating the file system.

The current dialog uses a two-pane editor: a read-only item list on the left and a selected-item rename editor on the right.

## Implemented Changes

- Replaced the old rename review checkbox with a `RenameReviewMode` selection.
- Kept `Always` as the default review mode.
- Added the secondary automation mode: open review only when a row is `NeedsReview` or `Conflict`.
- Added a top-right summary line showing total items, changed items, review-needed rows, conflicts, and resolved conflicts.
- Removed the reason column from the main grid.
- Rebalanced the grid around the core comparison: original name `>` new name.
- Localized row status labels instead of exposing raw enum names.
- Sorts conflict and review-needed rows above normal rows on initial load.
- Highlights review-needed and conflict rows with warm warning colors.
- Shows resolved conflict rows with a green background after user edits clear the conflict state.
- Validates rows after filename edit completion.
- Disables Apply/OK while a row has a blocking validation error such as an empty name, invalid filename, duplicate target, or existing target path.
- Sanitizes edited filenames through `WindowsFileNameSafety.MakeSafeFileName` after edit completion.
- Adds cell tooltips for full source path, target path, and validation status.
- Replaced grid-cell filename editing with a selected-item editor.
- Shows original and new filename fields for the selected row.
- Adds `Use original` / `Use automatic name` actions.
- Exposes extracted title, episode, author, tags, and extension fields.
- Re-composes the target filename when extracted part fields change.
- Provides insertable token buttons from original text, parsed parts, and correction candidates.
- Extracts cleaned title tokens from correction candidate filenames before showing the full candidate filename, so a candidate such as `[Monaka] 아가씨는 벌 받는 걸 좋아해 10권` also offers `아가씨는 벌 받는 걸 좋아해`.
- Shows existing common phrases as insertable chips when configured, collapsed to one row by default with More/Collapse controls for large phrase sets.
- Keeps token and common-phrase chips out of the focus chain, so a first click inserts into the active editor instead of first triggering editor focus loss and panel rebuild.
- Keeps editor selections visible after focus moves away, which makes the pending insertion point clear when using helper chips or command buttons.
- Limits token and common-phrase panel rebuilds to row synchronization, resize, and expand/collapse paths; normalizing the current filename now updates only the filename field and validation state.
- Gives the footer button row and command buttons additional height so OK/Apply and Cancel are not clipped by default WinForms margins or DPI scaling.
- Adds a rule-trace action so the selected row can show which built-in or user correction rules changed the name or produced candidates before apply.
- Adds next-issue navigation.

## Remaining UX Notes

### 1. Conflict semantics are intentionally conservative

Generated conflict rows still appear as conflicts until the user edits them. This keeps automatically suffixed names visible for review. If users find this too heavy, the next pass can split the status into `Conflict` and `Auto-resolved conflict`.

### 2. Candidate details are exposed as insert tokens

The reason column remains hidden to keep the list focused, but candidate alternatives are now available as selected-row token buttons. Candidate filenames are also reduced to title-only token options by removing bracket metadata and trailing episode or volume suffixes before the full candidate filename is shown. Internal extraction reasons are still not shown in the default editor surface.

Recommended next change:

- Add an optional diagnostic details view if extraction reasons become important during real use.

### 3. Extension changes are still possible through direct filename editing

The structured extension field is read-only, but users can still edit the whole target filename directly. This is flexible, but accidental extension removal or replacement is possible.

Recommended next change:

- Warn when a file target's extension changes.
- Consider a future split editor with stem and extension fields if accidental extension edits become common.

### 4. Size persistence is not implemented

The dialog now has a larger minimum size and resizable layout, but it does not remember the last size.

Recommended next change:

- Persist the last rename dialog size in user settings if long filenames are common in real use.

## Suggested Next Priority

1. Decide whether generated conflict suffixes should show as `Conflict` or `Auto-resolved conflict`.
2. Add extension-change warnings for file targets.
3. Remember the last dialog size after real-use validation.
4. Consider in-dialog common phrase add/remove after the editor is validated in real use.
5. Consider a non-modal rule trace panel if users need to compare traces across many rows.
