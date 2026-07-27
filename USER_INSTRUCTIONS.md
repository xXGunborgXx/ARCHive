# ARCHive User Instructions

Status: Seven-day beta instructions

Current beta: 1.1.0-beta2

## Seven-Day Beta Period

The beta period begins the first time ARCHive is launched and ends seven days
later. ARCHive checks the beta status only when it starts. If the seven-day
period expires while Copy, Create Archive, or Extract Archive is running, that
operation is not interrupted.

The beta status is kept in an encrypted local record under
`%LOCALAPPDATA%\ARCHive\Beta`. The record contains the first-run time, most
recent run time, and any permanent expiry or clock-rollback lock. It is not
uploaded. Uninstalling and reinstalling the beta does not restart the trial.
Moving the Windows clock backward beyond the allowed correction tolerance
locks the beta to protect the integrity of the testing period.

Testers should complete `ARCHive_Beta_Test_Questionnaire.docx` and email it to
`GunborgServers@gmail.com`.

## Selecting More Than One Copy Source

On the Copy screen, **Add File(s)** accepts normal Windows Ctrl/Shift
multi-selection and **Add Folder(s)** accepts multiple folder selection. Every
choice is additive, so files can be added first and folders added afterward,
or the other way around. Multiple items dropped together are also added.

Windows uses separate native selection modes for files and folders. Folders
visible inside the file picker are navigation locations and cannot be returned
as selected copy items. Files visible in the folder picker cannot be returned
as folders. The picker titles state this distinction.

The source field shows the selected path when one item is chosen and the total
item count when more than one item is chosen. **Clear** starts the selection
again. To change only one item in a multi-selection, clear the selection and
choose the intended files and folders again.

A multi-source job creates one dated folder named like:

```text
ARCHive Copy - 2026-07-27 0915
```

Each selected file keeps its file name. Each selected folder keeps its
top-level folder name and structure inside that batch. ARCHive refuses
ambiguous selections rather than silently overwriting or duplicating data:

- the same item selected twice;
- a folder together with a file or folder already inside it;
- two top-level items with the same name.

Single-file and single-folder copies retain their established dated naming.

## Selecting More Than One Archive Source

Create Archive uses the same clear, additive **Add File(s)** and
**Add Folder(s)** controls. Add files and folders in any order; every choice
remains in one archive selection until it is cleared.

Each selected item is stored under its own top-level name. ARCHive does not
store the original parent folders or machine path. For example, selecting:

```text
C:\Work\readme.txt
D:\Pictures\Holiday Photos
```

creates an archive whose top level contains:

```text
readme.txt
Holiday Photos\
```

The same safety rules used by multi-source Copy apply. ARCHive refuses the
same item twice, a parent folder together with an item inside it, or two
top-level items with the same name. A multiple-source archive receives a dated
name such as `ARCHive Collection - 2026-07-27 0915.7z`.

For efficiency, selected files and folders that share a parent folder are sent
to the archive engine together in one operation. Selections from different
locations use one operation per unique parent folder. This avoids repeatedly
reopening the archive for every selected item while preserving the same safe
top-level layout.

Extract Archive intentionally accepts one `.7z` or `.zip` file at a time.

## Copy Cancellation in the Current Beta

ARCHive never modifies the original source.

For a single-file copy, ARCHive writes to an application-owned `.partial`
file. If the copy is cancelled, it tries to remove that incomplete file. The
incomplete file is never presented as a successful copy.

For a folder or multi-source copy, ARCHive maintains an in-session manifest of
files proven complete. Cancellation still removes the entire dated output
folder created for that job. Fully completed copies from earlier jobs and
anything outside the current dated output are not removed.

If Windows, antivirus software, a disconnected drive, or permissions prevent
cleanup, ARCHive reports:

> Copy cancelled. Incomplete output remains and must not be treated as
> completed. The source was not changed.

This is intentionally more conservative than presenting unverified files as a
usable partial backup.

## Pause and Resume

Eligible folder and multi-source copies provide Pause and Resume during the
current ARCHive session. The integrity rule is:

> Pause applies to the overall copy operation. Partial files are never treated
> as valid copied files.

When Pause is requested, ARCHive stops assigning new files. Files already
being copied are allowed to finish and are published only after their lengths
and source state pass the file-level checks. ARCHive then displays `Paused`
and waits without starting more files.

Files proven complete remain in the current dated output while paused. Resume
rechecks the completed-file session manifest and starts with the next file. If
a completed source file changed during the pause, ARCHive stops, reports the
source change, and removes the invalid job output.

For a single file, including an ISO, there is no between-file pause point, so
the Pause button is not shown. Cancel removes the incomplete destination and a
new attempt starts from zero.

Pause is also unavailable for archive creation and extraction in this beta.

Resume is in-session only. Closing ARCHive, cancelling the job, restarting
Windows, or losing the destination ends the session and triggers full cleanup
when possible. Cross-session Resume remains disabled until persistent
checkpoints can be proven safe.

## Archive Cancellation

Create Archive writes all selected sources into one application-owned
temporary archive and publishes the final `.7z` or `.zip` only after the
complete archive passes verification. Cancellation removes the temporary
archive whenever Windows permits. Sources are never modified.

Extraction writes individual files into a new dated extraction folder. If
extraction is cancelled, ARCHive tries to remove that entire dated output.
If Windows prevents cleanup, ARCHive states that incomplete output remains and
must not be treated as completed.

## Progress and Speed

ARCHive displays measured processing or transfer speed when reliable byte
progress is available. If the storage device temporarily stops reporting
movement, it displays `Speed: waiting for storage...` so the user knows the
engine is still working.

ARCHive does not display a time-remaining estimate. Copy, compression, and
extraction rates can change because of storage caching, file sizes, device
temperature, antivirus activity, network conditions, and compression ratios.
The preview reports measurements it can observe instead of predicting a finish
time.

## Local Operation Records and Privacy

This testing beta writes one local JSON diagnostic record after each Copy,
Create Archive, or Extract Archive operation. Records may contain:

- selected source, destination, and output paths;
- file names shown by the copy or archive engine;
- result, duration, file and byte counts, and average speed;
- warnings, errors, exit codes, and limited engine details;
- Windows, architecture, .NET, and ARCHive version information.

Records are stored under `%LOCALAPPDATA%\ARCHive\Logs`. They are not uploaded
automatically, and ARCHive contains no telemetry or cloud reporting. JSON
records older than 30 days are removed when a new operation record is written.
Uninstalling the beta does not automatically remove existing records.

The installer shows this disclosure before installation. The public release
must receive a separate privacy, logging-control, retention, and Terms of Use
review.
