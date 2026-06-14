# Release Readiness Review - 2026-06-14

This review was done before the planned `v1.3.0.0` beta/version-up release.

## Verification Run

- Working tree was clean before the review.
- Open GitHub issues matched `docs/next-tasks.md`: #3, #6, #7, #8, and #9 remain open.
- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` passed 99/99 tests.
- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release` passed 99/99 tests.
- `MSBuild.exe FileTools.sln /p:Configuration=Release /p:Platform=x64` passed after sandbox escalation for Windows SDK lookup.
- The full solution build currently reports one xUnit analyzer warning in `tests\FileTools.Tests\WorkPlanDisplayBuilderTests.cs`.

## Release Blockers And Follow-Up

1. Complete manual ZIP sample validation and install smoke testing before publishing the draft release.
2. Re-run the final release build and managed tests after the fix pass.

## Fixed In Follow-Up

- Added the current permission set required by `actions/attest@v4`, including `artifact-metadata: write`.
- Updated the existing-release branch in `.github/workflows/release.yml` to refresh release notes, title, draft state, and prerelease state after replacing assets.
- Updated release-facing notes from the 2026-06-07 62-test pass to the 2026-06-14 99-test pass.
- Removed the xUnit analyzer warning in `tests\FileTools.Tests\WorkPlanDisplayBuilderTests.cs`.
- Added `global.json` to pin SDK selection to .NET SDK 8.0.422 with feature-band roll-forward.

## Post-Fix Verification

- `dotnet --version` resolves to 8.0.422 from `global.json`.
- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj` passed 99/99 tests.
- `dotnet test tests\FileTools.Tests\FileTools.Tests.csproj -c Release` passed 99/99 tests.
- `MSBuild.exe FileTools.sln /p:Configuration=Release /p:Platform=x64` passed with 0 warnings and 0 errors after sandbox escalation for Windows SDK lookup.
- `build_msi.ps1 -Version v1.3.0.0` passed with 0 warnings and 0 errors after sandbox escalation for Windows SDK signing and certificate-store access.
