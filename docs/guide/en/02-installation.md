# Installation and first launch

RoboKeep has no real installer: you download an archive, extract it, and run the app. You're
ready in a few minutes.

## Requirements

You need **Windows 10 or 11**. RoboKeep builds on robocopy and other Windows-native features, so
it doesn't run on other systems.

## Download the right package

On the **Releases** page you'll find two zips. Pick one:

- **Self-contained** — extract and run, **nothing to install**: .NET is already bundled. The
  download is larger.
- **Framework-dependent** — much smaller, but it needs the free **.NET 10 Desktop Runtime**
  installed once on the PC.

If you're not sure which to grab, take the self-contained one: it just works.

## Extract and run

Extract the zip into a **writable folder** — for example `D:\Programs\RoboKeep`. Avoid
`C:\Program Files`: Windows restricts writing there, and RoboKeep keeps a few working files next
to itself.

Then open the folder and run **`RoboKeep.exe`**. The main window opens.

## A tour of the main window

- **The jobs list** in the middle: one row per backup, with the columns *Enabled*, *Name*,
  *Mode*, *Source*, *Destination*, and *Last result*. A **health icon** tells you at a glance if
  something's wrong (or if a job is simply waiting for its disk).
- **The top toolbar**, with the commands: *New*, *Edit*, *Delete*, *Versions*, *Preview*, *Run
  selected*, *Run all*, *Verify*, *History*, *Help*, *Settings*.
- **The execution log** at the bottom: it scrolls in real time while a backup runs.

## Where your data lives

Settings, results, and logs live in `%APPDATA%\RoboKeep`, so they **survive app updates**.

Prefer to keep everything alongside the executable — on a USB stick, say? Turn on **portable
mode**: create an empty file named `portable.flag` next to `RoboKeep.exe`, and from then on the
configuration, logs, and results stay in the app folder.

> Ready? In *Your first backup* you'll create your first job in a few clicks.
