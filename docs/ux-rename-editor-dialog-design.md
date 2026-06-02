# Rename Editor Dialog UX Design

Design date: 2026-06-02

Scope:

- Current review surface: `src/FileTools.App/Ui/RenameReviewDialog.cs`
- Rename parsing model: `src/FileTools.App/Naming/NamingCore.cs`
- Standalone plan editing entry points: `RenameReviewDialog.EditPlanStep` and `EditPlanSteps`
- ContextMenu apply entry point: `RenameReviewDialog.ShowAndApply`

Concept reference:

![Rename editor dialog concept](images/rename-editor-dialog-concept.svg)

## Implementation Status

Implemented on 2026-06-02 in `src/FileTools.App/Ui/RenameReviewDialog.cs`.

The first pass keeps the existing rename operation semantics and replaces the grid-cell editor with a selected-item editor:

- Read-only left grid for item selection and status.
- Selected-item editor for original name, final new name, extracted title, episode, author, tags, and extension.
- `원본 사용` and `자동 이름 사용` quick actions.
- Token insertion from original filename parts, parsed parts, and generated correction candidates.
- Read-only common phrase chips from the existing app common phrase dictionary.
- Immediate validation, conflict highlighting, summary counts, and next-issue navigation.

Not implemented in this pass:

- Filter buttons.
- Skip-current-item behavior.
- Batch pattern application.
- Common phrase add/remove from inside the rename dialog.
- User-defined correction rule creation.
- Internet dictionary or AI-assisted correction.

## Problem

The current rename review dialog uses a grid as both the list and the editor. That works for simple before/after verification, but it is weak for the actual rename task:

- Long file names are hard to edit inside a grid cell.
- The original name is visible, but copying useful portions from it is awkward.
- Parsed title, episode range, tags, author, reasons, and correction candidates exist in the rename engine but are not exposed as editing aids.
- Users cannot easily switch between direct filename editing and structured title/episode/tag editing.
- Conflict and validation feedback is row-level, while the editing decision usually needs selected-item detail.

## UX Principle

Keep the grid/list for navigation and status. Move filename editing into a dedicated selected-item editor.

The dialog should behave like a small rename workbench:

- Left side: which item needs attention.
- Right side: what the original was, what the target will become, and which extracted values can be inserted or corrected.
- Bottom: whether all rows are safe to apply.

## Recommended Layout

### 1. Header

Use a compact header with:

- Dialog title: `이름변경 검토`
- Summary counters: total, changed, needs review, conflicts, resolved
- Optional filter buttons: `전체`, `검토`, `충돌`, `변경됨`

The filter buttons are not the primary feature. They are useful when many files are selected.

### 2. Left Item List

Replace the editable grid with a selection list or read-only grid:

- File/folder icon
- Original filename, ellipsized in the middle if needed
- Status badge: ready, review, conflict, resolved, unchanged, invalid
- Small target preview line

Sorting remains the same as the current dialog: conflicts first, then review-needed rows, then ready rows, then unchanged rows.

The left list should not allow inline editing. Double-clicking an item should focus the main new-name field.

### 3. Selected Item Comparison

At the top of the right panel, show two full-width filename fields:

- `기존 이름`: read-only, selectable text
- `바뀔 이름`: editable final filename

The final filename field is the source of truth. Structured part fields update it, and direct edits update the preview/validation state.

Recommended quick actions next to these fields:

- `원본 사용`: copy original full filename into the target editor
- `자동 이름 사용`: replace the target editor with the generated automatic correction result
- `확장자 잠금`: keep the original extension unless explicitly unlocked

Naming note:

- Prefer `자동 이름 사용` for the button label. It is shorter and more natural than `추천안 복원`.
- If the action needs to emphasize undoing user edits later, `자동안으로 되돌리기` is acceptable for a tooltip or menu item, but it is too long for the primary button.

### 4. Extracted Parts Editor

Expose `RenamePreview.Parts` as normal fields:

- Title
- Episode range
- Author
- Tags
- Extension

This should be a structured editor, not only text chips. The user can correct the extracted title or episode range directly and immediately see the composed filename.

Recommended behavior:

- Title and episode fields are normal text boxes.
- Tags are removable chips with an add field.
- Author is optional and can be cleared.
- Extension is read-only in the first implementation for files and empty for folders.
- Every part has a small insert/copy affordance where useful.

### 5. Original Token Strip

Below the original filename, show useful source tokens derived from parsing and simple tokenization:

- Original stem
- Parsed title
- Episode range
- Author candidate
- Tags/bracket contents
- Correction candidates

Each token is an insert button. Clicking inserts the token at the current caret position in the active field, or replaces the selected text.

This directly solves the "original title copy" problem without requiring manual selection inside a long filename.

### 6. Common Word Set

Replace the bottom "extraction evidence" area with an app-level common word set. In the first implementation this is backed by the existing common phrase dictionary and is insert-only inside the rename dialog:

- Frequently used title words, author names, tags, and domain phrases.
- Words from the current `CommonPhrases`/lexicon configuration.
- Recently used words from accepted rename edits. Deferred.
- User-added words that improve future correction candidates. Deferred.

