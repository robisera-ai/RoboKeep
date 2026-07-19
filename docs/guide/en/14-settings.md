# Settings

**Settings** gather the choices that apply to the whole app, not to a single job. You open them from the top toolbar. Here's what's inside.

## Language

RoboKeep speaks **five languages**: Italian, English, Spanish, French, German. Pick yours and the interface changes right away.

## Logs and folders

- **Log folder**: where RoboKeep archives the record of every run.
- **Retention days**: how many days to keep logs before automatic cleanup.
- **Temporary folder**: the workspace the app uses during operations.

## Credential encryption (DPAPI)

Passwords (network shares, email sender) are encrypted with **Windows DPAPI**, never in plain text. Here you choose the encryption **scope**:

- **Machine-wide** (default): any account on the PC can decrypt them. It has to be this way so that **scheduled tasks** can read the credentials and run with the app closed.
- **Per-user**: only your account can decrypt them. Recommended on a **shared PC**, where other users shouldn't be able to read them.

> If you change this option, passwords already saved are **re-encrypted automatically** with the new scope. You don't have to re-enter them.

## Notifications and system tray

From here you turn on **toast notifications** and the **tray** behaviors (minimize to tray, start minimized, run in background). The details are in the *Notifications and email* chapter.

## Stale-backup warning

The **"warn if a backup hasn't run in N days"** threshold raises an alert icon on jobs that haven't run in too long. Set it to **0** to never get this warning.

## Export and import configuration

You can save your whole configuration to a file and load it back elsewhere.

- **Export**: writes settings and jobs to a file.
- **Import**: first **validates** the file; then saves a **backup** of your current configuration; finally **re-syncs the scheduled tasks** with the new jobs.

## Where settings live

Everything lives in **`%APPDATA%\RoboKeep`**, so it survives app updates. In **portable mode**, it lives in the app folder instead.
