# ARCHive Final Project Plan

## 1. Product Definition

ARCHive is a simple Windows desktop utility that gives ordinary users a safe
graphical interface for:

1. copying one or more files and folders;
2. creating a 7z or ZIP archive;
3. extracting an archive.

The product hides copy-engine details, 7-Zip commands, path syntax, retry rules, and
diagnostic codes. Users interact through familiar Windows selection dialogs,
drag and drop, path paste, one Start button, one progress screen, and a clear
result.

Database backup is not part of ARCHive. Disk cloning, Windows imaging,
synchronization, and cloud backup are also separate concerns.

## 2. Product Principles

- **Simple:** no command line or technical switches.
- **Non-destructive:** no mirror, purge, move, or source deletion.
- **Predictable:** normal jobs always create a new dated output.
- **Honest:** no false percentage or false success message.
- **Recoverable:** cancellation never damages the source and is safe to repeat.
- **Private:** no telemetry, automatic network request, or password logging.
- **Tested:** every created archive is automatically verified.
- **Accessible:** keyboard and screen-reader operation are release requirements.

## 3. Version 1 User Experience

### Main screen

The main screen contains:

- **Copy**
- **Create Archive**
- **Extract Archive**
- Source
- Destination
- Start

The Source field supports:

- drag and drop;
- paste from File Explorer;
- additive **Add File(s)** and **Add Folder(s)** controls for Copy and Create
  Archive; each native picker accepts only its stated item type and every
  choice joins that action's mixed-source list;
- one archive-file picker for Extract Archive.

The Destination field represents a folder and supports:

- drag and drop;
- paste from File Explorer;
- **Choose Destination**.

The program remembers the last successful destination unless the user turns
that setting off.

### Automatic preflight

There is no separate Dry-run or Preview action. After source and destination
are selected, ARCHive automatically performs a read-only preflight and shows a
short summary:

```text
Copy "My Project"
to "E:\My Backups\My Project - 2026-07-26 1430"
2,481 files - 6.2 GB
Destination available space: 184 GB
```

Warnings appear only when the user must act.

### Progress

The progress screen shows:

- Preparing, Copying, Archiving, Verifying, or Extracting;
- overall progress when it can be calculated reliably;
- files processed and total files;
- bytes processed and total bytes;
- current item;
- elapsed time;
- measured processing or transfer speed when byte progress is trustworthy;
- Cancel.

UI updates are asynchronous and throttled to approximately 250 milliseconds.
If the engine cannot provide trustworthy progress, ARCHive shows an
indeterminate bar and an accurate activity message instead of inventing a
percentage.

ARCHive does not display a time-remaining estimate. Storage, network,
compression, caching, and antivirus conditions can change too quickly for that
prediction to remain trustworthy.

### Completion

Four result states:

- **Completed**
- **Completed with warnings**
- **Cancelled**
- **Failed**

The completion view shows:

- plain-language result;
- output location;
- number of files and bytes processed;
- elapsed time;
- archive verification result;
- **Open Destination** as the primary button;
- **View Details**;
- **Run Again** only for the completed in-session job.

When possible, Open Destination selects the newly created item in File
Explorer.

## 4. Copy Feature

### Normal behavior

- Accept one or more source files and folders.
- Accept one destination folder.
- Create a new dated result.
- Put multi-source selections in one dated `ARCHive Copy` batch while
  preserving each selected top-level name.
- Reject duplicate, overlapping, and same-name top-level selections rather
  than silently duplicating or replacing data.
- Preserve normal file data, attributes, and timestamps where supported.
- Include empty folders.
- Skip junction traversal by default.
- Limit retries for locked or unavailable files.
- Keep the source unchanged.
- Never delete destination files.

Example:

```text
Source:      C:\Users\Name\Documents\My Project
Destination: E:\My Backups
Result:      E:\My Backups\My Project - 2026-07-26 1430
```

### Engines

- Individual file: Windows/.NET file-copy implementation with progress.
- Folder: cooperative file coordinator with bounded concurrency.

The folder coordinator:

