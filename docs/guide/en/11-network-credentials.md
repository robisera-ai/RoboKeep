# Network backups and credentials

RoboKeep doesn't only work on local disks: a source or destination can live on a **network
share**, given as a UNC path in the form `\\server\share`.

## Using a network path

In the job editor, type the UNC path as the source or destination, exactly as you would a local
folder. If the share is open to everyone, that's all you need. If it requires a sign-in, add a
credential.

## Adding a credential

A credential is the **host, user, and password** trio RoboKeep uses to authenticate to the server.

The shortest route is the **wizard**: if the source or destination is a network share, its last
step asks for user and password and creates the credential for you (if one already exists for
that server, it is used without asking). Otherwise, by hand:

1. Open **Settings** and go to network credential management.
2. Add a credential with the host (the server name), the user, and the password.
3. Save.

The password is kept **encrypted with Windows DPAPI**, never in plain text: nothing readable is
left on disk.

## Linking a credential to a job

In the job editor, choose which credential to use for that connection. Before running the job,
RoboKeep connects to the share with those credentials; then it runs the backup normally.

> Tip: if you copy to a share where permissions matter, turn on **"Copy ACLs/owner too"** so the
> destination also carries each file's permissions and owner, not just its contents.

## Encryption scope and scheduled tasks

DPAPI can encrypt credentials with **user** scope or **machine** scope. The difference matters for
scheduled tasks: a scheduled backup can run when you aren't signed in, and with user-only scope it
couldn't decrypt the password. So if you schedule network jobs, use **machine** scope, so the task
can read the credentials on its own. You'll find the setting in *Settings* — on a shared PC, weigh
the trade-off described there.

## Network and disk rotation

A network destination **has no removable volume**, so it's **exempt** from disk-rotation
protection: a network job is never "skipped" while waiting for the right disk. That protection only
applies to rotating external disks (see *Disk rotation*).
