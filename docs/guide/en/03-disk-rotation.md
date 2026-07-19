# Disk rotation

If you back up to **several external disks that you connect in turn**, this chapter matters to you:
it protects against the most insidious kind of data loss.

## The problem

Windows assigns drive letters in the order you connect disks. So two different external disks can
receive, at different times, the **same letter** — for example `E:` — with the **same destination
folders**.

Picture a *mirror* job set to write to `E:\Backup`. If you connect the wrong disk, that job would
see `E:\Backup` on the wrong disk and — being a mirror — make it identical to the source,
**deleting whatever it finds there**. On a disk that might hold your historical versions, that's a
disaster.

## The solution: recognize the disk, not the letter

RoboKeep identifies each disk by its **volume** — a unique code assigned at format time that does
*not* change with the letter. You can tie a job to a specific disk: from then on, if that disk
isn't in the slot, the job is **skipped without touching anything**.

## How to protect a job

1. Connect the **right** disk (the one that job should write to).
2. Open the job with **Edit**. Below the destination you'll find the **Disk** row.
3. If the job isn't protected yet, click **Protect with this disk**: the disk's label appears and
   the job is now tied to that volume.
4. Save.

Repeat for every job that lives on a rotating disk. Jobs on internal disks or network folders
don't need it.

## What you see when the disk changes

- **Right disk connected** → the job runs normally.
- **Wrong disk, or no disk** → the job comes up as **Skipped**, with a clear message like *"writes
  to disk BACKUP1, but the connected disk is OTHER"*. No error, nothing deleted.
- In the list, a job waiting for its disk shows a **grey hourglass** ("waiting"), not a red alarm:
  it isn't overdue, it's just waiting for its disk.

> The icons refresh on their own when you connect or disconnect a disk — no need to press
> *Refresh*.

## Moving a job to a different disk

If you really want to reassign a job to a different disk, open it with that disk connected: you'll
see the warning *"Disk … is connected now"* and the **Use this disk** button. It's the only way to
reassociate without changing the destination — deliberately, so that an ordinary save can't move a
job to the wrong disk by mistake.
