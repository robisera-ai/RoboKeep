# Notifications and email

RoboKeep can tell you how a backup went without you watching the window. You choose how much to be told: a quick pop-up, an app that runs out of the way, or a report that lands in your inbox.

## Toast notifications

At the end of each job, RoboKeep can show a Windows **toast notification** — the one that appears at the bottom right — with the outcome. One glance and you know it all went fine, without opening the app.

You turn notifications on and off in **Settings**.

## System tray

If you want RoboKeep within reach but out of the way, use the **notification area** (the tray, near the clock):

- **Minimize to tray**: closing the window doesn't quit the app — it tucks it away among the system icons.
- **Start minimized**: at startup RoboKeep launches straight into the tray.
- **Run in background**: it stays active to show you notifications and to react when you connect or disconnect a disk.

A click on the tray icon brings the window back up.

## Email reports (SMTP)

If you want a summary even when you're away from the PC, set up **email reports**. RoboKeep sends them through an **SMTP** server of your choice.

You need just a few details:

- **Server and port**: the address of your SMTP server. **TLS is on by default**, on **port 587** — the recommended setup for encrypted mail.
- **Sender credentials**: the username and password of the account the email is sent from. The password is encrypted like every other one.
- **Recipient**: the address the report is delivered to.

There's also an **only on errors** option: turn it on and you get an email only when something went wrong, and none when everything runs smoothly.

> Reports go out **after every real run** of a job. A preview generates no email: it's only showing what would happen, it touches nothing.

Email settings are part of your configuration — you'll find them all gathered in the *Settings* chapter.
