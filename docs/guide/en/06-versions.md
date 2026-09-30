# Versions

Versions are a **time machine** for your files. With versions on, before overwriting anything
RoboKeep sets the previous state aside in a **dated folder**. Deleted a paragraph last Tuesday?
Open Tuesday's version and get it back.

## How it works

Versions are turned on **per job**: enable them where you need them, leave them off where you
don't. From then on, each run first saves the previous state, then updates the backup.

*How* it saves it depends on the destination disk, and RoboKeep decides that by itself. There are
two models, and there is nothing to configure.

## The two models

### Complete folders via hard links (local NTFS)

On a **local NTFS** disk every version is that day's **whole folder**: open it and you find the
entire backup as it was, not just the changed files.

The natural worry is: won't ten versions take ten times the space? No. Files that stayed
**unchanged** from one version to the next aren't duplicated — they're **shared** through hard
links, meaning several dated folders point to the very same file on disk. So ten versions don't
cost ten times the space — you only pay for what **actually changed**.

```
E:\Backup\Documents\
  2026-09-27_213000\      the whole backup as it was that day
  2026-09-28_213000\      likewise, and unchanged files are the same file on disk
```

### Differential (exFAT, FAT32, network)

Hard links exist only on NTFS. On an **exFAT** or **FAT32** disk (that's how USB sticks and many
external drives come from the factory) and on a **network share** there are none — but there are
still versions. With this model the current backup lives in a **`current`** folder, and every
version holds **only the files that backup replaced or deleted**:

```
E:\Backup\Documents\
  current\                          the backup as of now: always complete and up to date
  versions\
    2026-09-27_213000\              the files as they were BEFORE that date's backup
      sub\letter.docx               (same relative path they have in current)
    2026-09-27_213000.manifest.json the list of what that backup changed
    2026-09-28_213000\
    2026-09-28_213000.manifest.json
```

The `.manifest.json` file sits **next to** the folder, not inside it: that way no file of yours can
land on top of it, not even one actually called `_manifest.json`. Deleting it loses no files, only
the list the restore needs in order to know what that backup had changed.

The names **`current`** and **`versions`** at the root of a versioned destination are **reserved**
by RoboKeep. If your source has a top-level folder with either name, pick a different destination
subfolder: RoboKeep notices, moves nothing and says so in the log, but the backup would be left
without versions.

This is the model of Windows *File History* and of `rsync --backup-dir`. It costs one write per
changed file instead of the thousands of metadata writes it takes to clone a hard-link tree: on a
mechanical disk that is far lighter. In exchange, a dated folder is **not** that day's complete
tree: it is the difference. If you look inside and find three files, that isn't an incomplete
backup — the others hadn't changed, and they're in `current`.

### A backup that only adds files creates no folder

If a backup **replaced and deleted nothing** — the normal case for a photo or document archive,
which only grows — there is no previous state to keep, so RoboKeep **does not create the folder**:
only its `.manifest.json` remains, which weighs nothing and tells the restore which files did not
exist yet at that date. This is not cosmetic: if every backup created an (empty) folder, then with
**"keep the last 3 versions"** three days of pure additions would be enough for retention to delete
the one folder that actually held something — the copy of a file gone from the source. By counting
folders only, "keep N versions" counts **N real versions**.

### How to tell which one you have

Two ways, pick whichever is handier:

- in the **job editor**, under the *"Keep dated versions"* checkbox, a line spells it out for the
  destination you picked (it updates by itself when you change it);
- look at the **destination** in File Explorer: folders with a date in the name mean the hard-link
  model; `current` and `versions` mean the differential one.

The model is decided **once**, on the first run, and then **never changes**: if a backup is already
going, RoboKeep respects the layout it finds, even if the disk could meanwhile allow the other one.
Switching it underneath would make the versions you already have unreachable.

## Retention: how many versions to keep

Versions don't pile up forever. In the job you set the **retention**, the same for both models:

- keep the last **N versions**, and/or
- keep versions up to a **maximum age in days**.

The oldest ones are removed automatically, so you never have to think about it. A new job starts
with **10 versions**; you can change the number, or set **0** for no limit (not recommended: the
disk fills up with entries and slows down). The age limit never touches the most recent version:
that one is your current backup, even if the files haven't changed for months.

One note on the differential model: deleting a version loses the states **older** than the one
after it. That is exactly what "keep the last N versions" means — you can go back as far as the
oldest one you kept, and no further.

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
is neither lost nor redone: on the first run RoboKeep **adopts the existing copy**, moving it into a
dated folder (hard-link model) or into the `current` folder (differential model). It's a same-disk
move: instant, nothing recopied. It says so in the log.

If the destination holds files that **don't exist in the source**, RoboKeep adopts nothing and says
so: that isn't a copy made by this job, and moving it would mean moving someone else's things.
Those files stay where they are and the backup starts from scratch.

## If a backup stops halfway

With the differential model the files set aside are **moved** out of `current`: for those deleted
from the source, that is the only copy left. So if the backup fails, or you cancel it halfway, the
version is **not thrown away**: it still becomes a real version — incomplete, but holding exactly
the previous states of the files it had already moved — and the log says so. The next backup picks
up from there and puts `current` right again by recopying from the source. If an interrupted run
left a half-made folder behind, the next run **recovers** it instead of deleting it.

## No duplicate versions

If **nothing has changed** since the last run, RoboKeep **doesn't create a new version**: it says so
in the log, and the backup still counts as successful and up to date. It's not just tidiness: with
the hard-link model, creating a version means writing an entry on the disk for **every** file, even
untouched ones, and it's the heaviest work RoboKeep asks of a mechanical disk. Doing it only when
needed extends the disk's life.

## Browsing versions

When you need to recover something:

1. Select the job and click **Versions...**.
2. Pick the **date** you want.
3. That version opens in **File Explorer**.
4. Copy back whatever files or folders you need.

No special format, no extraction: they're ordinary files, exactly as they were that day. With the
differential model the window reminds you: the dated folder holds the files that backup **replaced
or deleted**, and everything else is in `current`.

To get the **whole tree back as it was on a date**, rather than just looking at what changed that
day, use **Restore...** — from the toolbar or from the *Versions...* window. The *Restoring*
chapter covers it.

> Versions pair nicely with *integrity verification*: jobs with versions verify the latest snapshot
> (hard-link model) or the `current` folder (differential model). See the dedicated chapter.
