# Changelog

All notable changes to ARCHive are documented in this file.

## [1.2.1-beta4] - 2026-07-27

### Safety and Reliability

- Explorer selections are accepted before the first-run notice and forwarded
  through a current-user-only named pipe, preventing multi-selection paths
  from being lost during startup.
- Failed copy jobs now remove the entire dated output whenever cleanup
  succeeds, matching cancellation behavior and avoiding ambiguous partial
  results.
- Archive verification is mandatory. A created archive is published only
  after the 7-Zip integrity test succeeds.
- Drive classification now uses the correct Windows NVMe bus value and the
  device seek-penalty descriptor, avoiding HDDs being treated as SSDs.
- Extract context-menu entries explicitly accept only one selected archive.

### Distribution

- The GitHub prerelease provides the installer, Markdown instructions,
  plain-text instructions and disclosures, and editable questionnaire as four
  separate downloadable files.
- Automated coverage is 48 unit tests and 26 integration tests (74 total).

## [1.2.0-beta3] - 2026-07-27

### Features

- **Windows Explorer context menu integration** — three context menu entries
  are now available when right-clicking files or folders in Windows Explorer:
  - **Copy with ARCHive** — appears for any file or folder selection; opens
    ARCHive with the Copy action pre-selected and files loaded.
  - **Archive with ARCHive** — appears for any file or folder selection; opens
    ARCHive with the Create Archive action pre-selected and files loaded.
  - **Extract with ARCHive** — appears only for `.7z` and `.zip` files; opens
    ARCHive with the Extract Archive action ready for a single archive.

### User Experience

- **Context menu icons** — each context menu entry displays the ARCHive icon
  alongside the text label for visual identification.
- **Command-line argument protocol** — ARCHive now accepts `--copy`, `--archive`,
  and `--extract` arguments with source file paths, enabling the context menu
  integration. A clear error message is shown if the Windows command-line
  length limit is exceeded.
- **Version bump** — release upgraded from `1.1.0-beta2` to `1.2.0-beta3`.

### Installer

- **Context menu registry entries** — the installer now adds context menu
  entries under `HKEY_CURRENT_USER` for files, folders, and archive file
  associations. Entries are automatically removed on uninstall.

### Documentation

- **Context menu disclosure** — installation disclosure and user instructions
  updated to describe the new context menu entries and their behavior.

### Bug Fixes

- **Multi-file context menu single-instance fix** — Windows Explorer invokes
  `%1` context menu commands once per selected file, which previously opened
  multiple ARCHive instances (one per file) instead of one instance with all
  files loaded. Added `SingleInstanceService` with named mutex detection and
  named pipe IPC so subsequent launches send their file paths to the running
  instance and exit. The first instance collects all files into a single
  window.

## [1.1.0-beta2] - 2026-07-27

### Bug Fixes

- **DriveClassifier P/Invoke corrected** — `STORAGE_PROPERTY_QUERY` no longer
  includes a marshaled `byte[]` field that shifted subsequent struct fields.
  `RemovableMedia` changed from `short` to `byte` to match the Win32 `BOOLEAN`
  type, fixing offset errors in `STORAGE_DEVICE_DESCRIPTOR` reads. Bus-type
  constants corrected to match Windows `STORAGE_BUS_TYPE` values (SCSI=0x01,
  USB=0x07, SATA=0x0B, NVMe=0x0D).
- **Double-buffer consistency** — single-file copy loop now uses
  `await readTask` instead of `.Result` for consistency with async patterns.

### User Experience

- **Cancel-vs-fail summary text hardened** — all runner failure paths now
  explicitly state whether incomplete output was removed or preserved at the
  destination. Cancel messages clearly state the source was not modified.
  Extraction failures note that any files written before failure are preserved.
- **Version bump** — release upgraded from `1.0.0-beta1` to `1.1.0-beta2`.

### Testing

- New integration tests: many small files, cancel-after-partial-progress,
  cancel-vs-fail summary distinction, archive verify on/off.
- New unit tests: `DriveClassifier` bus-type mapping via reflection.
- Test counts updated to 41 unit + 27 integration (68 total).

## [Unreleased]

### Performance

- **Double-buffered file copy pipeline** — single-file and folder-copy runners now
  overlap read and write operations using two buffers, improving throughput on fast
  storage (`CopyJobRunner.cs`, `PausableFolderCopyRunner.cs`).
- **Adaptive buffer sizing** — buffer size is selected per file based on size:
  256 KB (small), 1 MB (medium), 4 MB (large), replacing the previous fixed 1 MB
  buffer for all files.
- **ArrayPool buffer rental** — copy buffers are rented from
  `System.Buffers.ArrayPool<byte>.Shared` instead of allocating fresh arrays on
  every file, reducing GC pressure during large jobs.
- **Drive-aware concurrency** — `DriveClassifier` queries
  `IOCTL_STORAGE_QUERY_PROPERTY` via P/Invoke to detect NVMe, SATA, USB, SCSI,
  and network bus types, then selects 2/4/8 concurrent copy slots based on drive
  speed class (`DriveClassifier.cs`).

### User Experience

- **Verify-after-create toggle** — the Create Archive panel now includes an
  optional "Verify after create" checkbox (enabled by default). Unchecking it
  skips the post-create `7z t` verification pass, saving time for trusted
  sources. The toggle is wired through `ArchiveCreateSpec.VerifyAfterCreate`,
  `ArchiveJobPlanner`, and `SevenZipArchiveRunner` (`MainWindow.xaml`).
- **ZIP compression tuning** — the `CompressionArguments` helper now returns
  separate argument arrays and applies ZIP-specific defaults (Deflate is used
  implicitly by 7-Zip for `.zip` format; dictionary-size tuning is applied for
  the Smallest preset).

### Resilience

- **Source-change retry with 2-second backoff** — when a file's source changes
  during copy (`SourceChangedException`), the per-file retry loop now retries
  up to 3 times with a 2-second delay (previously only non-source-change
  `IOException` was retried with a 5-second delay) (`CopyFileWithRetryAsync`).
- **Partial output preservation on failure** — folder-copy failure now preserves
  already-copied files at the destination instead of deleting the entire output
  tree, allowing users to keep partially completed work when a source changes
  mid-copy.

### Code Quality

- **Dead-code removal** — removed ~542 lines of unused robocopy infrastructure
  from `CopyJobRunner.cs`, including `RunRobocopyAsync`,
  `MonitorRobocopyProgressAsync`, `VerifyFolderCopy`, `MeasureTree`, and
  related types, reducing the file from 756 to 214 lines.
- **Shared utility extraction** — `PathUtilities` in `ARCHive.Core` now houses
  `TryGetAvailableFreeSpace`, `TryGetDriveFormat`, `TryGetDriveType`,
  `GetTopLevelName`, `FormatArgumentsForLog`, and `FormatBytesForLog`, consumed
  by the copy, archive, and infrastructure layers.

### Testing

- Integration test `RunAsync_SourceChangeWhilePausedFailsAndRemovesOutput` renamed
  to `RunAsync_SourceChangeWhilePausedFailsAndPreservesPartialOutput` to match
  the new partial-preservation behavior.
- All 52 tests (32 unit + 20 integration) pass.
