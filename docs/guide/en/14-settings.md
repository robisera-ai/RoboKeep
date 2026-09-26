# Settings

**Settings** gather the choices that apply to the whole app, not to a single job. You open them from the top toolbar. Here's what's inside.

## Language

RoboKeep speaks **five languages**: Italian, English, Spanish, French, German. Pick yours and the interface changes right away.

## Logs and folders

- **Log folder**: where RoboKeep archives the record of every run.
- **Retention days**: how many days to keep logs before automatic cleanup.
- **Temporary folder**: the workspace the app uses during operations.

## Credential encryption (DPAPI)

Passwords (network shares, email sender) are encrypted with **Windows DPAPI**, never in plain text. Here you choose the encryption **scope**:

- **Machine-wide** (default): any account on the PC can decrypt them. It has to be this way so that **scheduled tasks** can read the credentials and run with the app closed.
- **Per-user**: only your account can decrypt them. Recommended on a **shared PC**, where other users shouldn't be able to read them.

> If you change this option, passwords already saved are **re-encrypted automatically** with the new scope. You don't have to re-enter them.

## Copy of the configuration on the backup disks

The **"Save a copy of the configuration on the backup disks"** checkbox (on by default) makes RoboKeep write, after every successful backup, a **`RoboKeep-config`** folder in the **root of the destination disk**, with two files:

- **`config.json`**: the whole configuration — jobs, exclusions, schedules, settings and email — in the same format as *Export configuration*;
- **`LEGGIMI.txt`**: in Italian and English, what that folder is, which PC it came from, when it was written and how to restore it.

It covers one case only, but a decisive one: **the PC is gone**. With the disk in your hands you have the files *and* the jobs; you install RoboKeep on the new PC, open *Settings → Scheduling → Import configuration*, pick that `config.json` and everything is back as it was. Without the copy, the jobs would have to be rebuilt from memory.

Things to know:

- one copy **per disk**, rewritten after every successful backup: if several jobs write to the same disk, the last one to finish wins;
- **passwords** (network shares, email) stay encrypted with DPAPI and can only be decrypted on that PC — and only by your Windows user, if you chose to encrypt them for your user: on another PC you type them in once, the rest comes back on its own;
- **network** destinations are excluded: the copy is about the disk you unplug and carry away, not a server's root;
- no mirror deletes it, because it lives **outside** the job folders; and if a job's destination is the root of the disk itself, RoboKeep excludes `RoboKeep-config` from the copy so it is not removed;
- if the copy fails (read-only disk, no space, permissions) all you get is a line in the log: **the file backup does not fail because of it**.

## Notifications and system tray

From here you turn on **toast notifications** and the **tray** behaviors (minimize to tray, start minimized, run in background). The details are in the *Notifications and email* chapter.

## Stale-backup warning

The **"warn if a backup hasn't run in N days"** threshold raises an alert icon on jobs that haven't run in too long. Set it to **0** to never get this warning.

## Updates

The **"Check for updates automatically"** checkbox decides whether RoboKeep checks on its own for a newer version: one request to `api.github.com` at startup, at most once a day. If it finds a newer version, the notice appears in the main window with **What's new**, **Download** and **Ignore**. Off, RoboKeep never contacts GitHub on its own.

The **About** tab also has a **"Check now"** button, which checks right away — it works even with the checkbox off, because it's an explicit action you decide in the moment. The result appears under the button: *you are up to date*, or *version X is available* (with the notice appearing in the main window), or *check failed* if there's no network or GitHub can't be reached.

## Disk health

The **"Disk health"** button, in the main window's toolbar, reads the SMART data of every physical disk and shows an explained verdict. SATA and USB disks need one administrator prompt per reading; the **Refresh** button in the window repeats the reading. What the values mean is in the *Troubleshooting* chapter.

## Scheduled tasks

The **"Scheduled tasks"** button, next to *Disk health*, shows the tasks RoboKeep registered in the Windows Task Scheduler: one for every job with a schedule, plus the one for *Run all*. For each you see the job it belongs to, the next run, the last run and its result, and the registered command. **Orphan** tasks — the ones with no job left, or that start another copy of RoboKeep — are highlighted and can be **deleted** from here; deleting the task of a job that still exists leaves that job without an automatic schedule. Nothing is edited: you change the schedule in the job editor, which rewrites the task when you save. Avoid deleting or editing RoboKeep's tasks directly in the Windows Task Scheduler: job and task would go out of sync. The button is off when there are no RoboKeep tasks.

## Export and import configuration

You can save your whole configuration to a file and load it back elsewhere.

- **Export**: writes settings and jobs to a file.
- **Import**: first **validates** the file; then saves a **backup** of your current configuration; finally **re-syncs the scheduled tasks** with the new jobs.

## Where settings live

Everything lives in **`%APPDATA%\RoboKeep`**, so it survives app updates. In **portable mode**, it lives in the app folder instead.
