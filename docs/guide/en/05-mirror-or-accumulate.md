# Mirror or accumulate

It's a job's most important choice, and the easiest to grasp: it decides **what happens in the
destination to the files you delete from the source**.

## What both do

In either case RoboKeep **skips files that haven't changed** (that's why the second run takes a
few seconds) and **updates the ones that are newer** in the source. There's just one difference.

## Mirror (default)

Mirror makes the destination an **exact copy** of the source. Besides adding and updating, it
**removes from the destination what you deleted from the source**.

Use it when you want the copy to faithfully reflect how the source is *right now*: a second disk
always in sync, with no leftover old files.

## Accumulate (mirror off)

With mirror off, RoboKeep **only adds and updates**, and **never deletes** anything in the
destination. If you remove a file from the source, it stays in the destination.

Use it when the destination should *keep everything*, even what's no longer in the source — an
archive that grows over time, for instance.

## Careful: mirror deletes

Mirror is powerful precisely because it deletes, but that's also where damage gets done. **The
first time, and any time you're unsure, click *Preview***: RoboKeep lists what *would* be copied
or deleted, without touching anything.

> Mirror is exactly what makes disk rotation dangerous: a mirror aimed at the wrong disk would
> make it identical to the source, deleting whatever it finds there. If you alternate several
> external disks, read *Disk rotation*.
