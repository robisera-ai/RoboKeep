# Privacy and security

RoboKeep is built to stay **in your home**: your files stay on your disks, and the app has nothing to gain from your data.

## It collects nothing

RoboKeep **collects nothing**: zero telemetry, zero analytics, no account and no network traffic at all, with **two exceptions, both your choice**.

The **update check**: on first start RoboKeep asks whether you want it to check for a new version; if you say yes, at startup (at most once a day) it makes one HTTPS request to `api.github.com` to read the latest published version number. The request carries your IP address (as any network request does) and a User-Agent `RoboKeep/<version>`; nothing else — no data about your jobs, disks or files. Downloading happens only when you ask. You can switch it off in Settings.

**Email reports**: if you set them up, the app contacts the **SMTP** server you chose (with TLS on by default) to send the summary. That's it. Nothing else leaves your PC.

Reading **disk health** (SMART) is entirely local too: the app talks directly to the disks connected to your PC, nothing goes out.

## Everything in local files

Everything the app knows — settings, outcomes, history, logs — lives in **local files** on your PC. It's **readable JSON**: you can open and inspect it whenever you like, no special tools needed. No opaque database, no secret format.

## Passwords encrypted

Passwords (network shares, email sender) are encrypted with **Windows DPAPI**, the operating system's own encryption mechanism. They are **never stored in plain text**. You set the encryption scope in *Settings*.

## The configuration copy on the backup disk

After every successful backup RoboKeep writes a **`RoboKeep-config`** folder, holding your configuration, in the root of the destination disk (see *Settings*). Knowing what is in it is only fair, because that disk often travels:

- the **paths** of sources and destinations, the job names and the exclusions;
- the **hosts of the network shares** and the **user names** of the saved credentials: the name of the NAS or server RoboKeep connects to, and the user it connects as;
- the **email addresses** of the sender and the recipient of the reports, with the user name and the SMTP server;
- the **passwords** (network shares, email) encrypted with **Windows DPAPI**: they can only be decrypted on that PC — and only by your Windows user, if you chose "encrypt passwords only for my Windows user" in *Settings*. Whoever takes the file to another PC cannot read them; that is why you have to type them in again when you restore on a new PC.

It contains **none of your files**, no logs and no history: the configuration only.

If you do not want it — because the disk is shared, say, or because you take it out of the house — turn off **"Save a copy of the configuration on the backup disks"** in *Settings*. You can delete the `RoboKeep-config` folders already written by hand: RoboKeep does not put them back while the option is off.

## Logs and their cleanup

Logs contain the **paths** of the files that were copied — useful for understanding what happened in a run. To keep them from piling up, they're **cleaned up automatically after 30 days**, a value you can change in *Settings*.

## Open source

RoboKeep is **open source**, under the **MIT** license. The code is public: anyone can read it and see for themselves that it does exactly what it says — nothing hidden.
