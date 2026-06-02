# FileTools

Windows Explorer ContextMenu and standalone WinForms utility for small file-management operations.

Current version: `1.0.1.2`.

## Features

FileTools provides three current-user ContextMenu actions for selected files and folders:

1. **파일이름 자동 교정**
   - Uses the filename correction flow derived from `NameCorrector`.
   - Normalizes Korean jamo/Unicode, extracts title/episode/tag/author parts, makes Windows-safe names, and avoids conflicts with suffixes.
   - The rename review dialog opens before applying changes, including ContextMenu execution.

2. **폴더 wrapping / unwrapping**
   - In automatic mode, selected files are wrapped into same-stem folders.
   - Selected folders are unwrapped when they are single-file folders, otherwise direct child files are moved up.
   - Single-file folder unwrapping can keep the original filename, rename to the folder name, or rename to `folder-file`.
   - Existing destination files are not overwritten.

3. **폴더 자동 재배치**
   - Uses lightweight AutoRelocation templates derived from `ImageArchiveManager`.
   - Default template moves items into title-initial buckets such as `[ㄱ]`, `[A]`, and `[0A]`.
   - Templates can build multi-level paths by chaining ordered path-rule steps.
   - Template fields are limited to values available from the file, folder, or parsed file name.

The native ShellExt only exposes Explorer menu commands and launches the executable. The executable queues selected items briefly, merges Explorer's per-item invocations, performs non-interactive work automatically, and exits silently when there are no errors.
The non-processing **FileTools 열기 / Open FileTools** command stays in the FileTools submenu and opens the standalone planner with the selected items loaded.

## Standalone UI

Run `FileTools.exe` without arguments to open the drag-and-drop work plan window.

![FileTools standalone window](docs/images/current-mainform-designer-layout.svg)

The standalone window supports:

- Drag and drop files/folders into the target list.
- Reviewing targets in a grid with file/folder icons, parent locations, and per-target action counts.
- Using the target toolbar to add/remove targets and move selected targets up or down in execution order.
- Dropped or newly added targets are selected automatically. Action buttons add the configured step to every selected target, so multi-folder unwrap workflows can be prepared in one pass.
- Manual file/folder selection.
- Adding multiple planned actions to each target before changing files.
- Chaining filename correction, folder wrapping, folder unwrapping, and AutoRelocation actions.
- Accessing file, task, and settings commands from the menu bar, while common task commands stay on the fixed task toolbar.
- Selecting folder unwrapping variants from a split button, including the default setting, same-name folders, single-file folder name mismatch modes, and moving direct child files upward.
- Reviewing each selected target's work plan in a grid with order, icon-labeled action kind, and expected result.
- Showing detailed per-step options in grid row tooltips instead of dedicating a separate settings column.
- Removing one selected step or clearing the currently displayed target's steps from the plan-side toolbar; the preview is recalculated from the remaining step chain.
- Double-clicking a planned action to reopen the matching action dialog; rename steps reopen the rename review dialog.
- Running all target plans in order with one bottom-right run/stop button and reviewing progress in the bottom log view.
- Opening a separate tabbed settings window for defaults, rename options, AutoRelocation defaults, folder options, and Explorer ContextMenu registration.

The settings window owns operational defaults and Explorer ContextMenu installation/removal. Native ShellExt registration uses one FileTools submenu, and individual ContextMenu actions can be enabled or disabled.
Folder wrapping/unwrapping and AutoRelocation commands can be selected independently for Explorer registration. Pressing OK in the settings window saves the options and synchronizes the current-user ContextMenu registration, even if the Install/Remove buttons are not pressed.
The app icon is stored as transparent PNG and multi-size ICO assets under `src\FileTools.App\Resources`; the EXE and MSI product metadata both use the ICO.

The rename review dialog is used by ContextMenu rename commands and by standalone plan editing.

![FileTools rename dialog](docs/images/filetools-rename-dialog.svg)

Separate dialogs are available for:

- Rename replacement dictionary entries (`source -> replacement`).
- Rename common phrase dictionary entries used by the filename correction scorer.
- AutoRelocation template editing. Path rule steps are evaluated in order, so a template can produce paths such as `{KnownFileKind}\[{Initial}]\{EpisodeRange}`.

AutoRelocation templates intentionally use only file-derived values:

- File name stem.
- File extension.
- Known file kind from common extensions: `Folder`, `Archive`, `Image`, `Video`, `Music`, `Text`, `Document`, `Program`, `Other`.
- Parsed title and episode range from the file or folder name.
- Size, created time, and modified time.

The known file kind source is separate from the raw extension source. It groups common extensions into broad folders:

- `Archive`: compressed/archive and disk-image style files such as `zip`, `rar`, `7z`, `tar`, `gz`, `cbz`, `cbr`, `iso`.
- `Image`: image/design/raw formats such as `jpg`, `png`, `gif`, `webp`, `heic`, `svg`, `psd`, `ico`.
- `Video`: video files and subtitle sidecars such as `mp4`, `mkv`, `avi`, `mov`, `webm`, `srt`, `ass`, `vtt`.
- `Music`: audio/music files such as `mp3`, `flac`, `wav`, `m4a`, `ogg`, `opus`, `wma`.
- `Text`: plain text and structured text such as `txt`, `md`, `log`, `csv`, `json`, `xml`, `yaml`, `ini`.
- `Document`: PDF, Office, OpenDocument, ebook, and HWP formats such as `pdf`, `docx`, `xlsx`, `pptx`, `odt`, `epub`, `hwp`, `hwpx`.
- `Program`: executable, installer, script, package, and library files such as `exe`, `msi`, `bat`, `ps1`, `js`, `jar`, `dll`, `apk`.

