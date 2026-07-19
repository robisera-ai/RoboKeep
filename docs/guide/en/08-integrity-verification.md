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
- **Automatically after every backup**, with the per-job option **"Verify after each backup"**:
  every run ends with its own integrity check, recorded in the history.

## How long it takes

Because it really re-reads **everything**, a verification takes roughly **as long as a first
backup**. It's slow by design: certainty is paid for in reading. So enable automatic verification
**only where it truly counts** — your critical data — and run it on demand elsewhere when you need
it.

> Every verification lands in the *history*, with outcome and duration; a double-click opens the
> full log inside the app.
