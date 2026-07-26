# Logging and Privacy Boundary

Status: Seven-day beta policy

## Local Trial Record

The beta creates an encrypted current-user record under
`%LOCALAPPDATA%\ARCHive\Beta\trial.dat`. It contains the first-launch time,
most recent launch time, calculated expiry state, and any lock reason.
ARCHive does not upload this record. It remains after normal uninstalling so
reinstalling does not automatically restart the seven-day trial.

## What the Current Preview Records

ARCHive writes one local JSON diagnostic record after each Copy, Create
Archive, or Extract Archive operation. The record contains the planned job,
result, performance totals, environment information, and limited engine
details. Full source and destination paths and file names may therefore appear
in a record.

Records are stored under `%LOCALAPPDATA%\ARCHive\Logs`. They are not uploaded
automatically. There is no telemetry or cloud-reporting service.

JSON operation records older than 30 days are removed when a new record is
written. Uninstalling the preview does not remove these records automatically.

## Public-Release Gate

The testing-preview policy is not automatically the public-release policy.
Before release, the project must decide and document:

- whether detailed paths and engine output remain enabled by default;
- whether the user receives an in-app logging control;
- the final retention period and deletion control;
- which fields are required for a useful operation receipt;
- the final Privacy Notice and Terms of Use wording.

The final legal documents should be reviewed by a qualified professional for
the intended countries and distribution method. This file documents product
behavior; it is not legal advice or a substitute for final legal terms.
