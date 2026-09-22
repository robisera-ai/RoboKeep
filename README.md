# RoboKeep

*Read in: English · [Italiano](readmeita.md) · [Español](readmees.md) · [Français](readmefr.md) · [Deutsch](readmede.md)*

**Set it up once — every file, every version, safe on your own disk.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Languages](https://img.shields.io/badge/languages-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Downloads](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep is a friendly Windows app that turns `robocopy` — the rock-solid copy engine already
built into every Windows PC — into a **real backup tool**: point-and-click setup, **dated
versions** of your files, copying of **files you're still working on**, each job on **its own
schedule**, a full **run history**, and **mathematical proof** your copies are intact.
Rotate two external disks that Windows both calls `E:`? RoboKeep keeps each job on **its own
disk** and never mirrors onto the wrong one. **No cloud, no account, no subscription** — your
files never leave your disks.

![RoboKeep main window: jobs with results at a glance and a live execution log](docs/images/main-window.png)

## Why you'll like it

- 🧙 **Answer a few questions, get the right backup.** No robocopy knowledge needed: the guided
  setup asks about your disks and your data in plain language, and picks the optimal settings
  for you. Experts can still tweak everything by hand.
- 💿 **It looks after your disks, not just your files** *(new in 1.7)*. A backup tool can wear
  out the very disk it writes to. RoboKeep now **stops at the first hardware error** (CRC, bad
  sector, I/O device error) instead of grinding on for hours, and leaves that disk alone for the
  rest of the session; keeps the PC **awake** so automatic sleep can't cut power to a USB disk
  mid-write; **caps copy threads on mechanical disks**, which it detects on its own; skips
  creating a new version when **nothing has changed**; and reads the **Windows event log** for
  the bad blocks and I/O errors that precede a failure — warning you *before* the damage, not
  after. Integrity checks are now **periodic** (weekly by default) instead of after every run.
- 🛡️ **Rotating backup disks? It never writes to the wrong one** *(new in 1.5)*. If you alternate
  two external drives, Windows often hands them the **same letter** (`E:`) — and a mirror job
  aimed at the wrong one could wipe it clean. RoboKeep recognizes each disk by its true
  **identity**, not its letter, and simply **skips** a job when the disk in the slot isn't the
  one it belongs to: no error, nothing deleted. Reconnect the right disk and everything picks up
  where it left off — the icons even update the moment you plug it in.
- 🕰️ **A time machine for your files.** Every run can save a **dated version** of your backup.
  Deleted a paragraph last Tuesday? Open Tuesday's version and get it back. Smart trick under the
  hood: unchanged files are *shared* between versions, so ten versions don't cost ten times the
  space — only what actually changed.
- 🔓 **Copies files even while you're using them** *(new in 1.3)*. Outlook archives, databases,
  files locked by other programs: with one checkbox, RoboKeep photographs the disk for an instant
  (a Windows "shadow copy") and copies from that frozen picture. And if the snapshot isn't
  possible, the backup simply continues the normal way — it never blocks.
- ✅ **Mathematical proof your backup is intact** *(new in 1.4)*. The **Verify** button re-reads
  every file on both sides and compares digital fingerprints (SHA-256): silent disk corruption —
  invisible to any date/size check — gets caught. Smart, too: a file you edited *after* the
  backup is reported as such, never as a false alarm. Run it on demand, or automatically on a
  schedule (every 7 days by default) for your critical jobs.
- ⏰ **Every job on its own schedule** *(new in 1.4)*. Documents every evening, photos on Sunday,
  archives once a month: each job gets its own Windows scheduled task and runs even with the app
  closed.
- 📜 **A memory of every run** *(new in 1.4)*. The **History** window lists every backup and
  every integrity check with outcome, counts, and duration — and a double-click opens the full
  log in Notepad, no digging through zip files. Every entry has its own log, verifications
  included; a **Log folder** button takes you straight to the files.
- 🚨 **It tells you when something's wrong — and only then.** A backup that fails silently is
  worse than no backup. RoboKeep marks each problematic job with a colored icon — red for
  failed, amber for "not run in too long", orange for "was interrupted", grey for "waiting for
  its disk" — with a plain explanation on hover. It won't nag you about a job whose disk you've
  simply unplugged: that's an hourglass, not an alarm.
- 🔍 **Nothing hidden.** The editor always shows the **exact command** that will run. You can
  preview any backup (a "dry run") to see what would be copied or deleted, before touching anything.
- 🏠 **Truly yours.** Free and open source (MIT), fully local, no telemetry. Runs in Italian,
  English, Spanish, French, and German. Portable if you want it to be.

## See it in action

| | |
|---|---|
| ![Guided setup](docs/images/wizard.png) | **Guided setup.** A few questions in plain language — including whether files stay open while you work — and the wizard configures the job for you. |
| ![Job editor](docs/images/editor.png) | **Everything under control.** Mirror or accumulate, open-file copying, large-file options: every choice explained in one line, with a hint where it matters. |
| ![Command preview and versioning](docs/images/editor-preview.png) | **Total transparency.** Dated versions with automatic cleanup, per-job exclusions, and the exact robocopy command always in view. |
| ![Run history](docs/images/history.png) | **Every run on record.** Backups and integrity checks side by side, filtered per job; double-click any entry to read its full log without touching a zip file. |
| ![Per-job scheduling](docs/images/editor-schedule.png) | **Set it and forget it.** Each job can have its own schedule — daily, weekly, or monthly — plus automatic integrity verification after every run. |
| ![Browse versions](docs/images/versions.png) | **Go back in time.** Pick a date, click, and that day's backup opens in File Explorer. Copy back whatever you need. |

## Get started in two minutes

1. Grab the latest version from the **[Releases](../../releases)** page:
   - **`…-selfcontained.zip`** — extract and run, **nothing to install** (bundles .NET, larger download);
   - **`…-framework-dependent.zip`** — much smaller, needs the free
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) installed once.
2. Extract the zip into any **writable folder** (e.g. `D:\Programs\RoboKeep` — avoid
   `C:\Program Files`).
