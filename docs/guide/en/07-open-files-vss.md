# Copying open files (VSS)

Some files won't let you copy them while you're using them: an Outlook archive, a database, a
document another program is holding open. Windows **locks** them, and a normal copy skips them or
fails. RoboKeep has a way around it.

## The problem

When a program keeps a file open, that file is locked: nothing else can read it in full
reliably. It's why backups of "live" data — mail, business apps, databases — often leave behind
exactly the files that matter most.

## The solution: a shadow copy

Tick the **"Also copy open files (VSS)"** checkbox in the job. Before copying, RoboKeep asks
Windows for a **shadow copy** (VSS): a frozen snapshot of the disk at that instant. The backup
reads from that snapshot rather than from the live files — so even locked files get copied
consistently, as if photographed at a single moment.

## What it needs

- A **local NTFS source**: shadow copy is a Windows feature for local disks, not network shares.
- **One administrator confirmation (UAC) per run**: taking the snapshot needs the right
  privileges, so Windows will prompt you when the run starts. That's expected.

## If the snapshot isn't possible

Not every environment allows a shadow copy. If for any reason the snapshot fails, the backup
**continues the normal way, with a warning**: it copies what it can and tells you that open files
may not be included. It **never** blocks for this reason — a backup with a warning beats no backup.

> Use this option where you really do have files always open (mail, databases). Where files are
> closed during the backup, you don't need it.
