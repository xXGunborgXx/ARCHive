# ARCHive Project Plan - Initial Draft

> Superseded by `FINAL_PROJECT_PLAN.md` after review of
> `Project Suggestions.docx`. This file is retained as the original planning
> record.
>
> Implementation amendment: beginning with `0.3.0-preview`, normal folder
> copies use a cooperative, bounded-concurrency coordinator so Pause can occur
> safely between files. Robocopy remains a rollback and comparison engine.

## 1. Product Summary

ARCHive will be a simple Windows desktop program for copying files and folders
and creating or extracting archives. It will give ordinary users a safe,
visual interface over proven Windows tools without exposing command-line
options, scripts, switches, or technical directory setup.

The basic workflow is:

1. Choose, paste, or drag in a file or folder.
2. Choose, paste, or drag in the destination.
3. Select **Copy**, **Create Archive**, or **Extract Archive**.
4. Press **Start**.
5. Watch one clear progress screen.
6. Receive a plain-language success, warning, or failure result.

Database backup is explicitly outside this project. It will be designed later
as a separate, more comprehensive system.

## 2. Project Goals

- Make reliable file copying understandable to nontechnical Windows users.
- Reduce mistakes caused by command-line syntax and manual path editing.
- Never delete source or destination data during the normal workflow.
- Show honest progress and a clear final result.
- Allow interrupted copies to be cancelled safely and run again.
- Verify archives before reporting them as successful.
- Produce useful technical logs without making users read those logs.
- Keep the initial release focused and dependable.

## 3. Version 1 Scope

Version 1 will contain three primary actions:

### 3.1 Copy

Copy one file or one folder to another location using Robocopy for folder jobs
and the appropriate Windows file-copy API for individual files.

The default will be **Safe Copy**:

- Copy the selected source into the selected destination.
- Create the source folder inside the destination automatically.
- Preserve file data, attributes, and timestamps where supported.
- Resume or safely repeat an interrupted folder copy.
- Skip identical files.
- Exclude junction traversal by default.
- Use limited retries so a locked or damaged file cannot stall forever.
- Never purge or mirror-delete destination files.
- Write a job log automatically.

Example:

```text
Source:      C:\Users\Name\Documents\My Project
Destination: E:\My Backups
Result:      E:\My Backups\My Project
```

If the destination already contains a folder with the same name, the simple
default will be to create a dated copy:

```text
E:\My Backups\My Project - 2026-07-26 1430
```

This avoids overwrite questions and prevents an ordinary user from
accidentally replacing a previous copy. An **Update Existing Copy** behavior
can be added later as an advanced option.

### 3.2 Create Archive

Create either a `.7z` or `.zip` archive from the selected file or folder.

Default behavior:

- `.7z` for compact storage.
- Balanced compression so the PC remains responsive.
- Automatic output name based on the source.
- Automatic date suffix when the name already exists.
- Write to a temporary `.partial` file first.
- Test the completed archive with 7-Zip.
- Rename it to the final archive name only after verification succeeds.
- Keep the original source unchanged.

Simple optional controls:

- **Archive type:** 7z or ZIP
- **Protect with password**
- **Show password**

When password protection is enabled for a 7z archive, file-name encryption
will also be enabled. Passwords will never be placed in logs or remembered
unless a future credential-storage design is deliberately approved.

### 3.3 Extract Archive

Extract a supported archive through the same simple source-and-destination
workflow.

Safety behavior:

- Preview the archive name and destination before starting.
- Create a new output folder automatically.
- Prevent archive entries from escaping the selected destination.
- Never silently overwrite an existing destination folder.
- Request the password only when the archive requires it.
- Display a clear error for an incorrect password or damaged archive.

## 4. Main User Interface

The main window will avoid technical settings. It will contain:

### Action selector

Three large choices:

- **Copy**
- **Create Archive**
- **Extract Archive**

### Source box

- Displays the selected file or folder.
- Accepts drag and drop.
- Accepts a path pasted from File Explorer.
- Has one **Choose File or Folder** button.
- Includes a clear/remove button.

### Destination box

- Displays where the result will be placed.
- Accepts a folder dragged from File Explorer.
- Accepts a pasted path.
- Has one **Choose Destination** button.
- Remembers the last successful destination for convenience.

### Start area

- One large **Start** button.
- A short sentence showing exactly what will happen.
- Estimated source size and available destination space when known.

### Progress screen

- Overall progress bar.
- Percentage when the underlying operation provides enough information.
- Files completed and total files.
- Data copied and total data.
- Current file name, shortened safely for display.
- Elapsed time and measured transfer or processing speed when reliable.
- **Cancel** button.
- No command window or raw Robocopy/7-Zip output.

