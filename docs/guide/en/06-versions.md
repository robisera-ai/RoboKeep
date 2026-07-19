# Versions

Versions are a **time machine** for your files. With versions on, before overwriting anything
RoboKeep sets the previous state aside as a **dated snapshot**. Deleted a paragraph last Tuesday?
Open Tuesday's version and get it back.

## How it works

Versions are turned on **per job**: enable them where you need them, leave them off where you
don't. From then on, each run first saves the previous copy as a dated snapshot, then updates the
backup.

## Why they cost little space

The natural worry is: won't ten versions take ten times the space? No. Files that stayed
**unchanged** from one version to the next aren't duplicated — they're **shared** through
hard-links, meaning several versions point to the very same file on disk. So ten versions don't
cost ten times the space — you only pay for what **actually changed**.

That's exactly why versions need a **local NTFS destination**: hard-links don't exist on exFAT or
on network shares. If the destination isn't eligible, RoboKeep tells you before it starts.

## Retention: how many versions to keep

Snapshots don't pile up forever. In the job you set the **retention**:

- keep the last **N versions**, and/or
- keep versions up to a **maximum age in days**.

The oldest ones are removed automatically, so you never have to think about it.

## Browsing versions

When you need to recover something:

1. Select the job and click **Versions...**.
2. Pick the **date** you want.
3. That version opens in **File Explorer**.
4. Copy back whatever files or folders you need.

No special format, no extraction: they're ordinary files, exactly as they were that day.

> Versions pair nicely with *integrity verification*: jobs with versions verify the latest
> snapshot. See the dedicated chapter.