- recursively copies files and creates empty directories;
- excludes junction traversal;
- preserves normal file and directory timestamps and attributes;
- writes each active file to an application-owned temporary path;
- publishes a file only after its length and source state are rechecked;
- stops assigning new files when Pause is requested;
- allows active files to finish before entering Paused;
- validates the completed-file session manifest before Resume;
- removes the entire dated job output on Cancel whenever Windows permits.

Windows Robocopy remains a tested rollback and comparison engine, but it is not
the normal Pause-capable folder path because a single Robocopy process cannot
reliably stop between files.

### Cancellation

Pause, cancellation, and future resume behavior follow one integrity rule:

> Pause applies to the overall copy operation. Partial files are never treated
> as valid copied files.

- When Pause is requested, stop assigning new files and allow active files to
  finish whenever possible.
- Display `Pausing after current file...` or
  `Pausing after current files...` while active work finishes.
- Files that completed successfully remain complete and are not copied again
  when the same job resumes.
- An individual file is never published as a resumable partial copy.
- If Cancel interrupts an individual file, delete the incomplete destination
  file owned by ARCHive.
- When the job is continued, copy the interrupted file again from the
  beginning.
- Keep the original source unchanged.
- Never display an interrupted file or job as Completed.
- Clearly mark any retained destination folder as incomplete until every file
  has completed and the job has passed its required completion checks.

For a single large file, including an ISO, Pause cannot take effect safely
between files because there is only one file. The interface must explain that
the current file is finishing, or offer immediate Cancel. Immediate Cancel
removes the incomplete destination file, and a later attempt starts that file
again from zero.

Cross-session resume is not exposed until ARCHive has a tested job manifest
that can identify completed files, detect source changes, reject invalid
checkpoints, and prove that incomplete files are restarted rather than
published. If those guarantees are unavailable, Version 1 provides safe
cancellation and a new copy attempt instead of resume.

## 5. Create Archive Feature

### Visible choices

When Create Archive is selected:

- **7z - Smaller** is the default.
- **ZIP - More compatible** is clearly visible.
- Compression choices are Fast, Balanced, and Smallest.
- The last archive type and compression choice may be remembered.

### Normal behavior

- Accept one or more source files and folders.
- Add every selected item to one archive while preserving its top-level name
  and nested structure.
- Store no original parent-folder or machine path in the archive.
- Reject duplicate, overlapping, and same-name top-level selections rather
  than creating an ambiguous archive.
- Send selections sharing a parent folder to the archive engine together and
  use no more than one add operation per unique parent folder.
- Generate the output name automatically.
- Name a multi-source output `ARCHive Collection` plus its date and time.
- Add a date when the name already exists.
- Write to an application-owned `.partial` output.
- Keep the source unchanged.
- Test the archive after creation.
- Publish the final `.7z` or `.zip` only after the test succeeds.
- Report a failed verification as Failed, not Completed.

### Password protection

Password protection is included only when the library adapter passes its
security spike.

Release requirements:

- Passwords never appear in process arguments.
- Passwords never appear in logs, history, settings, or crash reports.
- Passwords are held in memory only as long as necessary.
- 7z password mode encrypts file names as well as contents.
- The program never remembers a password.

If these requirements cannot be met reliably, password protection is deferred
to Version 1.1 while unencrypted archive creation remains in Version 1.

## 6. Extract Archive Feature

- Accept a supported archive as the source.
- Accept a destination folder.
- Create a new dated extraction folder.
- Never silently overwrite an existing folder.
- Request a password only if required and supported.
- Show damaged-archive and incorrect-password errors clearly.
- Preserve empty folders and zero-byte files.

### Hard extraction security gate

Before writing an entry:

1. Decode and normalize its path.
2. Reject absolute, device, drive-qualified, or UNC entry paths.
3. Reject parent traversal that escapes the destination.
4. Resolve the final path canonically.
5. Confirm that the final path remains beneath the selected extraction root.
6. Reject unsafe reparse-point behavior.

An archive that violates these rules must fail safely before writing outside
the selected destination.

## 7. Automatic Safety Checks

Before Start:

- source still exists;
- source is readable;
- destination exists or its parent can be used;
- destination is a folder, not a file;
- destination is writable where testable without changing user data;
- source and destination are not identical;
- a folder is not being copied into itself or its descendant;
- the removable/network destination is currently available;
- destination free space is sufficient when it can be determined;
- destination file system supports the planned maximum file size;
- paths can be represented safely;
- critical archive metadata is readable.

During the job:

- handle source disappearance;
- handle permission changes;
- handle destination disconnect;
- handle destination-full conditions;
- handle locked files and antivirus interference;
- stop safely when cancellation is requested;
- preserve the real engine result.

ARCHive does not request administrator elevation in Version 1. Protected
locations produce a clear message.

## 8. Accessibility

- Logical Tab order.
- Visible keyboard focus.
- Enter activates Start when valid.
- Esc requests cancellation while a job runs.
- Buttons have accessible names.
- Progress and result state are exposed to screen readers.
- Color is never the only indication of success, warning, or failure.
- Text remains usable with Windows display scaling.
- Animations are restrained and nonessential.

## 9. Logging and Local History

Each job has a stable identifier and:

- a short user-readable summary;
- a structured JSON diagnostic record;
- start/end timestamps;
- application and engine versions;
- action, source, destination, and resolved output;
- Windows/.NET architecture and versions;
- destination file system and free space;
- file and byte counts;
- engine exit code;
- warning and error summaries;
- verification result.

Logs never contain:

- archive passwords;
- credentials;
- file contents;
- machine serial numbers.

Version 1 provides a readable details view and an option to open the diagnostic
log. Searchable/filterable log viewing is deferred.

## 10. Internal Architecture

```text
ARCHive.App
  WPF views, accessibility, navigation, progress, completion

ARCHive.Core
  immutable JobSpec, validation rules, result model, interfaces

ARCHive.Copy
  individual-file adapter, cooperative folder coordinator, pause state

ARCHive.Archive
  7-Zip create/test/extract adapter, path security

ARCHive.Infrastructure
  settings, JSON logs, local history, platform information

ARCHive.Tests
  unit, integration, smoke, and security fixtures
```

The Job Planner creates an immutable `JobSpec`. Engines cannot alter it.
Critical mutable conditions are rechecked immediately before start and handled
during execution.

Archive and copy engines remain behind normal internal interfaces. Version 1
will not contain a plugin loader or Advanced pane.

## 11. Technology

- C#
- .NET 10 LTS
- WPF
- Supported Windows 11 runtime target
- Self-contained Windows x64 release
- Visual Studio 2026 development environment
- Robocopy supplied by Windows as a rollback/comparison engine
- Pinned official 7-Zip components
- Inno Setup installer
- Git source control

## 12. Local Settings

Version 1 settings:

- Remember last destination: On/Off
- Remember archive choices: On/Off
- Default archive type: 7z/ZIP
- Default compression: Fast/Balanced/Smallest
- Diagnostic retention: 7/30/90 days
- Theme: System/Light/Dark

No automatic update check, cloud account, telemetry, scheduler, or network
service exists in Version 1.

## 13. Development Phases

### Phase 0 - Technical spikes

- Confirm the minimum supported Windows 11 release from the current .NET 10
  support matrix.
- Benchmark Robocopy and cooperative-copy progress, cancellation, and throughput.
- Lock the safe normal and rollback engine behavior from evidence.
- Prototype 7-Zip create, test, extract, and progress.
- Prove password-safe library integration or defer password support.
- Confirm long-path and UNC behavior.

Gate:

- Written spike results and selected implementation paths.
- No unresolved password exposure.
- No invented progress percentage.

### Phase 1 - Foundation and UI shell

- Create solution and projects.
- Implement simple main layout.
- Implement picker, drag/drop, and paste workflows.
- Implement immutable JobSpec and automatic preflight.
- Implement asynchronous job runner and throttled progress model.
- Implement structured logging.
- Add unit tests for path and validation rules.

Gate:

- UI stays responsive under simulated high-frequency progress.
- Invalid and nested paths are blocked.
- Keyboard-only flow works.

### Phase 2 - Safe Copy