Each word should be actionable:

- Click to insert into the active text field.
- Right-click or small menu to remove from the app word set. Deferred.
- Add button to register the current selected text as a common word. Deferred.

Extraction reasons can still exist internally and may be useful in a diagnostic tooltip, but they should not take visible bottom-panel space in the normal editor.

### 7. Correction Rule Management

The editor should leave room for future user-defined rename correction rules.

Recommended future rule entry points:

- `규칙 추가`: create a correction rule from the current edit, such as `ㅇr -> 아` or `vol. -> 권`.
- `이 항목에서 규칙 만들기`: compare original and accepted target names and suggest a reusable replacement.
- `규칙 관리`: open the rename dictionary/rule editor from this dialog.

Rules should remain explicit and reviewable. The app should not silently create broad rules from one edit.

### 8. Validation Panel

Validation should be visible near the target name:

- Invalid Windows filename
- Empty target name
- Duplicate target among selected rows
- Existing file/folder target
- Extension changed while extension lock is enabled

The Apply/OK button remains disabled for blocking errors. Non-blocking warnings, such as extension changes after unlock, should allow apply but stay visible.

### 9. Batch-Oriented Controls

For multi-select renaming, add cautious batch helpers:

- `현재 구조를 같은 패턴 항목에 적용`
- `검토 필요 항목으로 이동`
- `다음 문제`
- `이 항목 건너뜀`

Batch application should be constrained to rows with compatible extracted parts. It should not blindly copy the whole filename to every row.

## Interaction Model

1. Dialog opens with the first conflict or review-needed row selected.
2. The user sees original filename, suggested filename, extracted parts, insertable tokens, and common phrases in one panel.
3. The user edits either the final filename or structured fields.
4. Validation runs immediately after edit changes, not only after leaving a grid cell.
5. When a row becomes valid/resolved, the status updates in the left list.
6. `다음 문제` moves to the next invalid, conflict, or review-needed row.
7. Apply/OK succeeds only when there are no blocking errors.

## Keyboard Behavior

Recommended shortcuts:

- `Ctrl+C` on original field: copy selected original text.
- `Ctrl+Shift+C`: copy original full filename.
- `Ctrl+R`: use the generated automatic name for current row.
- `Ctrl+Enter`: apply current part fields to final filename.
- `Alt+Down`: next problem row.
- `Esc`: cancel, matching existing dialog behavior.

The UI should also work without shortcuts. Shortcuts are accelerators, not instructions shown as primary text.

## Implementation Shape

The smallest useful implementation can keep the existing `RenameReviewDialog` class and change its layout:

- Keep `RenameRow`, validation, conflict detection, summary, and edited preview creation.
- Replace the editable `DataGridView` with a read-only row selector plus a selected-row editor.
- Bind selected-row editor controls to the current `RenameRow`.
- Add a `FileNameParts` draft for each row so structured fields can compose `SuggestedName`.
- Use `RenamePreview.Candidates` as actionable candidate tokens.
- Keep `RenamePreview.Reasons` available for diagnostics, but do not show it as a default bottom section.

Suggested helper types:

- `RenameEditorState`: selected row, active edit mode, extension lock state.
- `RenamePartDraft`: title, episode range, author, tags, extension, direct filename override.
- `RenameToken`: label, value, source kind, target insertion behavior.

No operation semantics need to change. `CreateEditedPreviews` can continue to produce the final safe `SuggestedFileName` values.

## MVP Scope

Implement first:

- Two-pane dialog.
- Read-only row list.
- Original/new filename comparison.
- Final filename textbox.
- Structured title, episode, author, tags, and extension fields.
- Token insert buttons from original stem, parsed parts, and correction candidates.
- Insert-only common phrase chips from the existing app-level dictionary.
- Immediate validation and current summary behavior.
- Next problem navigation.

Defer:

- Filter buttons.
- Skip-current-item behavior.
- Batch pattern application.
- Persisted dialog layout.
- Advanced token selection from arbitrary original-name text ranges.
- Per-user custom compose templates.
- Common phrase add/remove controls in the rename dialog.
- Full correction-rule creation from accepted edits.
- Dictionary, internet dictionary, or AI-assisted automatic correction.

## Long-Term Direction

After the editor stabilizes, automatic correction can grow in three layers:

- Local dictionary/rules: deterministic user-controlled replacements and common words.
- External dictionary lookup: optional internet dictionary support for ambiguous or corrupted terms.
- AI-assisted correction: optional candidate generation for difficult names, always presented as reviewable suggestions rather than silent changes.

The first implementation should focus on local rules and common words because they are predictable, explainable, and cheap to run.

## Acceptance Criteria

- A user can edit a long target filename without using a grid cell.
- A user can reuse the original title or extracted strings with one click.
- A user can insert app-level common words without leaving the dialog.
- The first problematic row is selected automatically.
- Generated candidates are visible as actionable options for the selected row.
- Conflict/invalid rows still block Apply/OK.
- Existing ContextMenu apply and standalone plan-editing flows keep their current semantics.
