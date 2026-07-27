# ARCHive

[![Windows build](https://github.com/xXGunborgXx/ARCHive/actions/workflows/build.yml/badge.svg)](https://github.com/xXGunborgXx/ARCHive/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-amber.svg)](LICENSE)

ARCHive is a compact Windows 11 desktop utility for ordinary users who need to
copy files and folders, create 7z or ZIP archives, or extract archives without
learning command-line tools.

The current public build is `1.1.0-beta2`, a seven-day testing release.

## Download the Beta

Download the installer, instructions, disclosures, and editable questionnaire
from the
[v1.1.0-beta2 release](https://github.com/xXGunborgXx/ARCHive/releases/tag/v1.1.0-beta2).

The installer is not code-signed. Windows may display an **Unknown publisher**
or SmartScreen warning. Do not bypass organizational security policy.

## Current Features

- Copy one or many files and folders in a single dated job.
- Create verified 7z and ZIP archives from mixed selections.
- Extract one 7z or ZIP archive into a dated destination.
- Pause and resume eligible multi-file copies between files.
- Clean up incomplete output conservatively after cancellation.
- Show measured percentage, byte progress, and transfer/processing speed.
- Explain storage stalls with a `waiting for storage` status.
- Verify copy totals and archives before reporting success.
- Reject unsafe archive paths, links, and unsupported passwords.
- Open the completed destination or local diagnostic record directly.
- Keep structured diagnostic records locally with 30-day retention.
- Run from a compact, fixed-size, native WPF interface.

Database backup, disk imaging, cloning, mirroring, source deletion, scheduling,
cloud accounts, telemetry, and automatic updates are intentionally outside the
version 1 scope.

## Seven-Day Beta

The testing period begins on first launch and lasts seven consecutive days.
ARCHive checks the trial only at startup, so an operation already running is
never interrupted by expiry.

The encrypted local trial record is stored under
`%LOCALAPPDATA%\ARCHive\Beta`. It is not uploaded. Reinstalling does not restart
the trial, and a significant Windows clock rollback locks the beta.

See [INSTALLATION_DISCLOSURE.txt](INSTALLATION_DISCLOSURE.txt) and
[LOGGING_AND_PRIVACY.md](LOGGING_AND_PRIVACY.md) before testing.

## Build from Source

Requirements:

- Windows 11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php)
- PowerShell
- Git

Clone and build:

```powershell
git clone https://github.com/xXGunborgXx/ARCHive.git
Set-Location ARCHive
.\scripts\Build-Preview.ps1
```

The script restores dependencies, builds the solution, runs unit and
integration tests, publishes a self-contained `win-x64` application, and
compiles the installer.

Outputs are written to:

```text
artifacts\publish\win-x64\
installer\output\ARCHive-Beta-1.1.0-beta2-7day-Setup.exe
```

The required 7-Zip runtime components and their license files are retained in
`third_party\7zip\26.02\runtime`.

## Run Tests

```powershell
dotnet test .\ARCHive.sln --configuration Release
```

The beta baseline contains 41 unit tests and 27 integration tests.

For real throughput testing, use the physically written test-data scripts:

```powershell
.\scripts\New-ARCHiveCopyTestData.ps1 `
    -OutputDirectory D:\ARCHive-Test-Source `
    -FileSizeGB 8,39

.\scripts\Test-ARCHiveCopyTestData.ps1 `
    -TestDirectory 'E:\ARCHive-Test-Output'
```

Use disposable data or keep another verified copy of every important file.

## Contribute

Bug reports, usability feedback, documentation corrections, and focused pull
requests are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md).

- Use GitHub Issues for reproducible bugs and feature suggestions.
- Do not post diagnostic JSON without reviewing it; logs can contain full local
  file and folder paths.
- Keep database backup proposals separate from this repository.
- Report security or data-integrity concerns privately as described in
  [SECURITY.md](SECURITY.md).

Beta testers may also complete
`ARCHive_Beta_Test_Questionnaire.docx` from the release and email it to
`GunborgServers@gmail.com`.

## License and Third-Party Components

ARCHive source code is available under the [MIT License](LICENSE).
Bundled 7-Zip components retain their own licensing terms; see
[THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).