Settings and templates are stored under:

```text
%APPDATA%\FileTools
%APPDATA%\FileTools\rename-dictionary.json
%APPDATA%\FileTools\Relocate
```

If `%APPDATA%` is not writable, FileTools falls back to `FileToolsData` next to the executable.

## UI Localization

The app UI follows the system UI culture through .NET `CurrentUICulture`.
English is the neutral/default resource, and Korean is provided as a satellite resource.
Unsupported UI cultures fall back to English.

```text
src\FileTools.App\Resources\Strings.resx
src\FileTools.App\Resources\Strings.ko.resx
```

`MainForm` is split into a WinForms Designer-friendly partial class:

```text
src\FileTools.App\Ui\MainForm.cs
src\FileTools.App\Ui\MainForm.Designer.cs
src\FileTools.App\Ui\MainForm.resx
```

Keep layout/control declarations in `MainForm.Designer.cs`, and keep runtime behavior and localized text binding in `MainForm.cs`.
Form-level culture resources such as `Ui\MainForm.ko.resx` are intentionally excluded from the build; add UI strings only to `Resources\Strings*.resx`.
The Designer file keeps neutral English text and placeholder combo items so Visual Studio can render the form without running runtime localization; the app overwrites those values from `Resources\Strings*.resx` at startup.

## Build Requirement

- Windows
- .NET 8 SDK or newer
- Visual Studio Build Tools with the C++ workload for `FileTools.ShellExt`

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

The MSI installer uses WiX Toolset SDK-style project files. The build script first builds the native ShellExt DLL with Visual Studio MSBuild, then restores/builds the WiX SDK project.

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
- `Explorer Context Menu`: optional native ShellExt registration.

The MSI installs the native `FileTools.ShellExt.dll` as a current-user COM ExplorerCommand handler. After first launch, use FileTools settings to choose individual folder wrapping/unwrapping and AutoRelocation commands. Legacy static registry components are kept disabled for fallback development only.

Use `dotnet build src\FileTools.App\FileTools.App.csproj` for an app-only build. `FileTools.sln` includes the native ShellExt project, so building the full solution requires Visual Studio MSBuild with the C++ workload. The ShellExt project is built by `build_msi.ps1` and `publish_and_install.ps1`. The MSI project is isolated in `installer\FileTools.Installer.sln`; build it with `build_msi.ps1` or open that solution in Visual Studio with a WiX v4-compatible extension such as HeatWave.

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

src\FileTools.ShellExt
└─ Native C++ ExplorerCommand shell extension

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

The explicit `/install` command enables `RegisterContextMenu` even if the saved settings currently have Explorer registration turned off.

This writes only to current-user registry keys:

```text
HKCU\Software\Classes\*\shell
HKCU\Software\Classes\Directory\shell
HKCU\Software\Classes\CLSID\{716e7cc4-5941-4362-8aca-d38c62817de9}
HKCU\Software\FileTools\ContextMenu
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

The explicit `/uninstall` command removes the Explorer registration and saves `RegisterContextMenu` as disabled.

## Clean ContextMenu Registration

If Explorer still does not show the FileTools menu after install, inspect and clean current-user registration leftovers:

```powershell
.\cleanup_context_menu.ps1 -WhatIf
```

Run the cleanup:

```powershell
.\cleanup_context_menu.ps1
```

Optional flags:

- `-RemoveInstalledFiles`: also removes `%APPDATA%\FileTools`, including copied binaries, settings, and templates.
- `-RestartExplorer`: restarts Explorer after cleanup.

## ContextMenu Behavior

Registered commands:

```text
FileTools.exe /open "%1"
FileTools.exe /context FileNameCorrection "%1"
FileTools.exe /context FolderStructure "%1"
FileTools.exe /context AutoRelocation "%1"
FileTools.exe /context FolderWrapFiles "%1"
FileTools.exe /context FolderUnwrapSameNameSingleFile "%1"
FileTools.exe /context FolderUnwrapSingleFile "%1"
FileTools.exe /context FolderUnwrapUseFolderName "%1"
FileTools.exe /context FolderUnwrapKeepFileName "%1"
FileTools.exe /context FolderMoveInnerFilesUp "%1"
FileTools.exe /context AutoRelocationCurrentFolder "%1"
FileTools.exe /context AutoRelocationChooseTarget "%1"
```

The first three `/context` commands are kept for backward compatibility. Native ShellExt decides which submenu items are visible from the selected item type. For single-file folders, it also checks whether the single file stem matches the folder name and exposes either the simple unwrap command or explicit folder-name/file-name unwrap commands.

Explorer often starts one process per selected item. FileTools waits briefly, merges those selected paths through a temporary queue, runs the selected operation, and exits automatically for non-interactive commands. File name correction opens the rename review dialog before applying changes. If any exception occurs, an error summary is shown.

## Safety Behavior

- Existing destination files/folders are not overwritten.
- Filename correction is reviewed in a dialog before applying changes.
- AutoRelocation applies `(2)`, `(3)` suffixes when a target already exists.
- Folders are deleted only when empty after unwrapping/moving child files.
- Folder unwrapping only moves direct child files; nested folder contents are not flattened.

## Log

```text
%TEMP%\FileTools.log
```

## License

FileTools is licensed under the MIT License. See `LICENSE`.