3. Run **`RoboKeep.exe`**, click **New**, answer the wizard's questions, then **Run all**. Done.

Want it to run by itself? Give each job its own schedule right in the editor (daily, weekly, or
monthly), or use **Settings → Scheduling** for a single "run everything" task. Either way, it
runs even with the app closed.

## How a backup behaves

For each job (a source folder → destination folder pair, subfolders included), RoboKeep:

- **skips** files that haven't changed (that's why the second run takes seconds);
- **updates** files that are newer in the source;
- in **mirror** mode (default) also **removes** from the destination what you deleted from the
  source — the destination stays an exact copy;
- with mirror off, it only adds and updates, **never deletes**.

And if you enabled versions, each run first saves the previous state as a dated snapshot.

## All the features

**Backing up**
mirror or accumulate mode · **disk-rotation protection: a job runs only on its own disk,
identified by volume — never mirrors onto the wrong or absent one** · dated versions with
hard-links and configurable retention (no duplicate version when nothing changed) · open/locked
file copying via VSS · **integrity verification (SHA-256), on demand or periodic** ·
**hardware-error stop: kills the copy at the first CRC/bad-sector/I/O error and rests the disk**
· **automatic thread cap on mechanical disks** · **PC kept awake during backups** ·
multi-threaded copying · per-job file
and folder exclusions · "force copy" for files whose date/size never change (encrypted
containers, some databases), with an optional content-hash mode · restartable mode for huge
files · preview/dry-run

**Keeping you informed**
**run history with a log for every entry (verifications too), opened in Notepad** · **Log folder
button** · per-job health icons with plain-language tooltips, including a neutral "waiting for
its disk" state · **icons that refresh the instant you plug or unplug a disk** · pre-run checks
(destination reachable, disk space, VSS and versioning eligibility, **recent disk errors from the
Windows event log**) · real-time log · per-job zipped log archive with automatic cleanup · toast
notifications and system tray · email reports (SMTP), optionally only on errors

**Fitting your setup**
guided wizard or full manual editor · exact command preview · **per-job scheduling (daily /
weekly / monthly)** · network shares with encrypted credentials (Windows DPAPI) · **copy
throttling for network backups** · **configuration export/import** · command line for
automation · drag & drop job ordering · 5 languages · portable mode

## Good to know

- **Windows 10/11 only** — RoboKeep builds on robocopy and other Windows-native features.
- **Changed files are recopied whole** (no delta/block copy): fine for documents and photos,
  costly for single huge files that change daily.
- **Rotating disks are matched by identity, not letter.** Tie a job to its disk in the editor
  (**Protect with this disk**); from then on the job is skipped whenever a different disk — or no
  disk — is in that slot, so it never mirrors onto the wrong one. Jobs on internal or network
  destinations don't need it and aren't affected.
- **Versions need a local NTFS destination** (hard-links don't exist on exFAT or network shares).
- **Open-file copying needs a local NTFS source** and asks for one administrator confirmation
  (UAC) per run.
- **Integrity checks re-read every file on both sides**: thorough by design, so expect a check
  to take roughly as long as a first backup. That's why the automatic check runs every N days
  (7 by default) rather than after every backup; set 0 for every run.
- **A disk that reports a hardware error stops the job**, on purpose. Before re-running, check
  the cable, the USB enclosure and the power supply — on external disks they cause the very same
  errors as a failing disk — then the disk's SMART health. See the in-app guide's
  troubleshooting chapter.
- Your settings, results, and logs live in `%APPDATA%\RoboKeep`, so they survive app updates.
  Passwords are encrypted with Windows DPAPI, never stored in plain text. Note: the default
  encryption scope is **machine-wide** (so scheduled tasks can decrypt them too) — on a shared
  PC, switch the setting to per-user scope if other accounts shouldn't be able to read them.

## Privacy

RoboKeep collects **nothing**. No telemetry, no analytics, no update checks, no account, no
network traffic at all unless *you* configure email reports (SMTP server of your choice — TLS
on by default). Everything the app knows — job settings, results, history, logs — lives in
local files on your PC, readable JSON you can inspect at any time. Logs contain the paths of
the files that were copied and are cleaned up automatically after 30 days (configurable).

## For power users

```text
RoboKeep.exe --run-all              run all enabled jobs (exit code 0 = all good)
RoboKeep.exe --job "Documents"      run a single job
RoboKeep.exe --run-all --dry-run    preview only, nothing changes
RoboKeep.exe --job "Photos" --config "D:\path\config.json"
```

Build from source: `dotnet build src/RoboKeep.sln -c Release` (requires the .NET 10 SDK) —
the test suite runs with `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`.
Portable mode: create an empty `portable.flag` file next to the exe and everything (config,
logs, results) stays in the app folder. Sample config:
[config/config.example.json](config/config.example.json). Project background and technical
decisions: [ANALISI.md](ANALISI.md) *(in Italian)*.

## Contributing

Translations into other languages are community-maintained. Spotted a wording that could read
more naturally, or a term a native speaker would phrase differently? [Pull requests are
welcome](../../pulls) — even a one-line fix helps.

## License

Distributed under the **MIT License** — see [LICENSE](LICENSE). Third-party components are
listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). © 2026 Roberto Serafini.
