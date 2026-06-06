# Next Tasks

Review date: 2026-06-06
Last updated: 2026-06-06 after GitHub issue split

Scope reviewed:

- Local commits after `v1.2.0.0`, through `7267c8d Document next FileTools tasks`.
- GitHub issues #1 through #9 in `byteword/FileTools`.
- README, `docs/name-template-and-collision-policy.md`, `docs/release.md`, and `docs/release-notes/next.md`.

## GitHub Issue Status

- #1 app-level rename common phrases: closed as completed before this review.
- #2 app-level rename correction rule management: closed as completed during this review. The rule model, built-in/user rules, review trace, and script-rule constraints are now implemented and documented.
- #3 internet dictionary or AI-assisted rename correction: keep open as a long-term research item.
- #4 selected-target folder merge: closed as completed. Common-filename-based file merge and preview scope were split to #9.
- #5 ZIP archive merge: closed as completed. 7Z input archive merge was split to #8.
- #6 compare two or more files: keep open as the next large standalone feature candidate after archive merge stabilization.
- #7 Windows ARM64 build and installer support: keep open and deferred until there is ARM64 Windows hardware or VM validation.
- #8 7Z input archive merge support: keep open as the archive merge follow-up.
- #9 common-filename-based file merge flow: keep open as the folder/file merge follow-up.

## Completed Follow-Up

- Split 7Z input archive merge from #5 into #8 and closed #5 as ZIP input/ZIP output archive merge.
- Split common-filename-based file merge from #4 into #9 and closed #4 as selected-target folder merge.
- Added `docs/release-notes/next.md` as the working release note draft for the archive merge scope.

## Next Priority

1. Stabilize ZIP archive merge release readiness.
   - Run the managed regression suite after the current extra-field writer changes.
   - Validate with real ZIP samples that include legacy filename encodings, comments, directory entries, external attributes, and local/central extra fields.
   - Verify cancellation, temp-file cleanup, and final move failure behavior with large archives.
   - Decide whether ZIP32 limits are acceptable for the first release or whether Zip64 output support must be added before release.

2. Finalize the next release notes.
   - Copy `docs/release-notes/next.md` to `docs/release-notes/<tag>.md` after the next version tag is chosen.
   - Keep the archive merge support note explicit: ZIP input and ZIP output are supported; 7Z input is not yet supported and is tracked by #8.
   - Keep #9 out of the release notes until the common-filename-based file merge flow is actually implemented.

3. Keep release verification honest.
   - Build and test the managed project with `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`.
   - Build the full solution from Visual Studio MSBuild, not `dotnet build FileTools.sln`, because the native C++ ShellExt requires Visual C++ targets.
   - Update release notes and external wiki only after the release artifacts and checksums are verified.

4. Defer lower-priority feature tracks.
   - 7Z input archive merge (#8) should wait until ZIP archive merge release risk is lower.
   - Common-filename-based file merge (#9) should wait until the rename, extension, collision, and preview UX is designed.
   - File comparison (#6) should wait until archive merge release risk is lower.
   - ARM64 packaging (#7) should wait for actual ARM64 Windows validation.
   - Internet dictionary or AI-assisted rename correction (#3) should stay research-only until privacy, cost, and review UX are defined.
