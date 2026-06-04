# Name Template and Collision Policy

This document records the naming design used by folder wrap/unwrap and the extension points reserved for future merge operations.

## Scope

The current implementation adds a shared naming foundation and applies it to folder wrap/unwrap execution, work-plan prediction, and work-plan preview. It does not yet expose a settings UI for custom templates.

## Template Evaluation

Name templates are evaluated by `NameTemplateResolver`. Token values come from `INameTemplateTokenProvider` implementations so new token families can be added without rewriting the parser.

Supported initial tokens:

```text
{FileName}
{FileStem}
{Extension}
{ExtensionNoDot}
{FolderName}
{ParentFolderName}
{CommonStem}
{FirstFileStem}
{SelectedCount}
{TargetExtension}
{Stem}
{Index}
{IndexLabel}
```

`{Extension}` includes the dot, such as `.jpg`. `{ExtensionNoDot}` omits it.

The parser also supports simple numeric formatting on integer tokens:

```text
{Index:000}
{SelectedCount:00}
```

Literal braces can be escaped with doubled braces:

```text
{{FileStem}}
```

## Folder Wrap/Unwrap Defaults

Folder wrap now resolves the target folder name through the shared template helper:

```text
{FileStem}
```

Single-file folder unwrap still keeps the original file name when the folder name and file stem match. When they differ, the existing mismatch presets map to templates:

```text
KeepFileName     -> {FileName}
UseFolderName    -> {FolderName}{Extension}
PrefixFolderName -> {FolderName}-{FileStem}{Extension}
```

This preserves the existing behavior while removing duplicate name-building logic from execution, prediction, and preview paths.

## Collision Policy

Collision handling is separate from name template evaluation. `NameCollisionResolver` supports these policies:

```text
Skip
AutoNumber
Ask
MergeIntoExisting
```

Current folder wrap/unwrap behavior remains conservative:

```text
Folder wrap target folder: MergeIntoExisting
Folder wrap target file: Skip
Folder unwrap target file: Skip
```

`MergeIntoExisting` for folder wrap preserves the old behavior where an existing same-name folder can receive the file if the child file path is not already present.

The default auto-number conflict template is:

```text
{Stem} ({Index}){Extension}
```

Index labels can be formatted as:

```text
Number
ZeroPadded3
Roman
KoreanNumber
KoreanHeavenlyStem
Alphabet
```

These labels are prepared for future user-visible conflict settings and merge features.

## Rename Correction Integration

The safest current integration is work-plan chaining:

```text
File name correction -> Folder wrap
```

In that chain, `{FileStem}` is evaluated from the already-renamed file path.

A future `RenameCorrectionTokenProvider` can add tokens such as:

```text
{CorrectedFileName}
{CorrectedFileStem}
{Title}
{EpisodeRange}
{Author}
{Tags}
```

Correction-derived tokens must carry review state. If the correction preview requires review or has a conflict, non-interactive context-menu execution should skip or fall back instead of silently applying an uncertain generated name.

## Future Merge Operations

The template foundation leaves room for these operations:

```text
Multiple files -> one folder
Multiple folders -> one folder
Multiple archives -> one archive
```

Reserved default templates:

```text
MultiFileMergeFolderNameTemplate   = {CommonStem}
MultiFolderMergeFolderNameTemplate = {CommonStem}
ArchiveMergeFileNameTemplate       = {CommonStem}{TargetExtension}
```

Multiple-folder merge will need a separate merge layout policy:

```text
PreserveSourceFolder
GroupBySourceName
Flatten
```

`PreserveSourceFolder` is the safest default because it avoids structure loss and reduces internal name collisions. `Flatten` should stay explicit because it can create many child conflicts.
