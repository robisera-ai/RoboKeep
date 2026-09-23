# Automatic scheduling

A backup is only truly useful if it runs on its own, without you having to remember it. With
**scheduling** you give each job its own time and let it work, even with the app closed.

## A time for each job

Open the job with **Edit**: in the scheduling section, choose how often it should run.

- **Daily** — every day at the time you set.
- **Weekly** — on the weekdays you pick, at the time you set.
- **Monthly** — once a month, on the day and at the time you set. Mind days 29, 30 and 31: Windows
  fires them only in months that have them (the 31st runs seven times a year). For "month-end",
  tick **Last day of the month**: it fires on the 28th, 29th, 30th or 31st depending on the month.

Each job keeps its *own* rhythm: documents every evening, photos on Sunday, archives once a month.
Save the job and the schedule is active.

## The Windows scheduled task

When you set a time, RoboKeep creates a Windows **scheduled task** for you. From then on the job
runs at the planned time **even with the app closed**: Windows wakes it up, runs that one job, and
closes it.

The task always stays **in sync** with the job. If you rename it, delete it, or change its time,
the task is updated to match. And if you import a configuration from another PC (see *Job editor*
for exporting), the scheduled tasks are recreated from what you import.

> Tasks are registered through a **language-independent** XML file: day names aren't written out
> in words, so the schedule works the same on an Italian, English, or any other-language Windows.

## Alternative: one task for everything

If you'd rather not give a time to every single job, go to **Settings** and create a single **"run
everything"** task: at a set time, Windows runs all enabled jobs in one go. It's the simplest
choice when your backups can all run together.

## Credentials and scheduled tasks

A scheduled task runs even when you're not signed in, so it must be able to read any network
credentials on its own. That's why the **encryption scope** they're saved with matters. If you
schedule jobs that write to network shares, read *Settings* and *Network backups and credentials*
to set this up correctly.