### Completion screen

The result will use one of four states:

- **Completed**
- **Completed with warnings**
- **Cancelled**
- **Failed**

It will show:

- Output location
- Files processed
- Total data written
- Elapsed time
- Archive verification result, when applicable
- **Open Destination** button
- **View Details** button
- **Run Again** button

## 5. Ordinary-User Safeguards

- No `/MIR`, purge, move, or source-deletion operation in Version 1.
- No destination formatting or drive manipulation.
- No hidden automatic deletion of old copies.
- No following junctions or reparse-point loops by default.
- Warn when source and destination resolve to the same location.
- Block copying a folder into itself or one of its own subfolders.
- Check available destination space before starting when Windows can report it.
- Warn when the destination is removable and becomes disconnected.
- Use safe temporary names for incomplete archives.
- Clearly mark partial or cancelled jobs.
- Never report success solely because a process started.
- Translate Robocopy and 7-Zip exit results into readable messages.
- Keep a diagnostic log for troubleshooting.

## 6. Robocopy Integration

Robocopy is built into supported Windows versions, so users will not install
or configure it.

ARCHive will construct and run the safe command internally. The user will not
see or edit switches.

Intended folder-copy behavior:

- Include subfolders and empty folders.
- Use restartable transfer behavior.
- Exclude junction points.
- Preserve normal data, attributes, and timestamps.
- Use a small retry count and wait interval.
- Produce a per-job log.
- Use moderate multithreading after performance testing.

Robocopy exit codes require interpretation because some nonzero values mean
files were copied or differences were detected rather than total failure.
ARCHive will map them into the four user-facing result states.

## 7. 7-Zip Integration

Yes, the archive feature should use 7-Zip rather than implementing a new
compression format.

The preferred integration is an isolated archive adapter built on the official
7-Zip library component shipped with the application. This gives ARCHive
consistent behavior even if the user has not installed 7-Zip separately and
avoids placing archive passwords in process command-line arguments.

The official standalone console component may be used for operations that do
not contain passwords, such as archive testing, if it proves more reliable for
progress reporting. Password-protected creation and extraction must use the
library adapter unless testing confirms an equally safe method that does not
expose the password in process arguments.

The release must:

- Bundle an approved, pinned 7-Zip version.
- Include the required 7-Zip/LGPL attribution and source link.
- Record the bundled 7-Zip version in **About** and diagnostic logs.
- Invoke any console-based 7-Zip operation without opening a console window.
- Parse progress and completion results.
- Use the 7-Zip test operation after archive creation.
- Never place passwords in process arguments, command previews, logs, job
  history, or crash reports.

7z will be the compact-storage default. ZIP will be available for users who
need broad compatibility with other computers and built-in operating-system
tools.

## 8. Recommended Technology

### Application

- C#
- .NET 10 LTS
- WPF desktop interface
- Windows 10 and Windows 11 target
- Self-contained x64 installer for the first release

Why:

- Strong Windows file and process APIs.
- Native file/folder dialogs and drag-and-drop support.
- Reliable background operations without freezing the interface.
- Straightforward progress, cancellation, logging, and installer support.
- Better long-term Windows integration than a collection of scripts.

ARM64 and portable builds can be considered after the x64 version is stable.

### Internal components

```text
ARCHive UI
  |
  +-- Job Planner
  |     validates source, destination, space, and safety rules
  |
  +-- Copy Engine
  |     Robocopy adapter for folders
  |     Windows copy adapter for individual files
  |
  +-- Archive Engine
  |     7-Zip create, test, and extract adapter
  |
  +-- Progress Translator
  |     converts technical output into user-friendly progress
  |
  +-- Job History and Logs
        records results without credentials or sensitive contents
```

## 9. Settings

Version 1 settings should stay small:

- Remember last destination: On/Off
- Default archive type: 7z/ZIP
- Default compression: Fast/Balanced/Smallest
- Keep diagnostic history for: 7/30/90 days
- Theme: System/Light/Dark
- Check for application updates: On/Off

Technical Robocopy switches will not be exposed in the ordinary interface.
If advanced settings are added later, they will be placed behind a separate
expert section with safe defaults and explanations.

## 10. Job History

The application will keep a local history containing:

- Date and time
- Action type
- Source
- Destination
- Result
- File count
- Total bytes
- Duration
- Warning/error summary
- Log-file location

History will not contain archive passwords or file contents. A user can open
the destination or diagnostic details from an entry.

