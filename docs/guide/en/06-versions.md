# Versions

Versions are a **time machine** for your files. With versions on, before overwriting anything
RoboKeep sets the previous state aside as a **dated snapshot**. Deleted a paragraph last Tuesday?
Open Tuesday's version and get it back.

## How it works

Versions are turned on **per job**: enable them where you need them, leave them off where you
don't. From then on, each run first saves the previous copy as a dated snapshot, then updates the
backup.

## Why they cost little space

The natural worry is: won't ten versions take ten times the space? No. Files that stayed
**unchanged** from one version to the next aren't duplicated — they're **shared** through
hard-links, meaning several versions point to the very same file on disk. So ten versions don't
cost ten times the space — you only pay for what **actually changed**.

That's exactly why versions need a **local NTFS destination**: hard-links don't exist on exFAT or
on network shares. If the destination isn't eligible, RoboKeep tells you before it starts.

## Retention: how many versions to keep

Snapshots don't pile up forever. In the job you set the **retention**:

- keep the last **N versions**, and/or
- keep versions up to a **maximum age in days**.

The oldest ones are removed automatically, so you never have to think about it. A new job starts
with **10 versions**; you can change the number, or set **0** for no limit (not recommended: the
disk fills up with entries and slows down). The age limit never touches the most recent version:
that one is your current backup, even if the files haven't changed for months.

## When the disk is full

Sooner or later the backup disk fills up. RoboKeep doesn't leave you guessing: if a backup stops for
lack of space, the job doesn't show an error code but **"Disk full"**, and the log, the warning
tooltip and the email say which disk, **how many versions** there are and **how much they really
take up** — each physical file counted once, because adding up the sizes of the dated folders would,
with hard-links, give a number far larger than the truth. From there you decide: lower **"Max number
of versions"** in the job editor — the next run deletes the oldest ones — or move to a roomier disk.
If you need space right now, **Versions...** → **Open in File Explorer** and delete a few dated
folders by hand (the Versions window itself deletes nothing: it shows and opens).

If you'd rather not think about it, **Settings → Reliability** has the checkbox **"When the backup
disk is full, delete the oldest versions to make room (never the latest)"**. With it on, before every
backup with versions RoboKeep looks at the free space: if it is below the **"Minimum free space at
destination"** threshold (10 GB by default, in the same tab), it deletes that job's oldest version,
checks the space again, and keeps going until it is back above the threshold or **only the most
recent one is left** — that one is your current backup and is never touched. Every deletion is a line
in the log, with the name of the version removed and how much space it freed.

The checkbox is **off by default**: deleting to make room is your decision, not the program's
initiative. And if even after the cleanup there still isn't enough space, the backup runs anyway and,
if it fails, tells you about it as above.

## Turning versions on for an existing job

If a job has been making a plain copy so far and you turn versions on, the backup you already have
is neither lost nor redone: on the first run RoboKeep **adopts the existing copy as the first
version**, moving it into a dated folder (a same-disk move: instant, nothing recopied). From then
on every version costs only what changed. It says so in the log.

## No duplicate versions

If **nothing has changed** since the last run, RoboKeep **doesn't create a new version** identical
to the previous one: it says so in the log, and the backup still counts as successful and up to
date. It's not just tidiness: creating a version means writing an entry on the disk for **every**
file, even untouched ones, and it's the heaviest work RoboKeep asks of a mechanical disk. Doing it
only when needed extends the disk's life.

## Browsing versions

When you need to recover something:

1. Select the job and click **Versions...**.
2. Pick the **date** you want.
3. That version opens in **File Explorer**.
4. Copy back whatever files or folders you need.

No special format, no extraction: they're ordinary files, exactly as they were that day.

> Versions pair nicely with *integrity verification*: jobs with versions verify the latest
> snapshot. See the dedicated chapter.