- Implement individual-file copy.
- Implement the tested cooperative folder coordinator and Robocopy rollback.
- Implement honest progress and exit-code mapping.
- Implement cancellation and incomplete-result reporting.
- Handle removable and network destination failures.
- Add Copy smoke fixtures.

Gate:

- Small, large, and many-file fixtures pass.
- No source or destination deletion occurs.
- Junction loops do not escape the selected source.
- Normal and rollback engine results are interpreted correctly.

### Phase 3 - Archive Creation

- Package pinned 7-Zip components and notices.
- Implement 7z and ZIP creation.
- Implement compression presets.
- Implement `.partial` output publishing.
- Automatically test every created archive.
- Add password support only if its security spike passed.

Gate:

- Every completed archive passes verification.
- Failed/cancelled jobs do not look like valid final archives.
- Unicode, empty-folder, zero-byte, and large-file fixtures pass.

### Phase 4 - Secure Extraction

- Implement extract workflow and progress.
- Implement canonical path enforcement.
- Implement dated extraction output.
- Add corrupt, password, traversal, and overwrite fixtures.

Gate:

- No archive entry can write outside the destination.
- Wrong passwords and damaged archives fail clearly.
- Existing output is not silently replaced.

### Phase 5 - Accessibility, history, and packaging

- Complete automation names, keyboard flow, scaling, and themes.
- Implement local job history and readable details.
- Implement About and third-party notices.
- Create installer and uninstaller.
- Add Authenticode signing when a release certificate is available.
- Test clean supported Windows 11 releases.

Gate:

- Installer and uninstaller pass clean-machine testing.
- Uninstall never removes user-created outputs.
- No command windows appear.
- Nontechnical usability test passes without guidance.

## 14. Test Matrix

### Files and paths

- small file;
- multi-gigabyte file;
- thousands of small files;
- empty folders;
- zero-byte files;
- Unicode and non-English names;
- spaces and shell-special characters;
- very long paths and deep nesting;
- hidden and read-only files;
- junctions, symbolic links, and hard links;
- source disappearance after selection.

### Destinations

- NTFS internal drive;
- NTFS removable drive;
- FAT32 file-size boundary;
- nearly full and full destination;
- read-only destination;
- USB removal during work;
- SMB/UNC disconnect;
- inaccessible protected location;
- destination entered as a file.

### Runtime

- user cancellation;
- application closure request during work;
- locked file;
- concurrent antivirus scan;
- low-memory stress;
- slow or fragmented media where practical;
- Windows display scaling;
- keyboard-only and screen-reader flow.

### Archives

- 7z and ZIP;
- only empty folders;
- zero-byte files;
- large files;
- Unicode names;
- damaged archive;
- incorrect password;
- malicious absolute and parent-traversal entries;
- verification failure;
- cancellation during create and extract.

## 15. Automated Smoke Suite

A deterministic fixture tree will:

1. Run Copy and compare expected names, sizes, and hashes.
2. Create 7z and ZIP outputs and require successful tests.
3. Extract both outputs and compare the restored fixture.
4. Assert user-facing result states.
5. Assert that sources were not modified.
6. Assert that unsafe archive paths are rejected.

The smoke suite must run locally without a network account.

## 16. Version 1 Release Definition

Version 1 is complete only when:

- all three actions work without command-line knowledge;
- the normal workflow remains source, destination, Start;
- no destructive copy mode exists;
- progress is responsive and honest;
- cancellation is safe and clearly explained;
- every completed archive is verified;
- extraction traversal tests pass;
- logs contain no passwords;
- clean supported-Windows installation tests pass;
- the automated smoke suite passes;
- a nontechnical user completes all three tasks without guidance.

## 17. Deferred Features

Version 1.1 candidates:

- Verify Existing Archive;
- rerun from history;
- saved favorite destinations;
- manual signed update check;
- password protection if its V1 security spike did not pass.

Later candidates:

- schedules;
- Windows notifications;
- Explorer context menu;
- snapshot/versioned repositories;
- offsite/cloud storage;
- ARM64 and portable builds.

Still separate projects:

- database backup and restore;
- disk cloning;
- Windows system imaging.
