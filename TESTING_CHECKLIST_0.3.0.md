# ARCHive 0.3.0-preview Pause Acceptance Checklist

Use disposable test data. Keep the accepted `0.2.0-preview` installer as the
rollback baseline.

## Installation

1. Restore or snapshot the updated Windows 11 test VM.
2. Install `0.3.0-preview` as a normal user.
3. Confirm the local-record disclosure still appears.
4. Launch ARCHive and select Copy.

## Pause Eligibility

1. Select a single file, including one large file or ISO.
2. Confirm no Pause button appears.
3. Select a folder containing at least two files.
4. Start Copy and confirm Pause appears beside Cancel.
5. Confirm Pause does not appear for Create Archive or Extract Archive.

## Pause and Resume

1. Use a folder containing more files than the coordinator can copy
   concurrently. Include several large files so the state changes are visible.
2. Press Pause while files are actively copying.
3. Confirm the interface says it is finishing active files and starts no new
   files.
4. Confirm the final state says Paused between files.
5. While paused, inspect the dated output:
   - completed files may remain;
   - no `.partial` file should remain;
   - the job must not be presented as Completed.
6. Press Resume and confirm copying continues with the next file.
7. Let the operation finish and run full SHA-256 verification.

## Source-Change Protection

Using disposable test data:

1. Pause after some files have completed.
2. Modify one source file already present in the paused destination.
3. Press Resume.
4. Confirm ARCHive reports that the source changed and removes the invalid
   dated job output.
5. Confirm the source remains otherwise untouched.

## Cancel from Paused State

1. Pause a folder copy.
2. Press Cancel while Paused.
3. Confirm the entire current dated output is removed.
4. Confirm no completed or partial files from that cancelled job remain.
5. Confirm earlier completed jobs and the source are unchanged.

## Cancel While Pausing

1. Request Pause while several large files are active.
2. Before Paused appears, press Cancel.
3. Confirm active copying stops and the whole current job output is removed.
4. If Windows prevents cleanup, confirm ARCHive explicitly reports that
   incomplete output remains.

## Performance and UI Regression

1. Repeat the previously accepted large-copy tests on HDD-to-HDD, HDD-to-SSD,
   and SSD-to-SSD where available.
2. Confirm percentage and measured speed remain responsive.
3. Confirm no time-remaining estimate appears.
4. Confirm `Speed: waiting for storage...` still appears when appropriate.
5. Confirm Open Destination and Open Diagnostic Log still work.
6. Note that Open Diagnostic Log button sizing is intentionally deferred to
   the dedicated UI-polish pass.

Record file counts, total size, storage types, Pause timing, completed files
while paused, Resume result, cancellation cleanup result, measured speed, and
SHA-256 verification.
