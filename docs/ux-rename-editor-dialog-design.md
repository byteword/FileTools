# Rename Editor Dialog UX Design

Design date: 2026-06-02

Scope:

- Current review surface: `src/FileTools.App/Ui/RenameReviewDialog.cs`
- Rename parsing model: `src/FileTools.App/Naming/NamingCore.cs`
- Standalone plan editing entry points: `RenameReviewDialog.EditPlanStep` and `EditPlanSteps`
- ContextMenu apply entry point: `RenameReviewDialog.ShowAndApply`

Concept reference:

![Rename editor dialog concept](images/rename-editor-dialog-concept.svg)

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

- `원본 복사`: copy original full filename into the target editor
- `추천안 복원`: restore generated suggested filename
- `확장자 잠금`: keep the original extension unless explicitly unlocked

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
- Extension is read-only by default for files and empty for folders.
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

### 6. Suggestions and Reasons

Show a compact "extraction evidence" area:

- Reasons from `RenamePreview.Reasons`
- Correction candidates from `RenamePreview.Candidates`
- Dictionary replacements that affected the name

Candidates should be actionable:

- Click candidate title to apply it to the Title field.
- Click full candidate name to replace the final filename.

Reasons should remain secondary. They explain why the row needs review, but they should not compete with the editor.

### 7. Validation Panel

Validation should be visible near the target name:

- Invalid Windows filename
- Empty target name
- Duplicate target among selected rows
- Existing file/folder target
- Extension changed while extension lock is enabled

The Apply/OK button remains disabled for blocking errors. Non-blocking warnings, such as extension changes after unlock, should allow apply but stay visible.

### 8. Batch-Oriented Controls

For multi-select renaming, add cautious batch helpers:

- `현재 구조를 같은 패턴 항목에 적용`
- `검토 필요 항목으로 이동`
- `다음 문제`
- `이 항목 건너뜀`

Batch application should be constrained to rows with compatible extracted parts. It should not blindly copy the whole filename to every row.

## Interaction Model

1. Dialog opens with the first conflict or review-needed row selected.
2. The user sees original filename, suggested filename, extracted parts, and reasons in one panel.
3. The user edits either the final filename or structured fields.
4. Validation runs immediately after edit changes, not only after leaving a grid cell.
5. When a row becomes valid/resolved, the status updates in the left list.
6. `다음 문제` moves to the next invalid, conflict, or review-needed row.
7. Apply/OK succeeds only when there are no blocking errors.

## Keyboard Behavior

Recommended shortcuts:

- `Ctrl+C` on original field: copy selected original text.
- `Ctrl+Shift+C`: copy original full filename.
- `Ctrl+R`: restore generated suggestion for current row.
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
- Use `RenamePreview.Reasons` and `RenamePreview.Candidates` in the detail panel.

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
- Token insert buttons from original stem, parsed parts, reasons/candidates.
- Immediate validation and current summary behavior.
- Next problem navigation.

Defer:

- Batch pattern application.
- Persisted dialog layout.
- Advanced token selection from arbitrary original-name text ranges.
- Per-user custom compose templates.

## Acceptance Criteria

- A user can edit a long target filename without using a grid cell.
- A user can reuse the original title or extracted strings with one click.
- The first problematic row is selected automatically.
- Generated candidates and extraction reasons are visible for the selected row.
- Conflict/invalid rows still block Apply/OK.
- Existing ContextMenu apply and standalone plan-editing flows keep their current semantics.
