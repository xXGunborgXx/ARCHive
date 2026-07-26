# ARCHive Build Preparation Checklist

## 1. Required Development Programs

### Visual Studio Community 2026

Purpose:

- C# and WPF development;
- compilation and debugging;
- unit/integration testing;
- Windows SDK tools.

Install:

- Visual Studio Community 2026, version 18.0 or later;
- **.NET desktop development** workload;
- latest .NET 10 SDK;
- Windows SDK;
- C# and WPF tools;
- .NET test tools.

Community is appropriate for an individual developer. Review the license again
if development later moves into a larger organization.

The development PC must run a supported 64-bit Windows 11 edition. Visual
Studio 2026 does not support ordinary Windows 10 development hosts.

Official source:

<https://visualstudio.microsoft.com/downloads/>

### .NET 10 SDK

Purpose:

- build and publish the self-contained WPF application;
- run `dotnet` restore, build, and test commands.

Visual Studio can install it. Verify that the x64 SDK is available rather than
installing a second unnecessary copy.

Official source:

<https://dotnet.microsoft.com/download/dotnet/10.0>

### Git for Windows

Purpose:

- source control;
- reversible development history;
- release tags;
- comparison and rollback.

Install the maintained x64 release and use a private repository until the
license, branding, and third-party notices are ready.

Official source:

<https://git-scm.com/install/windows.html>

### Official 7-Zip components

Purpose:

- 7z and ZIP creation;
- archive testing;
- extraction;
- optional password-safe library integration.

Preparation:

- pin one stable version for the project;
- obtain the official Extra/source package needed for `7z.dll` and any console
  component;
- verify downloaded hashes/signatures when available;
- store binaries under `third_party/7zip/<version>/`;
- store the corresponding license and attribution;
- document the official source location;
- never depend on a random 7-Zip already installed on the user's PC.

Current planning baseline: 7-Zip 26.02. Reconfirm the stable version before the
first public release.

Official source:

<https://www.7-zip.org/download.html>

### Inno Setup

Purpose:

- produce one Windows installer;
- install/uninstall ARCHive;
- add Start menu shortcuts;
- package third-party notices;
- support Authenticode signing.

Current planning baseline: Inno Setup 6.7.3. Reconfirm the stable version before
release. Commercial users are requested by the publisher to purchase a
commercial license.

Official source:

<https://jrsoftware.org/isinfo.php>

## 2. Built-in Windows Components

These require no separate download:

- Robocopy;
- File Explorer;
- Windows PowerShell;
- standard Windows file/folder dialogs;
- Windows Event Log APIs, if later needed for diagnostics.

ARCHive must locate the trusted Windows Robocopy executable through the Windows
system directory rather than relying on a user-controlled working directory.

## 3. Required Release Item, Not Needed for Early Development

### Authenticode code-signing certificate

Purpose:

- identify the publisher;
- reduce untrusted-publisher warnings;
- detect modified executables and installers.

Preparation:

- choose the legal publisher name;
- acquire a certificate from an appropriate public certificate authority;
- keep the private key outside the repository;
- restrict signing access;
- sign and SHA-256 timestamp the application and installer;
- verify signatures in the release pipeline.

The Windows SDK supplies SignTool. A certificate is not required to begin
coding, but it should be obtained before public distribution.

Microsoft SignTool documentation:

<https://learn.microsoft.com/windows/win32/seccrypto/signtool>

## 4. Optional Testing Programs and Equipment

### Windows Sandbox

Useful for disposable installer smoke tests when the development PC runs a
supported Pro, Enterprise, or Education edition. It is not available on
Windows Home.

Official guidance:

<https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/>

### Clean test machines or virtual machines

Prepare:

- current supported Windows 11 x64 releases;
- a standard non-administrator test account;
- a machine with no separate .NET runtime to prove the self-contained build;
- a machine without 7-Zip installed to prove the bundled dependency works.

### Test storage

Prepare noncritical media and folders for destructive fault simulation:

- NTFS USB drive;
- FAT32 test drive or partition;
- low-space test volume;
- SMB share that can be disconnected safely;
- folders with read-only and denied permissions;
- a deliberately locked test file.

Never use the only copy of real user data for fault testing.

## 5. Product Decisions Required Before Coding

Confirm and record:

- product name: `ARCHive`;
- executable name;
- publisher name;
- application identifier/GUID;
- initial version: suggested `0.1.0`;
- minimum supported Windows 11 release, checked against the current .NET 10
  support matrix;
- x64-only Version 1;
- per-user or all-user installation;
- default installation folder;
- local settings/history/log locations;
- application license;
- public or private source repository;
- support/contact link;
- final icon and visual palette.

Recommended Version 1 security decisions:

