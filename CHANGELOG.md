# Changelog

All notable changes to RoboKeep are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/).

## [1.4.1] - 2026-07-05

A hygiene and security patch driven by a full code and privacy audit.

### Security
- Shadow copy IDs are now validated as GUIDs before being used in any WMI query run by the
  elevated VSS helper (closes a WQL-injection path from user-writable files).
- New configurations default to **TLS on** and port 587 for email reports.
- Job names can no longer contain double quotes (they would break the scheduled task command line).

### Fixed
- The **Versions...** window now orders snapshots by date (it ordered by string).
- Three log messages that always appeared in Italian are now localized in all 5 languages.

### Changed
- Job cloning in the editor is now serialization-based with a reflection safety-net test: a
  future job property can no longer be silently lost on edit.
- Removed ~90 lines of dead code; test suite extended from 225 to 243 tests (5-language key
  parity, editor combo/enum couplings, clone completeness).
- New **Privacy** section in the README and a THIRD-PARTY-NOTICES file with dependency
  attributions.

### Downloads
- **`RoboKeep-1.4.1-win-x64-selfcontained.zip`** — bundles **.NET 10.0.9**: extract and run, nothing to install.
- **`RoboKeep-1.4.1-win-x64-framework-dependent.zip`** — smaller; requires the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## [1.4.0] - 2026-07-05 — Operations & integrity

### Added
- **Per-job scheduling** — each job can have its own Windows scheduled task (daily, weekly,
  or monthly at a chosen time), managed from the job editor and kept in sync on save, rename,
  delete, and config import. Tasks are registered via language-neutral XML, so weekday names
  work on any Windows language.
- **Run history with in-app log viewer** — every backup and every integrity check is recorded
  (up to 500 entries); the new **History** window lists them with per-job filtering, and a
  double-click opens the archived log right inside the app (read straight from the zip).
- **Integrity verification** — a **Verify** button compares source and copy file-by-file with
  SHA-256 hashes: mathematical certainty the backup is intact. Smart about false alarms: files
  edited *after* the backup are reported separately, not as corruption; job exclusions are
  honored; versioned jobs verify their latest snapshot. An optional per-job setting runs the
  check automatically after every backup.
- **Copy throttling** — an optional per-job slow-down (`/IPG`) for network backups; when
  active the job runs single-stream so the limit stays predictable.
- **Configuration export/import** — back up or move your whole configuration from Settings;
  import validates first, saves an automatic backup of the current config, and re-syncs all
  scheduled tasks (removing those of jobs that no longer exist).

### Fixed
- Health icons now update as each job finishes during "Run all", not only at the end of the
  whole batch; a job that is currently running is never flagged as "interrupted".
- The toolbar wraps to a second row on narrow windows instead of hiding buttons.
- Run history is safe against concurrent RoboKeep processes (window + scheduled tasks) via a
  cross-process lock.

### Downloads
- **`RoboKeep-1.4.0-win-x64-selfcontained.zip`** — bundles **.NET 10.0.9**: extract and run, nothing to install.
- **`RoboKeep-1.4.0-win-x64-framework-dependent.zip`** — smaller; requires the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## [1.3.0] - 2026-07-04 — Open files (VSS)

### Added
- **Copy open/locked files (VSS)** — opt-in per job: RoboKeep creates a Volume Shadow Copy
  snapshot of the source volume before copying, so files in use (Outlook PST, databases,
  encrypted containers) are copied intact. One UAC confirmation per run; requires the source
  on a local NTFS volume. Works together with versioning.
- **Never blocks the backup** — if the snapshot cannot be created (UAC denied, unsupported
  volume, any unexpected error), the job continues as a normal copy with a clear `[vss]`
  warning in the log; files in use are simply skipped, as before.
- **Wizard question** — the new-job wizard asks whether files stay open during the backup
  and enables VSS accordingly.
- **Eligibility warnings** — at save time and in the pre-run checks when the source cannot
  use VSS (network path or non-NTFS volume).
- **Per-job health icon** — problematic jobs now show a warning icon on their row (red =
  failed, amber = not run for too long, orange = was running at last shutdown) with a
  tooltip explaining the issue; the alert banner is now a compact one-line summary.
- **Interrupted-run detection** — a per-job lock file detects runs cut short by a crash or
  forced shutdown and flags the job as "check before starting".

