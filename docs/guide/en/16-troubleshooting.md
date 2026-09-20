# Troubleshooting and FAQ

The most common situations, with the short answer. If yours isn't here, the chapter on that topic almost always has the detail.

### A job comes up as "Skipped"

That's **normal** with disk rotation, not an error. The job writes to a specific disk, and right now another disk — or none — is in the slot. Connect the right disk and it picks up on its own. See *Disk rotation*.

### A UAC prompt appears

That's **VSS**: copying open files photographs the disk for an instant and asks for **one administrator confirmation** per run. Confirm and the backup continues. See *Copying open files (VSS)*.

### Versions aren't working

Dated versions use hard-links, which exist only on **local NTFS**. You need an **NTFS** destination on a **local disk**: on exFAT or a network share they aren't possible. See *Versions*.

### SmartScreen says "unknown publisher"

The app **isn't digitally signed yet**, so Windows SmartScreen warns you. It's **safe**: click **"More info"** and then **"Run anyway"**.

### A scheduled task doesn't start

Check that the credentials' **DPAPI encryption scope** is **machine-wide**: only then can the scheduled task decrypt the credentials and run with the app closed. With per-user scope, the task can't read them. See *Settings*.

### "Destination unreachable"

The destination disk or share **isn't connected**. Plug in the external disk or check the network connection, then try again.

### Verification says "modified after the backup"

That's not **corruption**. It means you **edited the files after copying them**: they're newer in the source than in the copy. RoboKeep reports it this way on purpose, so it doesn't scare you with a false alarm. The next backup brings them back in sync.

### A job stops with "HARDWARE ERROR" (CRC, cyclic redundancy check)

The error comes from Windows, not from RoboKeep, and means **a disk — or its connection — reported a physical problem**: data error (CRC), sector not found, or I/O device error. There are two possible causes, and they need telling apart: a **failing disk** (bad sectors), or a faulty **cable, USB enclosure or power supply**, which on an external disk produce exactly the same messages.

When this happens RoboKeep **stops immediately**, at the first error, and marks the job as failed: carrying on reading and writing to media that reports physical errors makes the damage worse. For the same reason, the other jobs of the same session that use that disk **are not started**. Don't keep re-running the backup: check first.

1. **Cable and power** — try another cable and another USB port (preferably a rear port, no hub). If the disk has its own power supply, check that too.
2. **Disk health** — with a tool like **CrystalDiskInfo** (free), look at *05 Reallocated Sectors*, *C5 Current Pending Sectors* and *C6 Uncorrectable Sectors*: if they're above zero, or the status is "Caution", **the disk is failing**. A rising *C7 UltraDMA CRC Error Count* with the others at zero points to the **cable** instead.

If the failing disk is the backup disk, **stop using it** and switch to a healthy one: your original data is safe in the source, and the copies can be rebuilt. If it's the **source** disk instead, rescue your data before anything else.
