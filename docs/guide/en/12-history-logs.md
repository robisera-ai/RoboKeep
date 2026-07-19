# History and logs

A backup that fails silently is worse than no backup. That's why RoboKeep keeps a record of every
run and lets you read it all back, whenever you want.

## The History window

Open **History** to see the list of every backup and every integrity verification. For each entry
you'll find:

- the **date** and the **job** it belongs to;
- the **outcome** (succeeded, skipped, or failed);
- the **counts** of the files involved;
- the **duration**.

You can **filter by job** to focus on a single backup and follow how it's done over time.

## Reading the full log

**Double-click** an entry in the history: RoboKeep opens that run's **full log** and shows it right
inside the app. The log is read straight from the zip it's archived in, so you never have to hunt
for files or extract anything by hand.

## The real-time log

While a job is running, the **log scrolls in real time** at the bottom of the main window: you see,
line by line, what's being copied, updated, or removed — and the summary at the end.

## Archive and automatic cleanup

Each run's log is **archived and compressed into a zip**, kept separate **per job**. That keeps the
history tidy and gives every backup its own trail.

To stop the archive from growing forever, RoboKeep applies **automatic cleanup**: logs older than
the retention period are removed on their own. The period is **configurable in Settings** (30 days
by default), so you decide how long to keep the history of your backups.

> Logs contain the paths of the files that were copied. They stay only on your PC and are deleted
> when the retention period you set expires: no data leaves your disks.
