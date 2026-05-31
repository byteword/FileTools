# FileTools

Windows Explorer ContextMenu and standalone WinForms utility for small file-management operations.

## Features

FileTools provides three current-user ContextMenu actions for selected files and folders:

1. **파일이름 자동 교정**
   - Uses the filename correction flow derived from `NameCorrector`.
   - Normalizes Korean jamo/Unicode, extracts title/episode/tag/author parts, makes Windows-safe names, and avoids conflicts with suffixes.
   - Items that require human review are skipped during automatic ContextMenu execution.

2. **폴더 wrapping / unwrapping**
   - In automatic mode, selected files are wrapped into same-stem folders.
   - Selected folders are unwrapped when they are single-file folders, otherwise direct child files are moved up.
   - Existing destination files are not overwritten.

3. **폴더 자동 재배치**
   - Uses lightweight AutoRelocation templates derived from `ImageArchiveManager`.
   - Default template moves items into title-initial buckets such as `[ㄱ]`, `[A]`, and `[0A]`.
   - Template prefilters can skip review-only items during automatic execution.

The Explorer command only starts the executable. It queues selected items briefly, merges Explorer's per-item invocations, performs the work automatically, and exits silently when there are no errors.

## Standalone UI

Run `FileTools.exe` without arguments to open the settings and drag-and-drop window.

![FileTools standalone window](docs/images/filetools-main-window.svg)

The standalone window supports:

- Drag and drop files/folders into the target list.
- Manual file/folder selection.
- Selecting one of the three tools and running it directly.
- Changing the folder wrapping/unwrapping mode.
- Creating, editing, saving, and deleting AutoRelocation templates.
- Installing or removing Explorer ContextMenu entries.

Settings and templates are stored under:

```text
%APPDATA%\FileTools
%APPDATA%\FileTools\Relocate
```

If `%APPDATA%` is not writable, FileTools falls back to `FileToolsData` next to the executable.

## Build Requirement

- Windows
- .NET 8 SDK or newer

## Build

```powershell
dotnet build FileTools.sln
```

Publish:

```powershell
dotnet publish .\src\FileTools.App\FileTools.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output:

```text
src\FileTools.App\bin\Release\net8.0-windows\win-x64\publish\FileTools.exe
```

## Build MSI

The MSI installer uses WiX Toolset SDK-style project files. The first build restores the WiX SDK and UI extension packages.

```powershell
.\build_msi.ps1
```

Output:

```text
installer\FileTools.Installer\bin\Release\FileTools.msi
```

The MSI publishes FileTools as a self-contained `win-x64` single-file app and installs it per-user under:

```text
%LOCALAPPDATA%\Programs\FileTools
```

MSI options:

- `FileTools`: application and Start Menu shortcut.
- `Explorer Context Menu`: optional ContextMenu registration.
- `Grouped Context Menu`: default. Shows one `FileTools` menu with subcommands.
- `Expanded Context Menu`: shows `FileTools 열기` and all tool commands directly.

The grouped menu is the default. In the feature selection page, select `Expanded Context Menu` to install the direct entries instead. If both grouped and expanded features are selected, the installer conditions prefer expanded entries.

`FileTools.sln` intentionally contains only the app project so Visual Studio can load it without WiX tooling. The MSI project is isolated in `installer\FileTools.Installer.sln`; build it with `build_msi.ps1` or open that solution in Visual Studio with a WiX v4-compatible extension such as HeatWave.

## Project Layout

```text
src\FileTools.App
├─ Configuration
├─ Infrastructure
├─ Naming
├─ Operations
├─ Relocation
├─ Shell
└─ Ui

installer\FileTools.Installer
├─ FileTools.Installer.sln
└─ FileTools.Installer
```

## Install ContextMenu

Use the helper script:

```powershell
.\publish_and_install.ps1
```

Or run the published executable:

```powershell
.\FileTools.exe /install
```

This writes only to current-user registry keys:

```text
HKCU\Software\Classes\*\shell
HKCU\Software\Classes\Directory\shell
```

No administrator permission is required.

## Uninstall ContextMenu

```powershell
.\uninstall.ps1
```

Or:

```powershell
.\FileTools.exe /uninstall
```

## ContextMenu Behavior

Registered commands:

```text
FileTools.exe /open "%1"
FileTools.exe /context FileNameCorrection "%1"
FileTools.exe /context FolderStructure "%1"
FileTools.exe /context AutoRelocation "%1"
```

Explorer often starts one process per selected item. FileTools waits briefly, merges those selected paths through a temporary queue, runs the selected operation, and exits automatically. If any exception occurs, an error summary is shown.

## Safety Behavior

- Existing destination files/folders are not overwritten.
- Automatic filename correction skips items marked as requiring review.
- AutoRelocation applies `(2)`, `(3)` suffixes when a target already exists.
- Folders are deleted only when empty after unwrapping/moving child files.
- Folder unwrapping only moves direct child files; nested folder contents are not flattened.

## Log

```text
%TEMP%\FileTools.log
```
