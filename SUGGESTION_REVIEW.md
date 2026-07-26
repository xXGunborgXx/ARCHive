# ARCHive Suggestion Review

## Review Result

The suggestion document contains many strong engineering improvements. Most
can be adopted without adding steps to the ordinary-user workflow because they
operate behind the interface.

The final decision is:

- Adopt the reliability, security, accessibility, logging, and test
  improvements.
- Convert the proposed manual Preview/Dry-run into an automatic preflight.
- Keep the interface limited to source, destination, action, and Start.
- Defer convenience systems that require additional screens or infrastructure.
- Reject premature plugin architecture and any technical rule that prevents
  necessary runtime safety checks.

The original `Project Suggestions.docx` was reviewed without modification.

## Decision Categories

- **Adopt V1:** Required for the first public-quality release.
- **Adopt internally:** Implemented behind the UI without adding a user step.
- **Simplify:** Keep the benefit but remove the unnecessary user process.
- **Defer:** Valuable after Version 1 is proven.
- **Reject:** Conflicts with safety, simplicity, or technical requirements.

## Scope and Defaults

### Dated folder naming only

**Decision: Adopt V1**

Every normal Copy job creates a new dated output folder. Version 1 will not
offer Update Existing Copy or destructive mirror behavior.

### Make ZIP more prominent

**Decision: Simplify**

7z remains the default. When Create Archive is selected, a clearly visible
two-choice control will show:

- **7z - Smaller**
- **ZIP - More compatible**

ZIP does not need a fourth top-level action because that would make the main
screen busier.

### Add Dry-run / Preview

**Decision: Simplify**

There will be no separate Preview button or dry-run workflow in Version 1.
Instead, ARCHive will automatically perform a read-only preflight after source
and destination are selected. The interface will quietly show:

- planned output path;
- source file count and size when available;
- destination free space;
- archive type, when applicable;
- warnings that must be resolved.

The user still presses only **Start**.

### Verify-only mode

**Decision: Defer**

Creating an archive will always include automatic verification. A standalone
Verify Existing Archive action is useful but belongs in Version 1.1.

## Architecture and Implementation

### Immutable JobSpec

**Decision: Adopt with correction**

The Job Planner will produce an immutable `JobSpec`. Engines will not change
the plan while running.

However, the suggestion that engines should never revalidate is rejected.
Files, permissions, free space, and removable drives can change after the user
selects them. Critical conditions must be checked immediately before start,
and runtime I/O errors must still be handled safely.

### Throttled asynchronous progress

**Decision: Adopt internally**

Work runs off the UI thread. Progress updates will be coalesced approximately
every 250 milliseconds rather than updating the interface for every file
event.

### Conservative Robocopy multithreading

**Decision: Adopt internally**

Begin testing at `/MT:8`. Increase only if measurements show a real benefit
without harming responsiveness or log/progress accuracy.

### Log the exact Robocopy command

**Decision: Adopt internally**

The diagnostic log will record the resolved executable, arguments, application
version, and engine version. No passwords or secrets may be present.

Paths are useful for a local diagnostic log but must be clearly disclosed
before any future support-package export because paths can contain personal
names.

### Password-safe 7-Zip path

**Decision: Adopt with a release gate**

Unencrypted create, test, and extract operations can use the official 7-Zip
console component.

Password-protected operations must not place the password in process
arguments. They require a proven library adapter or another verified
password-safe interface. If that adapter does not pass the security and
reliability spike, password protection will move to Version 1.1 rather than
ship insecurely.

### Cooperative cancellation

**Decision: Adopt V1**

Cancellation will stop new work cleanly and explain that a partial folder may
remain. The completion screen will say **Safe to run again**. A repeated
normal job creates a new dated output rather than overwriting the partial copy.

## Path and Edge-Case Hardening

### Long paths and network paths

**Decision: Adopt V1**

The application will use canonical absolute paths and long-path-capable .NET
APIs. UNC/SMB paths will be supported when Windows grants access. Disconnects
and permission failures will produce readable errors.

### Read-only volumes, FAT32 limits, and reparse points

**Decision: Adopt V1**

Automatic preflight will:

- detect an unwritable destination where possible;
- reject a single output file larger than the destination file system allows;
- warn when the destination format may limit the operation;
- exclude junction traversal by default;
- record skipped or unsupported reparse points in the result.

### Archive traversal protection

**Decision: Adopt as a hard release gate**

Every extraction entry must resolve beneath the selected destination after
canonicalization. Entries containing absolute paths, parent traversal, or
another destination escape are rejected before extraction writes data.

### Recheck the source immediately before starting

**Decision: Adopt V1**

