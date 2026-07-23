# The job editor

The editor is where you define a backup in detail. You reach it from **New** — which opens the
guided setup or, if you skip it, an empty editor — or from **Edit** on an existing job.

## The main fields

- **Name** — what you call this job. It shows in the list and in the history.
- **Source** (+ *Browse*) — the folder to save, subfolders included.
- **Destination** (+ *Browse*) — the folder your files end up in.
- **Disk** — the row below the destination, to tie the job to a specific external disk. See
  *Disk rotation*.

## The options

- **Mirror** — the destination stays an exact copy of the source. See *Mirror or accumulate*.
- **Don't overwrite newer files in the destination** — if a newer version already sits in the
  destination, leave it be.
- **Copy ACLs/owner too** — carries file permissions and ownership along; handy on network
  shares.
- **Multi-thread** — copies several files in parallel, faster with lots of small files. It helps on
  **SSDs**; on a **mechanical disk** a high value makes the head jump constantly between files and
  often slows things down, so keep it low there (2-4).
- **Optimize large files** — a mode meant for very big files.
- **Restartable copy** — if copying a huge file is interrupted, it resumes where it left off.
- **Log every file** — records every copied file, not just the summary.
- **Retries** and **Wait (seconds)** — how many times to retry a locked file, and how long to
  wait between attempts.
- **File** and **folder exclusions** — what to skip, one entry per line.
- **Force copy** — always recopies files whose date and size never change (encrypted containers,
  some databases), which would otherwise be skipped. There's an optional **content-comparison**
  mode that decides based on what's actually inside the file.
- **Copy throttling** — caps the speed: handy to avoid saturating the network during a backup to
  a share.

## The command preview

At the bottom of the editor you always see a **preview of the exact command** that will run.
Change an option and you watch it update: no hidden magic, you always know what runs.

## Features with a chapter of their own

Some of the editor's options are covered separately, in full:

- *Versions*
- *Automatic scheduling*
- *Copying open files (VSS)*
- *Integrity verification*
- *Network backups and credentials*