- run `asInvoker`, not automatically as administrator;
- no background service;
- no scheduled task;
- no telemetry;
- no automatic update network call;
- no stored passwords;
- no shell context-menu extension;
- no destructive copy mode.

## 6. Repository Preparation

Create:

```text
ARCHive.sln
src/
  ARCHive.App/
  ARCHive.Core/
  ARCHive.Copy/
  ARCHive.Archive/
  ARCHive.Infrastructure/
tests/
  ARCHive.UnitTests/
  ARCHive.IntegrationTests/
  ARCHive.SmokeTests/
fixtures/
installer/
third_party/
  7zip/
docs/
artifacts/
```

Repository rules:

- initialize Git before implementation;
- create `.gitignore` for Visual Studio, .NET, test outputs, logs, packages,
  temporary archives, and installer output;
- never commit certificates, signing tokens, passwords, or personal test data;
- commit third-party licenses and attribution;
- keep generated releases out of source folders;
- tag approved releases;
- keep fixture data synthetic.

## 7. Dependency and License Preparation

Before packaging:

- choose the ARCHive application license;
- confirm 7-Zip component license obligations;
- include the 7-Zip LGPL notice and official source link;
- record the pinned 7-Zip version;
- review any native interop or NuGet package license;
- avoid an abandoned third-party 7-Zip wrapper unless its maintenance,
  security, architecture, and license are acceptable;
- review Inno Setup terms for the intended commercial/noncommercial use;
- create `THIRD_PARTY_NOTICES.txt`;
- create an About-page attribution section.

No new runtime dependency should be added merely for a minor convenience.

## 8. Technical Spikes Before Full UI Development

Complete these small proofs first:

### Robocopy spike

- enumerate source totals;
- test `/MT:8`;
- test localized output;
- test progress parsing;
- test cancellation;
- test exit codes 0 through 8+ scenarios;
- test `/XJ`;
- test long and UNC paths;
- decide whether restartable mode and multithreaded progress work together;
- lock the final arguments only after evidence.

### 7-Zip spike

- create and test 7z;
- create and test ZIP;
- extract both;
- capture progress;
- cancel safely;
- test Unicode and long paths;
- test corrupt archives;
- test malicious traversal entries;
- determine the password-safe library interface;
- defer password support if it would require insecure process arguments.

### WPF responsiveness spike

- run a fake high-frequency worker;
- throttle updates to approximately 250 milliseconds;
- confirm the window remains responsive;
- confirm cancellation and close-window behavior;
- test display scaling and keyboard focus.

## 9. Test Fixture Preparation

Create a synthetic fixture containing:

- nested folders;
- empty folders;
- zero-byte files;
- Unicode filenames;
- spaces and special characters;
- many small files;
- one large generated file;
- duplicate content;
- hidden/read-only files;
- a controlled junction and hard link;
- long paths;
- known SHA-256 hashes.

Create archive-security fixtures containing:

- `..` traversal entries;
- absolute paths;
- drive-qualified paths;
- mixed separators;
- Unicode normalization edge cases;
- a corrupt archive;
- an incorrect-password case when password support exists.

Fixtures must contain no private documents or credentials.

## 10. Build and Release Preparation

Development configuration:

- Debug x64;
- local structured logs;
- deterministic fixture paths;
- warnings treated seriously;
- nullable reference types enabled;
- analyzers enabled.

Release configuration:

- Release x64;
- self-contained .NET publish;
- no console window;
- pinned dependencies;
- reproducible version metadata;
- license and attribution files;
- application and installer signing;
- SHA-256 release hashes;
- clean-machine installation test;
- uninstall test;
- automated smoke suite;
- manual nontechnical user test.

## 11. Environment Verification

Before Phase 0, verify:

```powershell
dotnet --info
git --version
robocopy /?
```

Also verify:

- Visual Studio opens a .NET 10 WPF solution;
- the Windows SDK and SignTool are discoverable;
- Inno Setup Compiler is installed;
- the pinned 7-Zip files and license are present;
- the development drive has sufficient free space;
- test destinations contain no irreplaceable data.

Record versions in `docs/DEVELOPMENT_ENVIRONMENT.md`.

## 12. Preparation Completion Gate

Coding begins when:

- final product scope is approved;
- required programs are installed and version-checked;
- repository and solution layout are created;
- product/publisher identifiers are decided;
- 7-Zip files and notices are staged;
- synthetic test storage is available;
- Phase 0 spike acceptance criteria are recorded;
- no real user data is needed for testing.

Public release begins only when:

- clean Windows tests pass;
- license obligations are complete;
- automated and manual acceptance tests pass;
- binaries and installer are signed;
- hashes and release notes are produced.
