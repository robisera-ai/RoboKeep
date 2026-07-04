# RoboKeep

*Read in: English · [Italiano](readmeita.md)*

**Set it up once — every file, every version, safe on your own disk.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Languages](https://img.shields.io/badge/languages-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Downloads](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep is a friendly Windows app that turns `robocopy` — the rock-solid copy engine already
built into every Windows PC — into a **real backup tool**: point-and-click setup, **dated
versions** of your files, copying of **files you're still working on**, and clear alerts when a
backup falls behind. **No cloud, no account, no subscription** — your files never leave your disks.

![RoboKeep main window: jobs with results at a glance and a live execution log](docs/images/main-window.png)

## Why you'll like it

- 🧙 **Answer a few questions, get the right backup.** No robocopy knowledge needed: the guided
  setup asks about your disks and your data in plain language, and picks the optimal settings
  for you. Experts can still tweak everything by hand.
- 🕰️ **A time machine for your files.** Every run can save a **dated version** of your backup.
  Deleted a paragraph last Tuesday? Open Tuesday's version and get it back. Smart trick under the
  hood: unchanged files are *shared* between versions, so ten versions don't cost ten times the
  space — only what actually changed.
- 🔓 **Copies files even while you're using them** *(new in 1.3)*. Outlook archives, databases,
  files locked by other programs: with one checkbox, RoboKeep photographs the disk for an instant
  (a Windows "shadow copy") and copies from that frozen picture. And if the snapshot isn't
  possible, the backup simply continues the normal way — it never blocks.
- 🚨 **It tells you when something's wrong.** A backup that fails silently is worse than no
  backup. RoboKeep marks each problematic job with a colored warning icon — red for failed,
  amber for "not run in too long", orange for "was interrupted" — with a plain explanation on hover.
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
| ![Browse versions](docs/images/versions.png) | **Go back in time.** Pick a date, click, and that day's backup opens in File Explorer. Copy back whatever you need. |

## Get started in two minutes

1. Grab the latest version from the **[Releases](../../releases)** page:
   - **`…-selfcontained.zip`** — extract and run, **nothing to install** (bundles .NET, larger download);
   - **`…-framework-dependent.zip`** — much smaller, needs the free
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) installed once.
2. Extract the zip into any **writable folder** (e.g. `D:\Programs\RoboKeep` — avoid
   `C:\Program Files`).
3. Run **`RoboKeep.exe`**, click **New**, answer the wizard's questions, then **Run all**. Done.

Want it to run by itself? **Settings → Scheduling** creates a Windows scheduled task that runs
all your jobs at the time you choose — even with the app closed.

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
mirror or accumulate mode · dated versions with hard-links and configurable retention ·
open/locked file copying via VSS · multi-threaded copying · per-job file and folder exclusions ·
"force copy" for files whose date/size never change (encrypted containers, some databases), with
an optional content-hash mode · restartable mode for huge files · preview/dry-run

**Keeping you informed**
per-job health icons with plain-language tooltips · pre-run checks (destination reachable, disk
space, VSS and versioning eligibility) · real-time log · per-job zipped log archive with
automatic cleanup · toast notifications and system tray · email reports (SMTP), optionally only
on errors

**Fitting your setup**
guided wizard or full manual editor · exact command preview · network shares with encrypted
credentials (Windows DPAPI) · scheduling via Windows Task Scheduler · command line for automation ·
drag & drop job ordering · 5 languages · portable mode

## Good to know

- **Windows 10/11 only** — RoboKeep builds on robocopy and other Windows-native features.
- **Changed files are recopied whole** (no delta/block copy): fine for documents and photos,
  costly for single huge files that change daily.
- **Versions need a local NTFS destination** (hard-links don't exist on exFAT or network shares).
- **Open-file copying needs a local NTFS source** and asks for one administrator confirmation
  (UAC) per run.
- Your settings, results, and logs live in `%APPDATA%\RoboKeep`, so they survive app updates.
  Passwords are encrypted with Windows DPAPI, never stored in plain text.

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

## License

Distributed under the **MIT License** — see [LICENSE](LICENSE). © 2026 Roberto Serafini.