### Fixed
- Orphaned VSS snapshots from crashed runs are tracked in a PID-stamped ledger and removed
  at the next VSS run; concurrent RoboKeep processes (window + scheduled task) can no longer
  delete each other's live snapshots; the elevated helper cleans up on its own if the app dies.

### Downloads
- **`RoboKeep-1.3.0-win-x64-selfcontained.zip`** — bundles **.NET 10.0.9**: extract and run, nothing to install.
- **`RoboKeep-1.3.0-win-x64-framework-dependent.zip`** — smaller; requires the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## [1.2.1] - 2026-07-01

### Fixed
- **Long paths in versioned backups.** Files whose full path exceeds the historical 260-character limit
  (`MAX_PATH`) made the hard-link clone fail with `CreateHardLink (Win32 3)`. Our direct Win32 calls
  (hard-link creation and read-only-safe delete) now use the `\\?\` extended-length prefix, so long
  paths work.
- **UI no longer freezes during a versioned run.** The hard-link clone and cleanup ran on the UI thread,
  freezing the window on large folders; they now run on a background thread.
- **Interrupted versioned runs no longer leave orphaned `.inprogress` folders.** If a run is cut short
  (app closed, crash, power loss) after copying but before finalizing, its incomplete `.inprogress`
  snapshot used to linger forever — later runs never cleaned it up (they use a different timestamp, and
  retention ignores `.inprogress`). Now each versioned run first removes any leftover `.inprogress` from
  a previous interrupted run (using the read-only-safe delete, so surviving snapshots keep their attributes).

## [1.2.0] - 2026-06-30 — Versioning

### Added
- **Dated snapshots (versioning)** — opt-in per job: every backup becomes a dated folder
  (`YYYY-MM-DD_HHmmss`) on a local NTFS destination. Unchanged files are stored as hard-links
  (no disk duplication); changed files are copied fresh, and previous versions stay intact.
- **Retention** — keep the last *N* snapshots and/or drop snapshots older than a maximum age (days).
- **Browse & restore** — a **Versions…** button lists a job's snapshots and opens any of them in File
  Explorer (the most recent is pre-selected).
- **Destination suitability checks** — at save time and before a run, RoboKeep warns when the
  destination cannot keep versions (no hard-link support, e.g. exFAT or network shares) and falls back
  to a simple mirror instead of silently failing.
- **Summary: extra folders** — the run recap now counts extra folders too, grouped by kind
  (folders: copied / failed / extra, then files: copied / unchanged / extra / failed).

### Fixed
- Retention and cleanup no longer fail on read-only files, and the **ReadOnly attribute is preserved**
  on surviving snapshots (deletion uses POSIX semantics with `IGNORE_READONLY_ATTRIBUTE`, so it removes
  the directory entry without touching the shared inode).
- Column-header sorting now **physically reorders** the job list, so manual drag-and-drop reordering
  stays consistent with what's shown; the sort arrow clears after a manual move.
- Editing a versioned job no longer drops its versioning settings.

### Downloads
- **`RoboKeep-1.2.0-win-x64-selfcontained.zip`** — bundles **.NET 10.0.9**: extract and run, nothing to install.
- **`RoboKeep-1.2.0-win-x64-framework-dependent.zip`** — smaller; requires the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## [1.1.0] - 2026-06-28 — Reliability foundations

### Added
- **Stable data folder** — configuration, results and logs now live in `%APPDATA%\RoboKeep` (per-user;
  survives updates/cleanups). Portable mode via an empty `portable.flag` next to the exe. Existing
  configuration is migrated automatically.
- **Stale/failed backup alert** — a banner and colored rows flag jobs that failed or haven't run for too long.
- **Pre-run checks** — warns if the destination is unreachable or low on free space (Run anyway / Cancel);
  previews are never blocked.
- **Toast notifications + system tray** — toast on job completion and on start-in-tray; minimize/close to
  tray, tray menu (Open / Run all / Exit), start minimized.
- New reliability/notification settings, plus UI refinements (row selection/hover, full-height log).

## [1.0.0] - 2026-06-27 — First release

### Added
- A modern GUI for `robocopy`: simple, transparent, reliable backup and mirroring on Windows.
- Job management (source/destination, mirror, multithread, exclusions, retries), real-time log,
  dry-run preview, log archiving, scheduling and email notifications.

[1.4.1]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.4.1
[1.4.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.4.0
[1.3.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.3.0
[1.2.1]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.2.1
[1.2.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.2.0
[1.1.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.1.0
[1.0.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.0.0
