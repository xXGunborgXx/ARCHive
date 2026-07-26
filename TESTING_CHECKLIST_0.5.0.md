# ARCHive 0.5.0-preview Multi-Source Archive Acceptance Checklist

Use disposable test data. Keep the accepted `0.4.1-preview` installer as the
rollback baseline.

## Installation

1. Restore or snapshot the Windows 11 test VM.
2. Install `0.5.0-preview` as a normal user.
3. Confirm the local-record disclosure still appears.
4. Launch ARCHive and select Create Archive.

## Mixed Archive Selection

1. Confirm **Add File(s)** and **Add Folder(s)** are both visible.
2. Add at least three files with Ctrl/Shift selection.
3. Add at least two folders afterward, including one with an empty folder.
4. Add another file and confirm all earlier choices remain.
5. Confirm Remove Selected and Clear All affect only the Create Archive list.
6. Switch to Copy and back; confirm each action retains its own selection.
7. Drag several files and folders together onto Source and confirm they are
   added to the archive list.

## Selection Safety

1. Select the same item twice and confirm it appears only once.
2. Try selecting a folder and a file already inside it; confirm Start remains
   unavailable and the overlap is explained.
3. Select two items from different parents with the same top-level name;
   confirm the collision is explained instead of silently overwriting.
4. Confirm Extract Archive still accepts only one `.7z` or `.zip` file.

## 7z Round Trip

1. Select mixed files and folders located under different parent directories
   or drives.
2. Create a 7z archive with Balanced compression.
3. Confirm the output is named like
   `ARCHive Collection - 2026-07-27 0915.7z`.
4. Confirm the final archive appears only after verification succeeds.
5. Extract it with ARCHive.
6. Compare every extracted file with its source using SHA-256.
7. Confirm selected folders keep their nested structure and empty folders.
8. Confirm the archive contains only the selected top-level names—not original
   parents such as `C:\Users`, drive names, or other machine paths.

## ZIP Round Trip

Repeat the complete 7z round trip using **ZIP - Compatible**. Open the ZIP in
Windows File Explorer as an additional compatibility check.

## Cancellation and Large Files

1. Start a mixed-source archive containing at least one large file.
2. Cancel while archiving.
3. Confirm no final archive is published and the temporary archive is removed.
4. Confirm every source remains unchanged.
5. Repeat a successful 7z and ZIP job with representative large files.
6. Confirm progress, processed counts, speed, and
   `waiting for storage...` remain responsive.

## General Regression

1. Repeat single-file and single-folder Create Archive.
2. Repeat single-file, single-folder, and mixed-source Copy.
3. Repeat Extract Archive.
4. Confirm Open Destination and Open Diagnostic Log use the completed job.
5. Confirm no time-remaining estimate appears.
6. Check keyboard-only operation and 100%, 125%, and 150% display scaling.

If the VM crashes again, retain the exact-time Windows Event Viewer System and
Application entries plus the newest ARCHive diagnostic log.