## 11. Explicitly Out of Scope

The following are not part of Version 1:

- Database backup
- SQL dump creation or restore
- Whole-disk cloning
- Windows system imaging
- Cloud accounts
- Network account management
- Continuous synchronization
- Scheduled unattended jobs
- Ransomware-proof repositories
- Deduplicated snapshot repositories
- File version retention policies
- Destructive mirroring
- Automatic deletion of old backups
- Backup of locked application databases

These exclusions keep the first product easy to understand and test.

## 12. Development Phases

### Phase 1: Foundation

- Create the .NET 10 WPF solution.
- Establish the visual style and window layout.
- Implement source and destination models.
- Implement drag/drop, paste, and Windows picker workflows.
- Add validation for invalid, identical, or nested paths.
- Establish structured logging and job result types.

Acceptance:

- A user can select or paste source and destination paths.
- The app prevents obvious self-copy and nested-copy mistakes.
- The interface remains responsive during a simulated job.

### Phase 2: Safe Copy

- Implement individual-file copying.
- Implement the Robocopy folder adapter.
- Parse progress and exit codes.
- Add cancellation and restart-safe behavior.
- Add destination-space checks.
- Add dated destination naming.
- Add completion summaries and **Open Destination**.

Acceptance:

- Small and large file/folder tests complete correctly.
- Cancelling does not delete the source.
- Re-running a cancelled folder copy behaves predictably.
- Junction-loop tests do not recurse outside the selected tree.
- Locked-file and disconnected-drive failures produce readable results.

### Phase 3: Archive Creation

- Package the approved 7-Zip library and any required console component.
- Implement the archive adapter and password-safe library interop.
- Add 7z and ZIP creation.
- Add Fast, Balanced, and Smallest presets.
- Add optional password protection.
- Add temporary output and automatic verification.
- Protect passwords from logs and UI history.

Acceptance:

- Created archives pass the 7-Zip test operation.
- Failed tests never produce a normal-looking final archive.
- Existing output names are not silently overwritten.
- Unicode, long-name, large-file, and empty-folder tests pass.

### Phase 4: Archive Extraction

- Add archive selection and destination generation.
- Add password requests.
- Add extraction progress and cancellation.
- Add traversal and overwrite protections.

Acceptance:

- Valid archives extract correctly.
- Wrong passwords and corrupt archives are explained clearly.
- Existing destination contents are not silently replaced.
- Malicious relative-path entries cannot write outside the destination.

### Phase 5: Polish and Packaging

- Finalize light and dark themes.
- Add accessibility labels and keyboard navigation.
- Add job history and log viewer.
- Add installer, uninstaller, license notices, and About page.
- Test clean Windows 10 and Windows 11 installations.
- Perform large-copy and low-space testing.

Acceptance:

- No command windows appear.
- A first-time user can complete a copy without instructions.
- App and outer window remain responsive throughout operations.
- Installer includes all required dependencies and notices.
- Uninstalling does not remove user-created copies or archives.

## 13. Test Matrix

The release will be tested with:

- One small file
- One large file
- Thousands of small files
- Empty folders
- Unicode and non-English names
- Long paths
- Hidden and read-only files
- Junctions and symbolic links
- Locked files
- A nearly full destination
- USB removal during a job
- Network-path interruption
- User cancellation
- Existing output names
- Password-protected archives
- Incorrect archive passwords
- Damaged archives
- Paths containing spaces and special characters

Testing must distinguish:

- UI acceptance
- Static validation
- Copy/archive completion
- Archive verification
- Manual restore/extraction acceptance

## 14. Version 1 Definition of Done

Version 1 is ready when:

- Copy, archive creation, and archive extraction work from the simple UI.
- No command-line knowledge is required.
- The app never performs destructive mirroring.
- Progress and cancellation work without freezing the window.
- Copy results and Robocopy warnings are translated accurately.
- Every created archive is automatically tested.
- Logs contain enough detail to diagnose failures but never contain passwords.
- The installer works on clean supported Windows machines.
- A nontechnical tester can complete all three main actions without guidance.

## 15. Possible Future Versions

Only after Version 1 is stable:

- Multiple source selection
- Saved favorite destinations
- One-click repeat from job history
- Scheduled copy/archive jobs
- Windows notifications
- Network-share credential support
- Snapshot/versioned backup engine
- Offsite/cloud destinations
- SHA-256 manifest exports
- Explorer right-click integration
- ARM64 build
- Portable edition

Database protection will remain a separate product or independently designed
module even if both programs later share branding or interface components.
