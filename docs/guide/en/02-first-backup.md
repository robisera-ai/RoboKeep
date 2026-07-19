# Your first backup

Creating a backup with RoboKeep takes a few minutes. All you need to know is **what** you want to
save (the source folder) and **where** (the destination folder, usually on an external disk).

## Step 1 — Start the guided setup

In the top toolbar, click **New**. The wizard opens and asks a few questions in plain language:
which folder to copy, to which destination, and whether files stay open while you work. Based on
your answers, it picks the best settings for you.

If you'd rather do everything by hand, you can skip the wizard and fill in the editor directly.

## Step 2 — Check the settings

Before saving, the editor shows the main choices:

- **Mirror**: the destination becomes an exact copy of the source. Careful: in mirror mode, what
  you delete from the source is also removed from the destination. If you only want to add and
  update without ever deleting, clear the checkbox.
- **Destination**: the folder your files will end up in. Below it appears the *Disk* row, useful
  if you rotate disks (see the dedicated chapter).

At the bottom you always see a **preview of the exact command** that will run: no hidden magic.

## Step 3 — Preview and run

A good habit the first time: click **Preview**. RoboKeep lists what *would* be copied or deleted,
**without touching anything**. That way you avoid surprises.

When you're confident, click **Run selected** (or **Run all** to run every enabled job). You'll
watch the log scroll in real time and, at the end, a summary of how many files were copied.

> The second run will be nearly instant: RoboKeep skips files that haven't changed and recopies
> only what's needed.

Done — you have your first backup. From here you can add dated versions, automatic scheduling, or
integrity verification — a chapter for each.
