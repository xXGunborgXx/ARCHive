# Changelog

All notable changes to ARCHive are documented in this file.

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
