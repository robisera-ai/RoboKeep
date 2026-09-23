# Integrity verification

A backup can look fine and not be. A file can **corrupt silently** on disk — a flipped bit, a
failing sector — without its date or size noticing. Integrity verification is the mathematical
proof that the copy really is intact.

## What the Verify button does

Click **Verify** and RoboKeep **re-reads every file on both sides** — source and destination —
and computes its **SHA-256** fingerprint, a code that changes at the slightest differing byte. If
the two fingerprints match, the files are identical bit for bit. If they differ, something's
off. It's a check that catches silent corruption, **invisible** to any date or size comparison.

## Smart about false alarms

Re-reading everything could raise pointless alarms. RoboKeep avoids them:

- A file you **edited after** the backup is reported as *"changed after the backup"*, not as
  corruption: it's a legitimate difference, not a fault.
- Verification **respects the job's exclusions**: what you don't copy isn't checked either.
- Jobs with **versions** verify the **latest snapshot** — the most recent, consistent copy.

## When to run it

- **On demand**, when you want reassurance: select the job and click **Verify**.
- **Automatically and periodically**, with the per-job option **"Periodically verify integrity
  after the backup"**: when the backup ends, the integrity check runs and is recorded in the
  history. Next to it you choose **every how many days**: the default is **7**. Re-reading
  everything after every backup needlessly wears mechanical disks; once a week finds the same
  corruption with a seventh of the work. With **0** verification goes back to every backup. A
  manual verification counts too: if you've just run one, the automatic one waits its turn.
- If a disk reports a **hardware error** during verification, RoboKeep **stops at once** instead
  of carrying on reading (see *Troubleshooting*).

## How long it takes

Because it really re-reads **everything**, a verification takes roughly **as long as a first
backup**. It's slow by design: certainty is paid for in reading. So enable automatic verification
**only where it truly counts** — your critical data — and run it on demand elsewhere when you need
it.

> Every verification lands in the *history*, with outcome, duration and its own log: a
> double-click opens it in Notepad.