Source existence and readability, destination availability, nesting rules, and
free space will be checked again when Start is pressed.

## UX, Accessibility, and Polish

### Keyboard-only operation

**Decision: Adopt V1**

Tab order, visible focus, Enter to start, and Esc to request cancellation are
release requirements.

### Screen-reader support

**Decision: Adopt V1**

Controls, progress state, warnings, and completion state will expose useful
WPF automation names and live status updates.

### Remember archive choices

**Decision: Adopt V1**

The application may remember the last destination, archive type, and
compression preset locally. It never remembers an archive password.

### Filterable log viewer

**Decision: Defer**

Version 1 will show a clean, readable job summary and offer **Open Diagnostic
Log**. Search and filtering are unnecessary for the first release.

### Open Destination as the primary completion action

**Decision: Adopt V1**

Open Destination will be the prominent completion button. ARCHive will ask
File Explorer to select the newly created item when Windows supports it.

## Logging, History, and Diagnostics

### Structured logging

**Decision: Adopt internally**

Each job will have:

- a structured JSON diagnostic record;
- a short user-readable summary;
- a stable job identifier;
- timestamps and engine results.

### Re-run with the same settings

**Decision: Defer to Version 1.1**

The Version 1 data model will not prevent this feature, but the UI and resume
semantics will be designed only after user feedback.

### Machine information in logs

**Decision: Adopt with data minimization**

Logs may record:

- ARCHive version;
- Windows version and architecture;
- .NET runtime version;
- Robocopy and 7-Zip versions;
- destination file system;
- destination free space at start and end;
- engine exit code and duration.

No machine serial numbers, user credentials, file contents, or archive
passwords will be collected.

## Testing and Definition of Done

### Expanded edge-case matrix

**Decision: Adopt**

Add:

- extremely long and deeply nested paths;
- junction and hard-link mixtures;
- empty-folder-only and zero-byte archives;
- antivirus interference and locked-file conditions;
- low-memory stress;
- slow or fragmented media behavior where practical.

### Automated smoke suite

**Decision: Adopt V1**

A deterministic fixture tree will exercise Copy, Create Archive, and Extract
Archive. The suite must assert the final state, output structure, and archive
verification result.

### Nontechnical user test

**Decision: Adopt as a release gate**

A first-time user will be given only a short task, not operating instructions.
If they cannot complete the three main actions or misunderstand the result, the
release is blocked.

## Future-Proofing

### Clean archive-engine interface

**Decision: Adopt internally**

The UI and Job Planner will depend on an archive interface rather than calling
7-Zip directly.

### Future scheduling and multi-source compatibility

**Decision: Adopt minimally**

Use stable job identifiers and serializable job records. Do not add scheduler,
multi-source, or recurrence fields until those features are actually designed.

### Plugin or Advanced-pane architecture

**Decision: Reject for Version 1**

A plugin framework would introduce loading, compatibility, security, and
support complexity without helping the three core actions. Normal internal
interfaces are sufficient. An Advanced screen will not exist until real users
identify options they need.

## Clarifications Resolved

### Exact Robocopy switch set

**Decision: Resolve during the copy-engine spike**

The safe baseline concepts are accepted:

- recursive copy including empty folders;
- data, attributes, and timestamps;
- limited retries;
- junction exclusion;
- moderate multithreading;
- no mirror or purge.

The exact proposed combination is not accepted yet. `/NP`, `/NDL`, and `/NFL`
remove progress or file information that the GUI may need. We will prototype
the output on localized Windows installations and lock the final set only
after progress, logging, cancellation, and exit-code tests pass.

### Progress calculation

**Decision: Adopt an honesty-first rule**

1. Show **Preparing** while the automatic preflight enumerates the source.
2. Prefer byte-based overall progress when reliable total and completed byte
   values are available.
3. Show files completed as supporting information.
4. Throttle UI updates to approximately 250 milliseconds.
5. Show an indeterminate progress bar when a trustworthy percentage is not
   available.
6. Never invent a time-based percentage merely to keep the bar moving.

### Destination is a file rather than a folder

**Decision: Reject the input clearly**

The destination field always represents a folder. If the pasted destination is
a file, ARCHive explains the problem and asks for a destination folder. It
will not silently reinterpret the path.

### Elevation

**Decision: No automatic elevation in Version 1**

ARCHive runs with the user's normal permissions. Protected locations fail with
a clear message. This avoids unexpected administrator prompts and keeps the
security model understandable.

### Update mechanism

**Decision: Defer automatic updates**

Version 1 will be offline-friendly and contain no background update request.
The About page may provide a manual project link. A signed update manifest and
update verification can be designed for Version 1.1.
