# ARCHive 0.4.0-preview Multi-Source Copy Acceptance Checklist

Use disposable test data. Keep the accepted `0.3.0-preview` installer as the
rollback baseline.

## Installation

1. Restore or snapshot the Windows 11 test VM.
2. Install `0.4.0-preview` as a normal user.
3. Confirm the local-record disclosure still appears.
4. Launch ARCHive and select Copy.

## Selection

1. Click **Add Files** and Ctrl/Shift-select at least three files.
2. Confirm all selected paths appear in the selected-item list.
3. Click **Add Folders** and select at least two folders.
4. Confirm the folders are added without replacing the files.
5. Use **Remove Selected** on one item and confirm only that item is removed.
6. Use **Clear All** and confirm the source selection becomes empty.
7. Drag several files and folders together onto Source and confirm they are
   added.
8. Paste one path into the empty Source field and confirm the single-source
   workflow still works.

## Output Layout

1. Select a mixture of files and folders and choose a destination.
2. Confirm preflight shows one dated `ARCHive Copy` output.
3. Start Copy.
4. Confirm each selected file is directly inside the dated batch.
5. Confirm each selected folder retains its top-level name, nested structure,
   empty folders, timestamps, and attributes where supported.
6. Run full SHA-256 verification against every copied file.
7. Confirm **Open Destination** opens the dated batch.

## Ambiguous Selection Protection

Using disposable test data, confirm ARCHive refuses:

1. selecting the same source again does not add a duplicate;
2. a folder together with a selected item inside that folder;
3. two selected files or folders that have the same top-level name.

No output should be created for a refused preflight.

## Pause and Cancel

1. Select multiple sources containing enough files to make Pause visible.
2. Press Pause and confirm active files finish before the Paused state.
3. Confirm no `.partial` file is presented as complete.
4. Resume and verify the final batch by SHA-256.
5. Repeat, but Cancel while copying and while paused.
6. Confirm the entire dated batch is removed after cancellation and every
   source remains unchanged.

## Regression

1. Repeat one single-file, one single-folder, Create Archive, and Extract
   Archive test.
2. Confirm percentage and speed remain responsive and no ETA appears.
3. Confirm `Speed: waiting for storage...` remains accurate.
4. Confirm Open Destination and Open Diagnostic Log still work.
5. Check keyboard-only use and 100%, 125%, and 150% display scaling.

If the VM crashes again, record the exact time and retain the VM Windows Event
Viewer System/Application entries plus the newest ARCHive diagnostic log. Do
not classify it as an ARCHive or VM fault without that evidence.
