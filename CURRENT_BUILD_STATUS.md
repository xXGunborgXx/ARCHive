# ARCHive Current Build Status

Date: 2026-07-27

Beta: 1.2.0-beta3

Trial period: Seven days from first launch

Target: Windows 11 x64

## Beta Package

Package directory:
`artifacts\ARCHive-1.2.0-beta3-7day-package`

The distribution directory contains exactly four files:

1. `ARCHive-Beta-1.2.0-beta3-7day-Setup.exe`
2. `README.md`
3. `INSTRUCTIONS_AND_DISCLOSURES.txt`
4. `ARCHive_Beta_Test_Questionnaire.docx`

Installer size: 47,515,223 bytes (45.31 MiB)

Installer SHA-256:
`5BB891DC15D568BEBCE8790408A9E6E9CE81AB3C631EB1E49BDF7B091C8218A8`

## Seven-Day Trial Behavior

- The trial begins on first application launch, not during installation.
- The application checks the trial only at startup; an operation already in
  progress is never interrupted by expiry.
- The encrypted local record is stored under
  `%LOCALAPPDATA%\ARCHive\Beta\trial.dat`.
- The record contains the first-run time, most recent run time, and any
  permanent expiry or clock-rollback lock.
- Uninstalling and reinstalling does not restart the trial.
- A Windows clock rollback beyond the correction tolerance locks the beta.
- Invalid or unreadable trial state fails closed.
- The first-launch notice requires an explicit **Start Beta** action. Closing
  the notice exits ARCHive.

## Privacy and Feedback

- The installer discloses the seven-day trial, local diagnostic records,
  and context menu integration before installation.
- Operation records remain local under `%LOCALAPPDATA%\ARCHive\Logs`, are not
  uploaded automatically, and use the existing 30-day retention behavior.
- The Markdown and plain-text instructions contain the same safety, trial,
  logging, context menu, and submission disclosures.
- The editable five-page questionnaire asks testers to email completed feedback
  to `GunborgServers@gmail.com`.

## Automated Verification

- Release build: passed with 0 warnings and 0 errors.
- Unit tests: 41 passed.
- Integration tests: 27 passed.
- Total: 68 passed, 0 failed.
- Self-contained Windows x64 publish: passed.
- Inno Setup 6.7.3 compilation: passed.

## Visual Verification

- Branded first-launch beta notice: passed.
- Notice copy and locally calculated expiry date: passed.
- Notice-to-main-window startup lifecycle: passed after correction.
- Compact 680 x 500 main window and `7-DAY BETA • LOCAL` badge: passed.
- Branded dark installer and beta title: passed.
- Questionnaire: five pages rendered and inspected with no clipping or
  overflow; logo alternative text is present.
- Explorer context menu entries with ARCHive icon: pending verification.

## Manual Beta Acceptance

Use a clean, fully updated Windows 11 x64 VM for installation and workflow
testing. Repeat Copy, Pause/Resume, Cancel, Create Archive, and Extract Archive
with small, mixed, and large workloads. Test the Explorer context menu entries:
Copy with ARCHive, Archive with ARCHive, and Extract with ARCHive. Complete
the questionnaire and return it by email.

This installer is not code-signed and Windows may show an Unknown publisher or
SmartScreen warning. Public promotion still requires beta feedback, final
Terms of Use and privacy approval, publisher/support details, and a code-signing
decision.
