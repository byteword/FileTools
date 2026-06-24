# Name Template and Collision Policy

This document records the naming design used by folder wrapping, folder merge,
and archive merge.

## Scope

The current implementation adds a shared naming foundation and applies it to
folder wrap/unwrap execution, work-plan prediction, work-plan preview,
selected-target folder merge, and archive merge output naming. Folder name
templates and collision policy can be edited from the folder-structure settings
group.

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
{CorrectedFileName}
{CorrectedFileStem}
{Title}
{EpisodeRange}
{Author}
{Tags}
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

The user-facing Korean name for folder wrapping is `폴더 씌우기`. It means
wrapping each selected file in its own same-name folder:

```text
Book 01.zip -> Book 01\Book 01.zip
Book 02.zip -> Book 02\Book 02.zip
```

This operation is separate from `폴더 합치기`, which moves multiple selected
targets into one generated folder.

Folder wrap now resolves the target folder name through the shared template helper:

```text
{CorrectedFileStem}
```

If correction is unavailable or requires review, the folder wrap fallback is the
safe original file stem. This keeps context-menu execution deterministic while
still allowing safe automatic correction to participate in generated folder
names.

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
Folder wrap existing target folder: MergeIntoExisting
Folder wrap target file: Skip by default
Folder unwrap target file: Skip by default
```

`MergeIntoExisting` for folder wrap preserves the old behavior where an existing same-name folder can receive the file if the child file path is not already present.

For the general folder wrap/unwrap name-collision resolver, `Ask` remains a reserved engine value. Current settings load/save and folder-structure collision option creation normalize `Ask` to `Skip`. Archive merge has its own `Ask` handling, described below, because it can surface merge-specific existing/current entry details.

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

These labels are available in the folder name template settings dialog and are also reserved for future merge features.

## Rename Correction Integration

The safest integration remains work-plan chaining:

```text
File name correction -> Folder wrap
```

In that chain, `{FileStem}` is evaluated from the already-renamed file path.

`RenameCorrectionNameTemplateTokenProvider` adds correction-derived tokens:

```text
{CorrectedFileName}
{CorrectedFileStem}
{Title}
{EpisodeRange}
{Author}
{Tags}
```

Correction-derived tokens must carry review state. If the correction preview requires review or has a conflict, non-interactive context-menu execution should skip or fall back instead of silently applying an uncertain generated name.

In the current implementation, correction-derived tokens are unavailable when the correction preview is `NeedsReview`, `Conflict`, or `Skipped`. A template that depends on those tokens then falls back to the operation's safe default name.

## Common Merge Name Analysis

Folder merge and archive merge use `MergeNameProposalBuilder` before template
evaluation. It first asks the rename-correction pipeline for safe corrected
stems, falls back when correction requires review or fails, and then runs the
logical-name analyzer. The analyzer does not only compare the leading prefix.
It tokenizes selected stems, looks for stable text tokens anywhere in the name,
extracts numeric or text ranges, and builds a merged stem from the most useful
shared structure.

Examples:

```text
A 01.zip + A 02.zip                    -> A 01~02.zip
A 01~03.zip + A 04~06.zip              -> A 01~06.zip
A 01~03.zip + A 05~08.zip              -> A 01~03, 05~08.zip
test이름 tt.zip + 이름abc.zip          -> 이름.zip
이름 a태그.zip + 이름 b태그.zip        -> 이름 a~b 태그.zip
```

The analyzer keeps numeric padding from the selected names. Contiguous or
overlapping ranges are merged. Disjoint ranges are kept as a comma-separated
range summary. When a stable common token exists in the middle of the names but
the surrounding tokens do not form a reliable template, that token can still be
used as the logical common stem.

Correction-derived candidates can participate only when they are safe for
automatic execution. If automatic name correction reports `NeedsReview`,
`Conflict`, or `Skipped`, merge-name proposal falls back to the original stems
instead of silently applying an uncertain generated name.

## Final Name Review Flow

Operations that create a new destination name should consistently pass through a
final destination-name review surface before execution:

```text
Select targets -> analyze name -> edit final destination name -> OK -> execute
```

For file rename operations, the first confirmation surface is intentionally
simple: it shows the original name, one editable new-name field, and
`Advanced`/`OK`/`Cancel` buttons. `Advanced` opens a single-name editor for the
currently selected final name. The editor shows the original name, editable new
name, original restore and automatic-correction actions, recommended text, and
`OK`/`Cancel`.

The review surface differs by operation shape:

```text
Folder wrapping: one editable destination folder name per selected file, plus Advanced
Folder merge: one editable destination folder name for the merged folder, plus Advanced
Archive merge: one editable output ZIP path, plus Advanced filename editing
```

The shared advanced name editor is used by file rename, folder wrap, folder
merge, and archive merge without adding a global setting. It edits only one
final name at a time, validates file-name safety, and applies required output
extensions such as `.zip`. Recommended text is derived by tokenizing the
current original name only, and automatic correction applies obfuscated Hangul
restoration candidates before falling back to the stored automatic name.

After the user confirms the final name, the operation executes immediately. A
separate confirmation prompt is avoided unless the operation needs a destructive
or irreversible decision not already represented in the review surface.

## Folder Merge

Selected-target folder merge moves two or more selected files and folders into one generated folder. It is an immediate target-list command rather than a per-target work-plan step, because it operates across multiple targets at once.

Current behavior:

```text
Target folder template: {CommonStem}
Target folder collision: AutoNumber
Child file/folder collision: AutoNumber
Folder layout: PreserveSourceFolder
```

Files are moved directly into the generated folder. Folders are moved as named child folders, preserving their original structure. Selected targets with planned steps are not merged until those steps are cleared, because moving them would invalidate their per-target plan.

## Archive Merge

Archive merge combines two or more selected archive files into one new ZIP archive. The original archives are never modified while the output is being written.

The implementation uses an archive abstraction so merge policy, progress UI, and plan integration do not depend on one concrete library:

```text
IArchiveReader -> SharpCompress ZIP reader
IArchiveWriter -> ZIP writer with SharpZipLib CRC/deflate support
```

The first implementation uses SharpCompress for reading because it supports ZIP filename encodings and exposes normalized entry metadata such as creation, modification, access time, size, attributes, and comments through a common archive API. The ZIP writer writes UTF-8 entry names, external attributes, comments, compression level, and ZIP extra fields directly while still using SharpZipLib for CRC and deflate primitives.

Output is always ZIP in the first implementation. Metadata preservation keeps the normalized metadata the reader exposes and, when the source ZIP central directory can be parsed, copies each entry's local-header and central-directory extra field bytes back to the corresponding output entry byte-for-byte. If raw extra fields cannot be read safely, FileTools falls back to normalized timestamp/attribute/comment metadata.

Reserved default templates:

```text
FolderWrapFolderNameTemplate       = {CorrectedFileStem}
MultiFileMergeFolderNameTemplate   = {CommonStem}
MultiFolderMergeFolderNameTemplate = {CommonStem}
ArchiveMergeFileNameTemplate       = {CommonStem}{TargetExtension}
```

The default output filename uses `ArchiveMergeFileNameTemplate` with
`{TargetExtension}` set to `.zip`. `{CommonStem}` is resolved as a common
logical archive-family name first, so numbered sources such as `A 01.zip` and
`A 02.zip` produce `A 01~02.zip` rather than a raw common prefix like
`A 0.zip`. If `{CommonStem}` cannot produce a useful name, the parent folder
name is used; if that is unavailable, a timestamped
`Merged-yyyyMMdd-HHmmss.zip` fallback is used. When the output filename policy
is `Manual`, the final-name review dialog starts from the suggested path and the
user chooses the final ZIP path before execution.

### Archive Merge Layout

Two layout policies are supported:

```text
GroupByArchiveName
PreserveInternalPaths
```

`GroupByArchiveName` is the default. Each source archive becomes a root folder named from the archive filename, and entries are written under that folder. The root folder name still goes through collision handling because two selected archives from different folders can have the same filename.

`PreserveInternalPaths` writes entries using their original internal paths. It can create many collisions and should only be used with an explicit collision policy.

Archive entry paths are normalized before collision checks. Normalization rejects or rewrites absolute paths, drive-qualified paths, `..` traversal, empty segments, invalid Windows names, reserved device names, repeated separators, and case-only collisions on Windows. Long paths are shown in UI with ellipsis but full paths remain available in tooltips.

Empty directory entries are preserved by default.

### Archive Merge Collision and Duplicate Policy

Internal path collision and duplicate content are separate decisions:

```text
Internal path collision: AutoNumber, SameContentKeepFirst, Ask, Abort
Duplicate content: KeepBoth, SameContentKeepFirst, Ask
```

`AutoNumber` is the default for internal path collisions that remain after the selected layout has been applied. It uses the existing conflict template, such as `{Stem} ({Index}){Extension}`.

`KeepBoth` is the default duplicate-content policy. Automatic duplicate elimination and duplicate-content questions use content hash and keep the first item in selected archive order when the user chooses to skip the duplicate.

The `Ask` policies use a shared decision container in the archive-merge progress window and in the main window execution area. Name-collision questions offer these choices: auto-number and keep the current entry, skip the current entry, or abort the merge. Duplicate-content questions offer these choices: keep both, skip the current duplicate, or abort the merge.

### Archive Merge Failure Policy

Failures are classified by where they occur:

```text
ArchiveOpenFailure    -> central directory/header cannot be interpreted or the format is unsupported
EntryReadFailure      -> the archive opens, but one entry cannot be read or validated
OutputWriteFailure    -> temp ZIP creation, copying, disk space, permission, or final rename fails
```

Failure policy:

```text
AbortAll
SkipFailedArchive
SkipFailedEntry
```

`AbortAll` is the default. `SkipFailedArchive` can only continue past `ArchiveOpenFailure` or archive-level errors. `SkipFailedEntry` can keep remaining entries when a single entry fails, but the result must be reported as partial.

`OutputWriteFailure` is never a partial-success condition. The temp output is deleted and originals are left untouched.

### Archive Merge Automated Regression Tests

`tests\FileTools.Tests` contains the automated regression suite for managed FileTools engines. The archive merge coverage includes ZIP metadata preservation for written entries, byte-for-byte local and central extra field preservation, unreadable ZIP handling with `SkipFailedArchive`, per-entry read failure handling with `SkipFailedEntry`, output parent path failures, and temp ZIP cleanup after final move failures. The same test project also covers filename correction, name templates, collision resolution, rename apply, folder merge, AutoRelocation planning, work-plan previews, and settings/rule normalization. Deterministic ZIP output filesystem failures are injected through the internal `IArchiveMergeFileSystem` adapter.

Run only the managed app/test path with:

```powershell
dotnet test .\tests\FileTools.Tests\FileTools.Tests.csproj
```

### Archive Merge Encoding

ZIP names with the UTF-8 flag are read as UTF-8. Legacy ZIP names are opened with candidate encodings and scored by filename quality, valid path segments, suspicious replacement characters, extension patterns, and collision amplification.

The encoding picker shows both a localized display name and a short explanation:

```text
Korean UI: 한국어 (EUC-KR / CP949) - 한국어 Windows ZIP 파일에서 흔한 레거시 파일명 인코딩입니다.
English UI: Korean (EUC-KR / CP949) - Common legacy filename encoding for Korean Windows ZIP files.
Other candidates: Japanese, Simplified Chinese, Traditional Chinese, ZIP default, UTF-8, System default
```

If the score is ambiguous, the progress window opens a dedicated encoding selection dialog and shows a preview of decoded entry names.

When every candidate receives the same score, FileTools uses the Windows system
default encoding instead of choosing the first candidate. This avoids treating a
score tie on plain ASCII filenames as a Korean/Japanese/Chinese encoding signal.

### Archive Merge Rollback and Source Deletion

The output ZIP is written to a hidden temp file in the final output directory:

```text
.FileTools.Merge.tmp-{guid}.zip
```

Only after all required entries are written and the new archive can be reopened for verification is the temp file moved to the final filename. If the final path exists, the normal output file collision policy applies before writing starts.

Source deletion is an optional post-success step and is off by default. It runs only after the final ZIP exists and has been verified. With partial-success policies, only fully merged source archives are eligible for deletion. When `SkipFailedEntry` creates a partial result, source deletion should stay disabled unless the user explicitly confirms the risk.

### Archive Merge UI

Settings provide default archive merge policy:

```text
Default layout
Collision policy
Duplicate content policy
Failure policy
Output filename policy/template
Delete originals after verified success
Context menu entry visibility for both layout styles
ZIP compression level: Store only, Fast, Default, Maximum
```

The context menu exposes two archive merge entry points:

```text
Merge ZIPs: group by ZIP name
Merge ZIPs: preserve internal folders
```

The main window stores archive merge as a shared plan step. Every participating target shows the same shared row, but execution deduplicates by plan ID so the merge runs once. Double-clicking the row opens an archive merge options dialog for output path and policy changes. That dialog also scans entries and shows a detailed internal file list with original entry paths, final target entry paths, and collision-renamed targets before execution.

The progress window reports these phases:

```text
Validate sources
Detect filename encoding
Scan entries
Resolve collisions and duplicates
Write temp ZIP
Verify output ZIP
Move to final filename
Delete originals
```

When an `Ask` policy produces a question, the active execution UI adds it to a pending-decision list. Selecting a question shows the existing entry, current entry, target path, source archive, and size. Answering a question removes it from the list and unblocks that merge point. In the main window, the execution log area expands while pending decisions exist and collapses again after they are answered or canceled.

## Future Merge Operations

Common-filename-based archive merge is tracked by issue #9 and documented in
`docs/common-file-merge-design.md`. Its primary scenario is merging archives
such as `A 01.zip` and `A 02.zip` into `A 01~02.zip`, not renaming every
selected file to the same base name. It reuses the archive merge output template
`{CommonStem}{TargetExtension}` with the shared merge-name analyzer described
above. General file-content merge, such as
`b.txt + b02.txt -> b.txt`, remains a deferred operation because it needs
duplicate or overlapping content policy.

Multiple-folder merge will need a separate merge layout policy:

```text
PreserveSourceFolder
GroupBySourceName
Flatten
```

`PreserveSourceFolder` is the safest default because it avoids structure loss and reduces internal name collisions. `Flatten` should stay explicit because it can create many child conflicts.
