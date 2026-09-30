# Restoring

A backup earns its keep on the day something goes wrong. **Restore...** is the window that puts that
day back: you pick **how the backup looked on a given date**, tick **what** to recover and say
**where** to put it. It works with both version models, and it never touches your originals.

There are two ways in:

- from the main toolbar, with a job selected: **Restore...** (enabled for jobs that keep versions);
- from **Versions...**, with the **Restore...** button: the date you picked there arrives already
  selected.

## The one rule that matters: "as it was on that date" ≠ "what changed that day"

They are two different things, and mixing them up is the easiest way to believe you have lost files.

- **Versions...** opens a dated folder in File Explorer. With the **differential** model that folder
  holds **only the files that backup replaced or deleted**. Finding three files in there does not
  mean the backup is incomplete: nothing else had changed, and the rest is in `current`.
- **Restore...** instead rebuilds the **whole tree**: changed files come from the version folders,
  everything else from `current`, and files that **did not exist yet** back then are left out. It
  is almost always many more files than the dated folder shows — and it is exactly what you need.

With the **hard-link** model the distinction does not arise: every dated folder is already the whole
tree of that day, and restoring simply saves you from copying it back by hand.

### What a date means: it depends on the model, and the list says so

The two models keep different things, so a date does not mean the same in both. The list spells it
out, entry by entry:

- **differential → "Before the backup of …"**. A version folder holds copies of the files that
  backup **replaced or deleted**: the state from *before* that backup. Picking that point gives you
  exactly that. A file that vanished with the backup of the 28th is in **"Before the backup of the
  28th"**.
- **hard-link → "After the backup of …"**. The dated folder is the tree **as that backup left it**:
  picking that point gives you exactly that.

In the differential model **every** point in time is listed, including backups that only added files
(no folder, and no count of changed files): each one is a different state.

Why not "after" for differential too? Because the copies in the **oldest** version folder would
become unreachable: you would need the point before it, which is often an additions-only backup whose
record retention has already removed. A file deleted from the source, whose only copy lives right
there, could no longer be recovered from the window. With "before the backup" every version folder is
reachable by picking its own point.

### How RoboKeep walks back (differential model)

It starts from `current`, which is **now**, and walks back one version at a time, from the oldest to
the most recent among **the backup you picked** and the **later** ones:

- a file that version had **replaced** or **deleted** comes back from its folder — that is where the
  *previous* copy lives, which is precisely the one that existed back then;
- a file that version had **added** drops out of the list: it did not exist yet;
- a file no version ever touched stays the one in `current`: it never changed, so today's copy **is**
  the one from back then.

The version **closest** to the chosen point always wins. If a file was rewritten on the 27th and
again on the 28th and you pick **"Before the backup of the 27th"**, you get the copy set aside by the
backup of the **27th** — not the one from the 28th, which is the state a day later.

How far back can you go? To **before the oldest version you kept**. Retention ("keep the last N
versions") is also the reach of your time machine: keep 10 versions with a daily backup and you go
back ten days, no further.

## Case 1: getting one file or folder back

The everyday case: you overwrote a document, or emptied a folder by mistake.

1. Select the job and press **Restore...**.
2. Under **As it was on:** pick the moment. The first entry, **Now (current state)**, is the
   up-to-date backup; below it, most recent first, one entry per backup, each with how many files
   it changed — often that is how you spot "the day it went wrong".
   - Differential: pick that very entry, **"Before the backup of …"**: it gives the files back as
     they were before that backup replaced or deleted them.
   - Hard links: entries read **"After the backup of …"**: pick the one **below** the suspicious
     backup, i.e. the last backup before things went wrong.
3. Open the folders in the tree and **tick** what you need. Ticking a folder takes everything
   beneath it, including branches you never opened; untick a file inside and the folder becomes
   partly ticked, with the exception honoured.
   The search box at the top filters by name when you already know what you are after.
4. Under **Restore into:** choose where the files should land. See below: it cannot be the job's
   source.
5. **Restore**. A confirmation shows how many files, how much space and into which folder.

When it is done the window says how many files arrived, and **Open folder** takes you there. The
files keep the **last-modified date** they had: that is what makes a March letter recognisable.

## Case 2: putting the whole job back

New disk, new PC, a folder deleted outright: here you tick nothing.

1. **Restore...**, pick the moment: usually **Now**; if the source was emptied or damaged and a
   backup has already recorded the damage, **"Before the backup of …"** of that backup
   (differential) or **"After the backup of …"** of the one before it (hard links).
2. Choose an empty destination folder.
3. Tick the box at the top of the list, next to "Files and folders": it selects everything. Then **Restore**.

RoboKeep rebuilds the complete tree, including the **folders that were empty** back then — a
recovery that dropped them would hand you a different tree from the one you had.

Then, once you have checked that everything is there, you move the files into place with File
Explorer. That extra step is deliberate: see below.

## Why you cannot restore straight over the source

The destination folder **cannot be the job's source, nor a folder inside it**. The button stays off
and the window says why.

It is not a technical limit: restoring over the source would overwrite **today's** files with those
of the chosen date, in one click and with no way back. Restoring into a separate folder lets you look
at what arrived before you decide what to replace — and if you picked the wrong date, you have lost
nothing.

For the same reason a restore **never overwrites** a file that already exists in the destination
folder. When it finds one it leaves it alone and lists it at the end among the **not restored**
files: that file might be the very one you were trying to save. If the folder you chose is not
empty, the window tells you before it starts.

## While it copies, and when something fails

The copy shows a progress bar and can be **cancelled**: files already copied stay where they are,
nothing is taken back.

A file that cannot be copied — open in another program, permissions too tight, a disk problem —
**does not stop the others**: it ends up in the not-restored list with the reason beside it, and
everything else still arrives. Close whatever was holding it open and restore that one file again.

If a version has been removed by retention, or a file could not be set aside because it was in use
when the backup ran, RoboKeep uses the **closest** copy it has. That is the best that exists on the
disk: there is nothing hidden elsewhere to dig out.

> Restored files are **ordinary files**: no format to extract, no program to install. Even if
> RoboKeep vanished tomorrow, your destination is still a folder you can open in File Explorer —
> this window saves you the work of rebuilding by hand, it is not the key to a safe.
