# Changelog

All notable changes to RoboKeep are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/).

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

[1.2.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.2.0
[1.1.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.1.0
[1.0.0]: https://github.com/robisera-ai/RoboKeep/releases/tag/v1.0.0
