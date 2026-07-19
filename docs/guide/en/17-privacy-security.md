# Privacy and security

RoboKeep is built to stay **in your home**: your files stay on your disks, and the app has nothing to gain from your data.

## It collects nothing

RoboKeep **collects nothing**: zero telemetry, zero analytics, no update checks, no account, no network traffic at all. The one exception is up to **you**: if you set up **email reports**, the app contacts the **SMTP** server you chose (with TLS on by default) to send the summary. That's it. Nothing else leaves your PC.

## Everything in local files

Everything the app knows — settings, outcomes, history, logs — lives in **local files** on your PC. It's **readable JSON**: you can open and inspect it whenever you like, no special tools needed. No opaque database, no secret format.

## Passwords encrypted

Passwords (network shares, email sender) are encrypted with **Windows DPAPI**, the operating system's own encryption mechanism. They are **never stored in plain text**. You set the encryption scope in *Settings*.

## Logs and their cleanup

Logs contain the **paths** of the files that were copied — useful for understanding what happened in a run. To keep them from piling up, they're **cleaned up automatically after 30 days**, a value you can change in *Settings*.

## Open source

RoboKeep is **open source**, under the **MIT** license. The code is public: anyone can read it and see for themselves that it does exactly what it says — nothing hidden.
