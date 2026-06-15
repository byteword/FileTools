# FileTools Next Release Notes Draft

This draft is for changes after `v1.4.3.0`. Add future changes here during
development, then copy or regenerate it into `docs/release-notes/<tag>.md` for
the next release tag.

## Highlights

- Fixed ZIP archive merge launched from Explorer context menus so the progress
  dialog no longer fails during initial layout when the splitter is created
  before the dialog width is established.
- MainForm target and work-plan grids now keep fixed/user column widths and use
  horizontal scrolling instead of proportional width recomputation.
- Added a small/medium/large setting for the top-right task toolbar, mapping the
  existing icon size to 1x, 2x, and 4x.
- The native ShellExt now embeds the same file/product version as the app and
  `build_msi.ps1` signs the ShellExt DLL before it is packaged.
- Bumped the beta release line to `1.4.4.0`.

## Support Scope

- No runtime support scope change yet. ZIP input and ZIP output remain the
  supported archive merge scope; 7Z input remains deferred under issue #8.

## Verification Before Publishing

- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`.
- Run `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release`.
- Build `FileTools.sln` with Visual Studio MSBuild in `Release|x64`.
- Build or dry-run `build_msi.ps1 -Version <tag>` before tagging to confirm the
  release tag is accepted and propagated into the installer build.
- Confirm `src\FileTools.ShellExt\x64\Release\FileTools.ShellExt.dll` has the
  expected file/product version and an Authenticode signature after the
  installer build.
- Verify release assets, checksums, signatures, and GitHub artifact attestations
  before publishing the draft release.

Latest local verification:

- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` passed 116/116 on
  2026-06-15.
- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release` passed
  116/116 on 2026-06-15.
- `MSBuild.exe FileTools.sln /p:Configuration=Release /p:Platform=x64` passed
  with 0 warnings and 0 errors on 2026-06-15.
- `.\build_msi.ps1 -Version v1.4.4.0` completed on 2026-06-15. The generated
  `FileTools.ShellExt.dll` reports file/product version `1.4.4.0` and has an
  Authenticode signature from the temporary `CN=FileTools Self-Signed`
  certificate. Local trust still reports an untrusted-root status until the CER
  is trusted.
