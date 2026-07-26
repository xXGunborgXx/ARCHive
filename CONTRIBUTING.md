# Contributing to ARCHive

Thank you for helping make ARCHive safer and easier to use.

## Before Opening an Issue

- Search existing issues first.
- Use the bug template for reproducible faults.
- Use the feature template for focused suggestions.
- Remove personal paths, names, and other private information from screenshots,
  videos, and diagnostic JSON.
- Keep database backup, disk imaging, cloning, cloud backup, and scheduling
  outside the current project scope.

## Development Setup

ARCHive requires Windows 11 x64, the .NET 10 SDK, PowerShell, Git, and Inno
Setup 6.

```powershell
git clone https://github.com/xXGunborgXx/ARCHive.git
Set-Location ARCHive
dotnet restore .\ARCHive.sln
dotnet test .\ARCHive.sln --configuration Release
```

Run the complete publish and installer pipeline with:

```powershell
.\scripts\Build-Preview.ps1
```

## Pull Requests

1. Fork the repository and create a focused branch.
2. Keep changes limited to one bug, feature, or documentation improvement.
3. Add or update tests for behavior changes.
4. Run the full test suite before submitting.
5. Explain the user impact, integrity considerations, and validation performed.

Changes affecting cancellation, cleanup, pause/resume, archive path handling,
trial behavior, or diagnostic records need explicit integrity and privacy
tests.

Do not commit generated installers, publish output, test videos, diagnostic
logs, secrets, signing certificates, or real user data.

By submitting a contribution, you agree that it may be distributed under the
repository's MIT License.
