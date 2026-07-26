# ARCHive 0.4.1-preview Picker-Clarity Acceptance Checklist

Use disposable test data. Keep the accepted `0.4.0-preview` installer as the
rollback baseline.

## Installation

1. Restore or snapshot the Windows 11 test VM.
2. Install `0.4.1-preview` as a normal user.
3. Confirm the local-record disclosure still appears.
4. Launch ARCHive and select Copy.

## Add File(s)

1. Confirm the Copy screen displays **Add File(s)** and **Add Folder(s)**.
2. Click **Add File(s)**.
3. Confirm the picker title states that folders are for navigation only.
4. Ctrl/Shift-select at least three files.
5. Confirm only those files are added to the selected-item list.
6. Highlight or navigate through folders in the file picker and confirm no
   folder is returned as a selected file.

Windows must show folders in this dialog so the user can navigate into them.
Showing a folder is not the same as allowing it to be selected as a result.

## Add Folder(s)

1. Click **Add Folder(s)** after files have already been added.
2. Confirm the picker title states that files cannot be selected.
3. Ctrl-select at least two folders.
4. Confirm only the folders are added and all earlier files remain listed.
5. Confirm visible files cannot be returned as selected folders.
6. Add more files afterward and confirm the selections remain additive.

## Selection Controls

1. Confirm selecting the same item again does not create a duplicate.
2. Confirm **Remove Selected** removes only highlighted sources.
3. Confirm **Clear All** empties the source list.
4. Drag several files and folders together onto Source and confirm all are
   added.
5. Paste one path into an empty Source field and confirm it remains valid.

## Action Regression

1. Switch to **Create Archive** and confirm **Choose File** and
   **Choose Folder** remain available.
2. Switch to **Extract Archive** and confirm only **Choose File** is shown.
3. Switch back to Copy and confirm any preserved Copy selection is restored.

## Copy, Pause, and Cancellation

1. Copy a mixture of files and folders into one dated `ARCHive Copy` batch.
2. Confirm each top-level name and nested structure is preserved.
3. Run full SHA-256 verification on every copied file.
4. Repeat with Pause and Resume.
5. Repeat with Cancel while copying and Cancel while paused.
6. Confirm cancellation removes the entire current dated batch and leaves all
   sources unchanged.

## General Regression

1. Repeat single-file and single-folder Copy.
2. Repeat Create Archive and Extract Archive.
3. Confirm progress, speed, waiting-for-storage, Open Destination, and Open
   Diagnostic Log behavior.
4. Confirm no time-remaining estimate appears.
5. Check keyboard-only operation and 100%, 125%, and 150% display scaling.

If the VM crashes again, retain the exact-time Windows Event Viewer System and
Application entries plus the newest ARCHive diagnostic log.
