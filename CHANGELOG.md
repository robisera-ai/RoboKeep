# Changelog

All notable changes to RoboKeep are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/).

## [Unreleased] — Hardware safety of the backup media

### Changed
- **A hardware error now stops the job at once.** When a disk — or its cable/enclosure — reports a
  physical error (data error/CRC, sector not found, I/O device error), RoboKeep **kills robocopy at
  the first such line**, before it retries and moves on to thousands of other files, and marks the
  job as failed with a clear "hardware error" status and actionable advice (check cable and power
  first, then SMART). The same applies to the hard-link clone and cleanup of versioned jobs and to
  the integrity verification. This **reverses the 1.6.0 behaviour** of skipping an unreadable file
  in the previous snapshot and carrying on: writing a whole backup onto a disk that has just
  reported a physical error is the wrong call. Files that can't be linked for ordinary reasons
  (locked, permissions) are still skipped and recopied.
- **A faulted disk is left alone for the rest of the session.** After a hardware error, the
  remaining jobs of the same run (`--run-all`, or a batch from the window) that use that disk are
  not started and are reported as failed.
- **Disk-error wording no longer blames the disk alone**: on external disks the same errors often
  come from a bad cable, USB enclosure or power supply, and the message now says so.

### Added
- **The PC stays awake while a backup or verification runs.** RoboKeep holds a Windows power
  request for the duration of the work, so automatic sleep can't cut power to a USB disk in the
  middle of a write. (Closing the lid or pressing the sleep button still sleeps the PC.)
- **Automatic thread cap on mechanical disks.** RoboKeep asks Windows what kind of media sits
  behind source and destination; when either is a mechanical disk — or a USB disk that can't be
  identified and doesn't support TRIM — copy threads are capped at 2 whatever the job says, and the
  log notes it. SSDs and network paths are unaffected.

## [1.6.0] - 2026-07-23 — In-app guide & versioning resilience

### Added
- **Built-in guide** — a browsable manual right inside the app, opened from the **Guide** button
  in the toolbar or with **F1**. Chapters on the left, content on the right, previous/next
  navigation at the bottom; the window is modeless, so you can follow it while you work. 17
  chapters in **Italian and English** (other app languages fall back to English), covering
  installation, the job editor, mirror vs accumulate, versions, open-file copying, integrity
  verification, scheduling, disk rotation, network backups, history, notifications, settings, the
  command line, troubleshooting, and privacy. Rendered natively — no new dependencies. The
  chapters also live in the repo under `docs/guide/`.

### Changed
- **Versioning is now resilient to a bad file in the previous snapshot.** If cloning the previous
  version via hard-links hits an unreadable file (e.g. a bad sector on the destination disk),
  RoboKeep now **skips that file and continues** — it gets recopied fresh from the source by the
  ensuing robocopy pass — instead of failing the whole job. The skipped files are logged, with a
  count summary.
- **Clearer disk-error messages.** A read failure that means a likely **bad sector** (cyclic
  redundancy check and similar) is now reported as such, pointing you to check the destination
  disk's health — instead of a cryptic "data error (cyclic redundancy check)".
- Guide note: on **mechanical destination disks**, keep the multi-thread count low (2-4); a high
  count only helps on SSDs.

### Downloads
- **`RoboKeep-1.6.0-win-x64-selfcontained.zip`** — bundles .NET 10: extract and run, nothing to install.
- **`RoboKeep-1.6.0-win-x64-framework-dependent.zip`** — smaller; requires the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## [1.5.1] - 2026-07-18

A small follow-up to 1.5.0, driven by field feedback and housekeeping.

### Fixed
- The job editor no longer hides the disk row when a job's destination points at a local disk
  that isn't currently connected. It now shows a clear "this destination's disk isn't connected"
  note, so changing the destination toward an unplugged disk can no longer silently leave the job
  unprotected.

### Changed
- CI and release GitHub Actions bumped to their Node.js 24 versions (`checkout` v5, `setup-dotnet`
  v6, `ghaction-virustotal` v5, `action-gh-release` v3), clearing the Node.js 20 deprecation
  warnings. Inputs and outputs used by the workflow were verified unchanged first.

### Docs
- Added **Spanish, French, and German** READMEs, all cross-linked with the English and Italian
  ones. Every README now leads with the disk-rotation safety feature and carries a short note
  inviting native-speaker translation fixes via pull request.

## [1.5.0] - 2026-07-18 — Disk rotation safety

RoboKeep now identifies a backup disk by its volume, not its drive letter, so it can tell two
external disks apart even when Windows gives them the same letter — and refuses to run a mirror
job on the wrong disk.

### Added
- **Disk rotation protection** — a job can be tied to a specific disk by its volume identity (a
  permanent id assigned at format, independent of the drive letter). Before running, RoboKeep
  checks the connected disk: if it is the wrong one — or if the expected disk is not connected
  at all — the job is **skipped without touching anything** (no lock, no UAC prompt, no
  robocopy). This closes a real data-loss path: two external disks sharing the same letter
  (`E:`) with the same destination paths, where a `/MIR` job on the wrong disk would delete the
  other disk's contents.
- **"Skipped" as a third outcome** — a skipped job is neither success nor failure: no error, no
  error email, exit code `0` from the command line (so the nightly scheduled task stops
  reporting a false failure), its own entry in the run history, and a log line naming the
  expected and connected disks.
- **Neutral "waiting" state** — a protected job whose disk is unplugged shows a grey hourglass
  ("Disk resting for N days") instead of the amber "overdue" alarm, and drops out of the
  warning banner: a disk you deliberately keep unplugged is not a problem. A safety net still
  raises the real alarm past 90 days (a truly forgotten disk).
- **Disk row in the job editor** — shows which disk a job is tied to, offers **Protect with
  this disk** for unprotected jobs, and **Use this disk** (with a warning) when a different disk
  is connected. Hidden for network destinations, which have no removable volume.
- **Automatic refresh on disk connect/disconnect** — health icons update on their own the
  moment you plug or unplug a disk, no manual refresh needed.
- **Unsaved-changes prompt** — closing the job editor with the window's ✕ after making changes
  asks for confirmation; only when something actually changed, and only for the ✕ (Save and
  Cancel are explicit choices).

### Fixed
- The hard-link support check no longer blocks saving a versioned job when it merely times out:
  a slow external disk taking more than a few seconds to answer is treated as "unknown", not
  "unsupported". Only a definite "not supported" blocks; at run time versioning keeps its usual
  graceful fallback to a normal copy.

### Changed
- The disk association can only be written when a job is new or when its destination changes,
  never on a plain save — so opening the editor with the wrong disk inserted and saving an
  unrelated change cannot silently re-tie the job to the wrong disk. An explicit **Use this
  disk** button is the only way to reassociate without changing the destination.
- Network destinations are always exempt from the disk check (they have no removable volume),
  regardless of any stored id.
- Test suite extended from 243 to 296 tests.

### Downloads
- **`RoboKeep-1.5.0-win-x64-selfcontained.zip`** — bundles .NET 10: extract and run, nothing to install.
- **`RoboKeep-1.5.0-win-x64-framework-dependent.zip`** — smaller; requires the
  [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

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
