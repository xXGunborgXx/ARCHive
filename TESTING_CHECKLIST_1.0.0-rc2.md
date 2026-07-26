# ARCHive 1.0.0-rc2 Compact UI Acceptance Checklist

Use disposable data and keep the accepted `0.5.1-preview` installer as the
engine rollback baseline.

## Installation and Branding

1. Restore or snapshot the Windows 11 test VM.
2. Install `1.0.0-rc2` as a normal user.
3. Confirm the installer uses its dark graphite styling, matching title bar,
   and ARCHive icon.
4. Confirm the local-record disclosure remains readable before installation.
5. Confirm the ARCHive icon appears on the installed application, desktop
   shortcut, Start menu entry, and uninstaller.
6. Launch ARCHive and confirm the 680 x 500 window is centered.

## Fixed Layout and Readability

1. Check Copy, Create Archive, and Extract Archive at 100% display scaling.
2. Repeat at 125% and 150%.
3. Confirm labels, paths, progress details, and result text remain readable.
4. Confirm no button text is clipped.
5. Confirm the window has no resize grip, maximize button, or page scrollbar.
6. Confirm all content remains visible in the fixed window.
7. Confirm the custom title bar can move, minimize, and safely close ARCHive.
8. Confirm keyboard Tab order, Enter to start, and Escape to cancel.

## Source Selection

1. Add one file and confirm its path appears in the source field.
2. Add several files and folders and confirm only the accurate item count is
   shown; no large selected-path list should appear.
3. Use Clear and confirm the complete current selection is removed.
4. Confirm file and folder selections remain additive after the list removal.
5. Confirm Extract Archive still accepts only one archive.

## Operation State

1. Confirm the idle readiness and Start panel is visible before an operation.
2. Start Copy and confirm the idle panel is replaced by the progress panel.
3. Repeat for Create Archive and Extract Archive.
4. Confirm archive options collapse during an active archive operation so the
   progress controls remain inside the fixed window.
5. Confirm the completion result replaces progress without exposing a page
   scrollbar.

## Progress Treatment

1. Run a small Copy and confirm measured progress fills the amber bar.
2. Run a folder Copy and confirm Pause/Resume remains visible and usable.
3. Run Create Archive and confirm Archiving and Verifying are distinguishable.
4. Confirm indeterminate work uses the amber activity sweep without showing a
   fabricated percentage.
5. Confirm measured speed and `waiting for storage...` remain readable.
6. Confirm no time-remaining estimate appears.

## Full Workflow Regression

1. Copy one file, one folder, and mixed sources from different directories.
2. Create and extract same-parent and different-parent 7z archives.
3. Repeat archive creation and extraction with ZIP.
4. Verify representative output files with SHA-256.
5. Test Pause/Resume, Cancel while copying, and Cancel while paused.
6. Test archive cancellation and confirm no final archive is published.
7. Confirm Open Destination targets the actual completed output.
8. Confirm Diagnostic Log opens the current operation record.

## Release Decision

Promote the release candidate to `1.0.0` only after the VM and physical-PC
checks pass. Public distribution additionally requires the documented legal,
logging-control, code-signing, publisher, and support-channel decisions.
