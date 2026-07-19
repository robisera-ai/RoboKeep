# Command line

RoboKeep can also start from the **command line**, without opening the window. That's handy if you want to build your own automations with **Windows Task Scheduler** or a script.

## The commands

```
RoboKeep.exe --run-all              run all enabled jobs
RoboKeep.exe --job "Documents"      run a single job
RoboKeep.exe --run-all --dry-run    preview only, nothing changes
RoboKeep.exe --job "Photos" --config "D:\path\config.json"
```

- **`--run-all`** runs every enabled job in sequence.
- **`--job "Name"`** runs a single job, named.
- **`--dry-run`** runs the **preview** only: RoboKeep shows what *would* be copied or deleted, **without touching anything**.
- **`--config`** uses a configuration file other than the default.

## Exit code

When it finishes, RoboKeep returns an **exit code**. A **0** means **all good**: you can use it in your scripts to decide whether to continue.

> Even a **job skipped by disk rotation** counts as a good outcome and returns **0**: it isn't an error, it's just waiting for its disk (see *Disk rotation*).

## Per-job scheduling is simpler

The command line is meant for **custom** automations. But if all you need is to run backups at a set time, don't build anything by hand: **Automatic scheduling** per job, right from the editor, is simpler and creates the Windows scheduled task for you. We cover it in the *Automatic scheduling* chapter.
