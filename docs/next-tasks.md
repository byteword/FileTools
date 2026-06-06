# Next Tasks

Review date: 2026-06-06

Scope reviewed:

- Local commits after `v1.2.0.0`, through `6cc59e1 Preserve ZIP extra fields during archive merge`.
- GitHub issues #1 through #7 in `byteword/FileTools`.
- README and `docs/name-template-and-collision-policy.md`.

## GitHub Issue Status

- #1 app-level rename common phrases: closed as completed before this review.
- #2 app-level rename correction rule management: closed as completed during this review. The rule model, built-in/user rules, review trace, and script-rule constraints are now implemented and documented.
- #3 internet dictionary or AI-assisted rename correction: keep open as a long-term research item.
- #4 merge multiple files into one folder: keep open. Selected-target folder merge is implemented, but common-filename-based file merge and preview scope still need a product decision.
- #5 merge multiple archives into one archive: keep open. ZIP merge is implemented and documented; 7Z support, real-sample validation, and release-scope wording remain.
- #6 compare two or more files: keep open as the next large standalone feature candidate after archive merge stabilization.
- #7 Windows ARM64 build and installer support: keep open and deferred until there is ARM64 Windows hardware or VM validation.

## Next Priority

1. Stabilize archive merge ZIP output.
   - Run the managed regression suite after the current extra-field writer changes.
   - Validate with real ZIP samples that include legacy filename encodings, comments, directory entries, external attributes, and local/central extra fields.
   - Verify cancellation, temp-file cleanup, and final move failure behavior with large archives.
   - Decide whether ZIP32 limits are acceptable for the first release or whether Zip64 output support must be added before release.

2. Close or split archive merge issue #5.
   - If the first supported scope is ZIP-only, update #5 title/body to say ZIP archive merge and create a separate 7Z follow-up.
   - If 7Z stays in #5, define the reader, metadata, encoding, entry failure, and test requirements before implementation.
   - Prepare release notes that explicitly say ZIP output is supported and 7Z input is not yet supported.

3. Resolve folder merge issue #4 scope.
   - Decide whether selected-target folder merge satisfies the issue enough to close it.
   - If common-filename-based file merge is still desired, split it into a narrower follow-up issue with rename/extension/collision preview requirements.

4. Keep release verification honest.
   - Build and test the managed project with `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj`.
   - Build the full solution from Visual Studio MSBuild, not `dotnet build FileTools.sln`, because the native C++ ShellExt requires Visual C++ targets.
   - Update release notes and external wiki only after the release artifacts and checksums are verified.

5. Defer lower-priority feature tracks.
   - File comparison (#6) should wait until archive merge release risk is lower.
   - ARM64 packaging (#7) should wait for actual ARM64 Windows validation.
   - Internet dictionary or AI-assisted rename correction (#3) should stay research-only until privacy, cost, and review UX are defined.
