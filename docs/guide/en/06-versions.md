# Versions

Versions are a **time machine** for your files. With versions on, before overwriting or deleting
anything RoboKeep sets the previous state aside in a **dated folder**. Deleted a paragraph last
Tuesday? Open Tuesday's version and get it back.

## How it works

Versions are turned on **per job**: enable them where you need them, leave them off where you
don't. From then on, each run first sets aside the previous state of the files it is about to
replace or delete, then updates the backup.

It works on **any disk**: NTFS, exFAT, FAT32 (that's how USB sticks and many external drives come
from the factory) and network shares. There is nothing to configure.

The current backup lives in a **`current`** folder, and every version holds **only the files that
backup replaced or deleted**:

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

This is the model of Windows *File History* and of `rsync --backup-dir`. Setting a file aside is a
same-disk move: instant, one write per changed file, and no work at all for untouched ones. That is
why a dated folder is **not** that day's complete tree: it is the difference. If you look inside and
find three files, that isn't an incomplete backup — the others hadn't changed, and they're in
`current`.

### A backup that only adds files creates no folder

If a backup **replaced and deleted nothing** — the normal case for a photo or document archive,
which only grows — there is no previous state to keep, so RoboKeep **does not create the folder**:
only its `.manifest.json` remains, which weighs nothing and tells the restore which files did not
exist yet at that date. This is not cosmetic: if every backup created an (empty) folder, then with
**"keep the last 3 versions"** three days of pure additions would be enough for retention to delete
the one folder that actually held something — the copy of a file gone from the source. By counting
folders only, "keep N versions" counts **N real versions**.

## Retention: how many versions to keep

Versions don't pile up forever. In the job you set the **retention**:

- keep the last **N versions**, and/or
- keep versions up to a **maximum age in days**.

The oldest ones are removed automatically, so you never have to think about it. A new job starts
with **10 versions**; you can change the number, or set **0** for no limit (not recommended: sooner
or later the versions fill up the disk). The age limit never touches the most recent version: it is
the previous copy closest to today, even if the files haven't changed for months.

Deleting a version loses the states **older** than the one after it. That is exactly what "keep the
last N versions" means — you can go back as far as the oldest one you kept, and no further. The
backup in `current` is never touched.

## When the disk is full

Sooner or later the backup disk fills up. RoboKeep doesn't leave you guessing: if a backup stops for
lack of space, the job doesn't show an error code but **"Disk full"**, and the log, the warning
tooltip and the email say which disk, **how many versions** there are and **how much they take up**.
From there you decide: lower **"Max number of versions"** in the job editor — the next run deletes
the oldest ones — or move to a roomier disk. If you need space right now, delete a few old versions
by hand in the destination's `versions` folder (never `current`, which is the backup). The
*Versions...* window itself deletes nothing: it shows and opens.

If you'd rather not think about it, **Settings → Reliability** has the checkbox **"When the backup
disk is full, delete the oldest versions to make room (never the latest)"**. With it on, before every
backup with versions RoboKeep looks at the free space: if it is below the **"Minimum free space at
destination"** threshold (10 GB by default, in the same tab), it deletes that job's oldest version,
checks the space again, and keeps going until it is back above the threshold or **only the most
recent one is left**, which is never touched. Every deletion is a line in the log, with the name of
the version removed and how much space it freed.

The checkbox is **off by default**: deleting to make room is your decision, not the program's
initiative. And if even after the cleanup there still isn't enough space, the backup runs anyway and,
if it fails, tells you about it as above.

## Turning versions on for an existing job

If a job has been making a plain copy so far and you turn versions on, the backup you already have
is neither lost nor redone: on the first run RoboKeep **adopts the existing copy**, moving it into
the `current` folder. It's a same-disk move: instant, nothing recopied. It says so in the log.

If the destination holds files or folders that **don't exist in the source**, RoboKeep adopts
nothing and says so: that isn't a copy made by this job, and moving it would mean moving someone
else's things. Those files stay where they are, outside `current`, and no backup touches them; the
first backup copies everything from scratch into `current`.

## If a backup stops halfway

The files set aside are **moved** out of `current`: for those deleted from the source, that is the
only copy left. So if the backup fails, or you cancel it halfway, the version is **not thrown
away**: it still becomes a real version — incomplete, but holding exactly the previous states of
the files it had already moved — and the log says so. The next backup picks up from there and puts
`current` right again by recopying from the source. If an interrupted run left a half-made folder
behind, the next run **recovers** it instead of deleting it.

## No empty versions

If **nothing has changed** since the last run, RoboKeep **doesn't create a new version**: it says so
in the log, and the backup still counts as successful and up to date. An empty folder for every
backup would fill the destination with entries that tell you nothing.

## Browsing versions

When you need to recover something:

1. Select the job and click **Versions...**.
2. Pick the **date** you want.
3. That version opens in **File Explorer**.
4. Copy back whatever files or folders you need.

No special format, no extraction: they're ordinary files, exactly as they were before that backup.
The window reminds you: the dated folder holds the files that backup **replaced or deleted**, and
everything else is in `current`.

To get the **whole tree back as it was on a date**, rather than just looking at what changed that
day, use **Restore...** — from the toolbar or from the *Versions...* window. The *Restoring*
chapter covers it.

> Versions pair nicely with *integrity verification*: jobs with versions verify the `current`
> folder, the real backup. See the dedicated chapter.
