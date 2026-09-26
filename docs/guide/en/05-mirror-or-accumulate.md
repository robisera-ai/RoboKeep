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

## The deletion threshold

The most common way to lose data to a mirror isn't a fault: it's a source that **emptied itself
without your knowing** — a folder moved or renamed, a network drive that didn't mount and looks
empty, ransomware that encrypted everything. The mirror does its job and makes the destination
identical to the source: empty.

That's why every mirror job has a threshold in the editor — **"Stop the mirror if it would delete …
% of the files or more"**, **20 %** by default. Before starting, RoboKeep counts, without touching
anything, how many files would disappear from the destination; if that reaches the threshold:

- job started **from the window**: it asks you to confirm, with the numbers in plain sight ("would
  delete 1812 files out of 2014, 90 %"). *Yes* goes ahead this once, *No* stops the job.
- job started from **a scheduled task or the command line**: it stops, because there's nobody to
  ask. The job comes up as failed with "BLOCKED: too many deletions", the log has the numbers, the
  result email announces it and the job's row in the main window flags it.

To unblock it: **start the job from the window and confirm** (that holds for that run only), or
raise the threshold in the job editor — **0** switches the check off entirely. Below **20 files**
it never triggers: a folder with a handful of files changes character in an afternoon. With
*Preview* nothing is blocked, the summary only says you'd be over the threshold. Accumulate isn't
concerned: it never deletes.

Jobs with versions reuse the check the versioning already runs to see whether anything changed, so
they cost nothing extra; for mirrors without versions it's one more read of the destination, which
on huge folders can take tens of seconds (the "previewing the mirror's deletions" line in the log
says so). With versions, though, **no existing file is deleted** — the last version stays untouched
— and the question says it as it is: how many files *fewer* the new version would have than the
last one.
