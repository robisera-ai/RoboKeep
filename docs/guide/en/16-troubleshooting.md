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

### A job fails with "data error (cyclic redundancy check)" (CRC)

This message comes from Windows, not from RoboKeep, and means the **destination disk can't read a sector** — almost always a **bad sector**. If it happens on a versioned job, RoboKeep now **skips the unreadable file and carries on** (it says so in the log), but the message is still an important warning.

Check the **destination disk's health** with a tool like **CrystalDiskInfo** (free): look at *Current Pending Sectors* and *Reported Uncorrectable Errors*. If they're above zero or the status is "Caution", the disk is failing: **stop using it for backups** and switch to a healthy one. Your original data is safe in the source — the copies can be rebuilt on a new disk.
