# MainForm UX Review

Review date: 2026-06-03

Scope:

- `src/FileTools.App/Ui/MainForm.Designer.cs`
- Related display logic in `MainForm.cs`, `WorkPlan.cs`, `PlanStepDialog.cs`, and `OperationResult.cs`

![Current MainForm designer layout](images/current-mainform-designer-layout.svg)

## Summary

The standalone window is now organized as a planner: targets on the left, task planning on the right, and execution feedback at the bottom. The latest layout pass addresses the earlier command-mixing problem by introducing a menu bar, icon toolbars, an unwrap split button, a plan scope header, and a bottom-right run/stop button paired with a compact log view.

The README now references `docs/images/current-mainform-designer-layout.svg`, which describes the current planner-oriented layout.

## Implemented Layout Changes

- The left target area uses a read-only `DataGridView` with system icons, name, parent location, and action count.
- File, task, and settings commands are available from a `MenuStrip`.
- Target add/remove/reorder/clear commands sit near the target grid as icon toolbar commands.
- Main task commands sit in a fixed `ToolStrip` above the plan area.
- Folder unwrapping uses `ToolStripSplitButton` for default unwrap, same-name unwrap, single-file mismatch modes, and moving direct child files upward.
- The work plan area now uses a read-only `DataGridView` with order, icon-labeled action kind, and expected result columns.
- Step options are moved out of the grid body and into row tooltips, keeping the visible grid focused on action and outcome.
- Step delete and clear-all-for-current-target commands sit on a horizontal icon+text toolbar above the plan grid.
- The work plan area shows which target is currently displayed, how many targets are selected, and the selected targets' planned step count for multi-target selections.
- Plan previews are rebuilt from the remaining step chain after add, edit, delete, or clear so downstream steps reflect the current virtual input path.
- The old always-large result box is replaced by a compact bottom log view.
- Execution uses one bottom-right button that shows run in the idle state and stop while running.
- Command state updates disable commands that do not apply to the current selection or execution state.

## Remaining UX Notes

### 1. Plan scope is now visible but still selected-target-first

The target grid shows per-target action counts, and the right plan grid still shows the currently selected target only. The new scope header reduces ambiguity by showing the displayed target, selected target count, and the selected targets' aggregate planned step count.

### 2. Icon-only commands need real-use validation

The menu bar gives every command a textual fallback, and tooltips name the toolbar actions. Still, first-time users may need icon+text for task commands if the icons are not obvious enough in practice.

### 3. The log is intentionally lightweight

The bottom log now handles progress and summary feedback. It is not a structured result viewer. If result review becomes a primary workflow, add a separate detail table with severity, target, operation, and message columns.

### 4. Layout proportions are not final

The form still starts at `980 x 700` and uses a fixed left split distance. This is reasonable while the plan/result model is still moving, but the next layout pass should revisit minimum size, splitter constraints, and how much vertical space the log should occupy.

### 5. Preview coverage still has real-file limits

The plan grid now predicts rename, wrap, unwrap, and relocation where enough path information is available. Some folder operations still depend on actual folder contents, so the grid may show a warning when a future virtual folder cannot be inspected.

## Suggested Next Priority

1. Decide whether task toolbar buttons should remain icon-only or become icon+text.
2. Revisit bottom log height and splitter constraints after using the new layout.
3. Consider a richer execution result viewer if result review becomes a primary workflow.
