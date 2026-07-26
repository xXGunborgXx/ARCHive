# ARCHive 0.5.1-preview Archive-Performance Acceptance Checklist

Use disposable test data. Keep the accepted `0.5.0-preview` installer as the
rollback baseline.

## Installation

1. Restore or snapshot the Windows 11 test VM.
2. Install `0.5.1-preview` as a normal user.
3. Confirm the local-record disclosure still appears.
4. Launch ARCHive and select Create Archive.

## Video-Scenario Retest

1. Select several files and folders from the same parent folder.
2. Use the same 7z, Balanced, and VMware shared-drive scenario shown in
   `ARCHive-test-08.mp4`.
3. Confirm all selections appear in one source list.
4. Start Create Archive and confirm progress remains responsive.
5. Compare completion time and average speed with `0.5.0-preview`.
6. Extract the result and verify every file with SHA-256.
7. Confirm only the selected top-level names appear—no original parent or
   machine paths.

The 18.94 GB video workload will still be limited by compression, four virtual
CPU cores, and VMware shared-drive throughput. The correction removes repeated
archive-add operations; it does not claim hardware-independent speed.

## Same-Parent and Different-Parent Coverage

1. Create a mixed archive from files and folders sharing one parent.
2. Repeat with sources from two different parent folders.
3. Repeat with sources from two different drives.
4. Confirm all archives verify and extract correctly.
5. Confirm duplicate, parent/child-overlapping, and same-name selections are
   still rejected.

## Formats and Compression

1. Repeat same-parent mixed selection with 7z Fast, Balanced, and Smallest.
2. Repeat with ZIP Fast and Balanced.
3. Confirm the final archive appears only after verification.
4. Confirm Cancel removes the temporary archive and publishes no final output.

## General Regression

1. Repeat single-file and single-folder Create Archive.
2. Repeat single-file, single-folder, and mixed-source Copy.
3. Repeat Pause/Resume and cancellation for Copy.
4. Repeat Extract Archive.
5. Confirm Open Destination and Open Diagnostic Log.
6. Confirm speed and `waiting for storage...` remain truthful.
7. Check keyboard-only operation and 100%, 125%, and 150% display scaling.
