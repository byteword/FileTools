Review date: 2026-06-03

Scope:

- `src/FileTools.App/Configuration/RenameRuleStore.cs`
- `src/FileTools.App/Naming/NamingCore.cs`
- `src/FileTools.App/Ui/RenameRuleEditorDialog.cs`
- `src/FileTools.App/Ui/RenameReviewDialog.cs`

Current reference:

![Rename rule editor concept](images/rename-rules-editor-concept.svg)

## Summary

The rename correction flow now uses an app-level rule document instead of treating every correction as hidden code. Built-in correction steps are represented as named rules, and user rules can be added for common deterministic edits.

The first implementation intentionally avoids script execution. Script rules remain a documented future extension because they need a separate security, timeout, and error-isolation design.

## Rule Model

Each rule has:

- Stable ID.
- Display name and description.
- Stage.
- Kind.
- Enabled state.
- Application mode.
- Order within its stage.
- Optional source/replacement values.

Supported stages:

- `Preprocess`
- `UserRewrite`
- `Candidate`
- `Extract`
- `Compose`
- `Finalize`

Supported modes:

- `Automatic`: changes the working name directly.
- `Review`: changes the working name and marks the row as review-needed.
- `CandidateOnly`: does not change the default suggestion, but records a candidate when the rule can produce one.

## Built-In Rules

The default built-in rules are:

- Unicode NFC/Hangul jamo normalization.
- UTF-8/Latin-1 mojibake recovery.
- Existing rename dictionary application.
- Separator and whitespace normalization.
- Obfuscated Hangul candidate generation.
- Bracket tag/author extraction.
- Author pattern extraction.
- Episode extraction and normalization.
- Title cleanup.
- Windows filename safety.

Windows filename safety is required and remains active even when shown in the rule list.

## User Rules

The first user-rule set supports:

- Literal string replacement.
- Prefix trimming.
- Suffix trimming.
- Whitespace normalization.
- Separator normalization.
- Regex replacement.

User rules can run in `Preprocess` or `UserRewrite`. Ordering is stage-scoped: moving a rule changes its position only among rules in the same stage. This avoids unsafe cross-stage changes such as running extraction before normalization.

## Preview And Trace

Rename preview now records rule traces with before/after values. The rename review dialog exposes a `Rule trace` action for the selected row so users can see which rules changed a filename or produced candidates before applying the rename.

## Script Rules

Script-backed rules are deferred. If added later, they should follow these constraints:

- Disabled by default.
- No file system, network, or external process access.
- Typed input only: original name, current stem, extension, parsed parts, and current candidate.
- Typed output only: new name, reason, and review-required flag.
- Timeout and exception isolation per rule.
- Trace every script result.
- Default to review-required.

This keeps the app deterministic for the current implementation and leaves script support as a controlled extension point rather than an unrestricted automation surface.
