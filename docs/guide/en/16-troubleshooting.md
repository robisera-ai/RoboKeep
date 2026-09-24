# Troubleshooting and FAQ

The most common situations, with the short answer. If yours isn't here, the chapter on that topic almost always has the detail.

### A job comes up as "Skipped"

That's **normal** with disk rotation, not an error. The job writes to a specific disk, and right now another disk — or none — is in the slot. Connect the right disk and it picks up on its own. See *Disk rotation*.

### A UAC prompt appears

That's **VSS**: copying open files photographs the disk for an instant and asks for **one administrator confirmation** per run. Confirm and the backup continues. See *Copying open files (VSS)*.

### Versions aren't working

Dated versions use hard-links, which exist only on **local NTFS**. You need an **NTFS** destination on a **local disk**: on exFAT or a network share they aren't possible. See *Versions*.

### SmartScreen says "unknown publisher"

The app **isn't digitally signed yet**, so Windows SmartScreen warns you. It's **safe**: click **"More info"** and then **"Run anyway"**.

### "Unexpected error" appears

Something in the app went wrong where it wasn't expected. RoboKeep **stays open** and writes the details to `crash.log` in the data folder (`%APPDATA%\RoboKeep`, or the app folder in portable mode). If something stops responding, close and reopen the app. The file is useful when reporting the problem: it holds the date, the version and the error trace, no personal data beyond any paths involved.

### A scheduled task doesn't start

Check that the credentials' **DPAPI encryption scope** is **machine-wide**: only then can the scheduled task decrypt the credentials and run with the app closed. With per-user scope, the task can't read them. See *Settings*.

### A task runs but the job no longer exists

RoboKeep deletes the Windows task when you delete or rename a job, but it can't always get there: if `config.json` was deleted by hand, if a portable copy runs from another folder, or if the job was renamed directly in the file, the task is left **orphaned** and still runs at night. Open **"Scheduled tasks"** from the main window's toolbar: orphans are highlighted — *no job by this name* or *points to another copy of RoboKeep* — and you remove them with **Delete**. If you delete the task of a job that still exists, the job loses its automatic schedule (the editor will show "None"): you can set it again any time from the job editor. The reverse holds too: if a scheduled job has lost its task (deleted from Task Scheduler or by another copy of RoboKeep), RoboKeep tells you at startup and asks whether to recreate it or drop the schedule.

### "Destination unreachable"

The destination disk or share **isn't connected**. Plug in the external disk or check the network connection, then try again.

### Verification says "modified after the backup"

That's not **corruption**. It means you **edited the files after copying them**: they're newer in the source than in the copy. RoboKeep reports it this way on purpose, so it doesn't scare you with a false alarm. The next backup brings them back in sync.

### A job stops with "HARDWARE ERROR" (CRC, cyclic redundancy check)

The error comes from Windows, not from RoboKeep, and means **a disk — or its connection — reported a physical problem**: data error (CRC), sector not found, or I/O device error. There are two possible causes, and they need telling apart: a **failing disk** (bad sectors), or a faulty **cable, USB enclosure or power supply**, which on an external disk produce exactly the same messages.

When this happens RoboKeep **stops immediately**, at the first error, and marks the job as failed: carrying on reading and writing to media that reports physical errors makes the damage worse. For the same reason, the other jobs of the same session that use that disk **are not started**. Don't keep re-running the backup: check first. The disk **stays rested on the following days too** (scheduled backups included) until you re-enable it with the **Re-enable disks** button in the main window, which appears only when needed; as a safety net it re-enables itself after 7 days. The result email is titled "HARDWARE ERROR" and leads with what happened and what to check.

1. **Cable and power** — try another cable and another USB port (preferably a rear port, no hub). If the disk has its own power supply, check that too.
2. **Disk health** — with a tool like **CrystalDiskInfo** (free), look at *05 Reallocated Sectors*, *C5 Current Pending Sectors* and *C6 Uncorrectable Sectors*: if they're above zero, or the status is "Caution", **the disk is failing**. A rising *C7 UltraDMA CRC Error Count* with the others at zero points to the **link between the enclosure and the disk** instead. Note that a faulty **USB cable** leaves no trace in SMART at all, so a C7 of zero doesn't clear it. And be wary of cheap enclosures: some report the model, serial and thresholds of a **different** disk — in that case only the *raw values* can be trusted.

### "Recent disk errors" shows up before a backup

A USB disk's SMART data can't be read without administrator rights, and without them Windows reports even a disk with pending sectors as "healthy". So RoboKeep looks at the **Windows event log**, which keeps bad blocks, I/O errors and lost writes for weeks: these are the signs that usually **precede** damage, sometimes by a month. If it finds any for the source or destination disk in the last 14 days, it tells you before starting (and, in scheduled backups, writes it at the end of the log). To read that disk's SMART data for real there is the **"Disk health"** button, which asks for administrator approval when needed: see *Reading disk health*, just below.

The warning **doesn't block** the backup, because the log names disks by letter and number, not by identity: if you alternate two disks on the same letter, one disk's errors can show up while the other is connected. Take it for what it is — a good reason to check cable, enclosure, power and SMART right away.

If the failing disk is the backup disk, **stop using it** and switch to a healthy one: your original data is safe in the source, and the copies can be rebuilt. If it's the **source** disk instead, rescue your data before anything else.

### Reading disk health

The **"Disk health"** button, in the main window's toolbar, reads the SMART data of every connected physical disk and shows a plain-language verdict, with the values that matter explained one by one. For **NVMe** disks the reading needs no prompt; **SATA and USB** disks need **one administrator prompt** (once per reading: it's the only way through USB enclosures). It's a **read-only** operation: no SMART test is started, nothing is written to the disks.

The verdict is one of:

- **Good** — no critical value.
- **Caution** — a value worth watching (pending sectors, wear, high temperature).
- **Danger** — the disk is failing: back up your data and replace it soon.

The values that matter, for ATA/SATA/USB disks:

- **05 Reallocated sectors** — above zero the disk is failing: replace it.
- **C5 Pending sectors** — unreadable sectors not yet rewritten, often the sign of **interrupted writes** (a cable pulled out, lost power) rather than a fault. A **full format** rewrites them; if reallocated (05) then rises, the disk really is failing. This happened to a disk in this very project: 17 pending sectors with 05 at zero, gone after a full format.
- **C6 Uncorrectable sectors** — data lost for good in those sectors: replace the disk.
- **C7 CRC errors on the link** — transmission errors between the enclosure (or controller) and the disk, not on the disk itself. The row **appears only when the value is greater than zero**: on a healthy link it would have nothing to say. A **faulty USB cable leaves no trace here**: a C7 of zero doesn't clear it.
- **BB Reported uncorrectable errors** — cumulative history since the disk was new. What matters isn't the absolute number, but whether it **grows** between one reading and the next.
- **Temperature** — a mechanical disk above **50 °C** suffers; an NVMe is fine up to **70 °C**.

**NVMe** disks show different values: **critical warning** (anything but zero is serious), **available spare** against the manufacturer's threshold (below threshold is serious), **media errors** (serious), **percentage used** (caution past 90%), **unsafe shutdowns** (informational).
