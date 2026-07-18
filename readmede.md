# RoboKeep

*Lesen auf: [English](README.md) · [Italiano](readmeita.md) · [Español](readmees.md) · [Français](readmefr.md) · Deutsch*

**Einmal einrichten — jede Datei, jede Version, sicher auf Ihrem eigenen Datenträger.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Sprachen](https://img.shields.io/badge/Sprachen-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Downloads](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep ist eine benutzerfreundliche Windows-App, die `robocopy` — die grundsolide Kopier-Engine,
die bereits in jedem Windows-PC steckt — in ein **echtes Backup-Werkzeug** verwandelt:
Einrichtung per Mausklick, **datierte Versionen** Ihrer Dateien, Kopieren von **Dateien, die Sie
noch verwenden**, jeder Auftrag nach **eigenem Zeitplan**, ein vollständiger **Verlauf** und der
**mathematische Beweis**, dass Ihre Kopien intakt sind. Wechseln Sie zwischen zwei externen
Datenträgern, die Windows beide `E:` nennt? RoboKeep hält jeden Auftrag auf **seinem eigenen
Datenträger** und spiegelt nie auf den falschen. **Keine Cloud, kein Konto, kein Abo** — Ihre
Dateien verlassen nie Ihre Datenträger.

![Hauptfenster von RoboKeep: Aufträge mit Ergebnissen auf einen Blick und Live-Ausführungsprotokoll](docs/images/main-window.png)

## Warum es Ihnen gefallen wird

- 🧙 **Ein paar Fragen beantworten, das richtige Backup erhalten.** Keine robocopy-Kenntnisse
  nötig: Die geführte Einrichtung fragt in klarer Sprache nach Ihren Datenträgern und Daten und
  wählt für Sie die optimalen Einstellungen. Profis können weiterhin alles von Hand anpassen.
- 🛡️ **Wechselnde Backup-Datenträger? Schreibt nie auf den falschen** *(neu in 1.5)*. Wenn Sie
  zwei externe Datenträger abwechseln, vergibt Windows ihnen oft **denselben Buchstaben** (`E:`) —
  und ein Spiegel-Auftrag auf den falschen könnte ihn löschen. RoboKeep erkennt jeden Datenträger
  an seiner echten **Identität**, nicht am Buchstaben, und **überspringt** den Auftrag einfach,
  wenn im Steckplatz nicht der Datenträger sitzt, zu dem er gehört: kein Fehler, nichts gelöscht.
  Schließen Sie den richtigen Datenträger wieder an, und alles läuft weiter, wo es aufgehört hat —
  die Symbole aktualisieren sich in dem Moment, in dem Sie ihn anstecken.
- 🕰️ **Eine Zeitmaschine für Ihre Dateien.** Jeder Lauf kann eine **datierte Version** Ihres
  Backups speichern. Letzten Dienstag einen Absatz gelöscht? Öffnen Sie die Dienstag-Version und
  holen Sie ihn zurück. Der clevere Trick: Unveränderte Dateien werden zwischen den Versionen
  *geteilt*, sodass zehn Versionen nicht das Zehnfache an Platz kosten — nur das, was sich
  wirklich geändert hat.
- 🔓 **Kopiert Dateien auch, während Sie sie nutzen** *(neu in 1.3)*. Outlook-Archive,
  Datenbanken, von anderen Programmen gesperrte Dateien: Mit einem Häkchen fotografiert RoboKeep
  den Datenträger für einen Augenblick (eine „Schattenkopie“ von Windows) und kopiert aus diesem
  eingefrorenen Abbild. Und wenn der Schnappschuss nicht möglich ist, läuft das Backup einfach
  normal weiter — es blockiert nie.
- ✅ **Der mathematische Beweis, dass Ihr Backup intakt ist** *(neu in 1.4)*. Die Schaltfläche
  **Prüfen** liest jede Datei auf beiden Seiten neu ein und vergleicht die digitalen Fingerabdrücke
  (SHA-256): stille Datenträger-Beschädigung — für jede Datums-/Größenprüfung unsichtbar — wird
  aufgedeckt. Und clever: Eine Datei, die Sie *nach* dem Backup bearbeitet haben, wird als solche
  gemeldet, nie als Fehlalarm. Auf Wunsch oder automatisch nach jedem Backup für Ihre kritischen
  Aufträge.
- ⏰ **Jeder Auftrag nach eigenem Zeitplan** *(neu in 1.4)*. Dokumente jeden Abend, Fotos am
  Sonntag, Archive einmal im Monat: Jeder Auftrag hat seine eigene geplante Windows-Aufgabe und
  läuft auch bei geschlossener App.
- 📜 **Das Gedächtnis jedes Laufs** *(neu in 1.4)*. Das Fenster **Verlauf** listet jedes Backup
  und jede Prüfung mit Ergebnis, Zählwerten und Dauer auf — und ein Doppelklick öffnet das
  vollständige Protokoll direkt in der App, ohne in Zip-Dateien zu wühlen.
- 🚨 **Es warnt Sie, wenn etwas nicht stimmt — und nur dann.** Ein Backup, das stillschweigend
  fehlschlägt, ist schlimmer als gar keins. RoboKeep markiert jeden problematischen Auftrag mit
  einem farbigen Symbol — rot für fehlgeschlagen, gelb für „zu lange nicht ausgeführt“, orange für
  „wurde unterbrochen“, grau für „wartet auf seinen Datenträger“ — mit einer klaren Erklärung beim
  Überfahren mit der Maus. Und es nervt Sie nicht wegen eines Auftrags, dessen Datenträger Sie
  einfach abgezogen haben: Das ist eine Sanduhr, kein Alarm.
- 🔍 **Nichts Verstecktes.** Der Editor zeigt stets den **exakten Befehl**, der ausgeführt wird.
  Sie können jedes Backup in der Vorschau (einem „Trockenlauf“) ansehen, um zu sehen, was kopiert
  oder gelöscht würde, bevor Sie irgendetwas anfassen.
- 🏠 **Wirklich Ihres.** Kostenlos und quelloffen (MIT), vollständig lokal, keine Telemetrie.
  Spricht Italienisch, Englisch, Spanisch, Französisch und Deutsch. Portabel, wenn Sie möchten.

## Sehen Sie es in Aktion

| | |
|---|---|
| ![Geführte Einrichtung](docs/images/wizard.png) | **Geführte Einrichtung.** Ein paar Fragen in klarer Sprache — auch, ob Dateien beim Arbeiten geöffnet bleiben — und der Assistent richtet den Auftrag für Sie ein. |
| ![Auftrags-Editor](docs/images/editor.png) | **Alles im Griff.** Spiegeln oder Ansammeln, Kopieren offener Dateien, Optionen für große Dateien: jede Wahl in einer Zeile erklärt, mit einem Hinweis, wo es zählt. |
| ![Befehlsvorschau und Versionen](docs/images/editor-preview.png) | **Volle Transparenz.** Datierte Versionen mit automatischer Bereinigung, Ausschlüsse pro Auftrag und der exakte robocopy-Befehl stets im Blick. |
| ![Ausführungsverlauf](docs/images/history.png) | **Jeder Lauf festgehalten.** Backups und Prüfungen nebeneinander, pro Auftrag filterbar; Doppelklick auf einen Eintrag, um sein vollständiges Protokoll zu lesen, ohne ein Zip anzufassen. |
| ![Zeitplanung pro Auftrag](docs/images/editor-schedule.png) | **Einstellen und vergessen.** Jeder Auftrag kann seinen eigenen Zeitplan haben — täglich, wöchentlich oder monatlich — plus automatische Integritätsprüfung nach jedem Lauf. |
| ![Versionen durchsuchen](docs/images/versions.png) | **Reisen Sie zurück in der Zeit.** Ein Datum wählen, ein Klick, und das Backup jenes Tages öffnet sich im Datei-Explorer. Holen Sie zurück, was Sie brauchen. |

## In zwei Minuten loslegen

1. Holen Sie sich die neueste Version von der **[Releases](../../releases)**-Seite:
   - **`…-selfcontained.zip`** — entpacken und starten, **nichts zu installieren** (enthält .NET, größerer Download);
   - **`…-framework-dependent.zip`** — viel kleiner, benötigt die kostenlose
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0), einmalig installiert.
2. Entpacken Sie das Zip in einen **beschreibbaren Ordner** (z. B. `D:\Programme\RoboKeep` —
   meiden Sie `C:\Program Files`).
3. Starten Sie **`RoboKeep.exe`**, klicken Sie auf **Neu**, beantworten Sie die Fragen des
   Assistenten und dann **Alle ausführen**. Fertig.

Soll es von selbst laufen? Geben Sie jedem Auftrag direkt im Editor seinen eigenen Zeitplan
(täglich, wöchentlich oder monatlich) oder nutzen Sie **Einstellungen → Zeitplanung** für eine
einzige „Alles ausführen“-Aufgabe. So oder so läuft es auch bei geschlossener App.

## Wie sich ein Backup verhält

Für jeden Auftrag (ein Paar Quellordner → Zielordner, Unterordner inbegriffen) macht RoboKeep
Folgendes:

- **überspringt** unveränderte Dateien (deshalb dauert der zweite Lauf nur Sekunden);
- **aktualisiert** in der Quelle neuere Dateien;
- im **Spiegel**-Modus (Standard) **entfernt** es außerdem aus dem Ziel, was Sie in der Quelle
  gelöscht haben — das Ziel bleibt eine exakte Kopie;
- bei ausgeschaltetem Spiegel wird nur hinzugefügt und aktualisiert, **nie gelöscht**.

Und wenn Sie Versionen aktiviert haben, speichert jeder Lauf zuerst den vorherigen Stand als
datierten Schnappschuss.

## Alle Funktionen

**Sichern**
Spiegel- oder Ansammel-Modus · **Schutz bei Datenträger-Rotation: ein Auftrag läuft nur auf
seinem Datenträger, per Volume erkannt — nie ein Spiegel auf den falschen oder fehlenden** ·
datierte Versionen mit Hardlinks und konfigurierbarer Aufbewahrung · Kopieren offener/gesperrter
Dateien via VSS · **Integritätsprüfung (SHA-256), auf Abruf oder nach jedem Lauf** ·
Multithread-Kopieren · Datei- und Ordnerausschlüsse pro Auftrag · „Kopie erzwingen“ für Dateien,
deren Datum/Größe sich nie ändern (verschlüsselte Container, manche Datenbanken), mit optionalem
Inhalts-Vergleichsmodus · fortsetzbarer Modus für riesige Dateien · Vorschau/Trockenlauf

**Es hält Sie auf dem Laufenden**
**Ausführungsverlauf mit integriertem Protokoll-Viewer** · Zustandssymbole pro Auftrag mit
Erklärungen in klarer Sprache, samt einem neutralen Zustand „wartet auf seinen Datenträger“ ·
**Symbole, die sich in dem Moment aktualisieren, in dem Sie einen Datenträger an- oder abstecken**
· Vorabprüfungen (Ziel erreichbar, Speicherplatz, VSS- und Versions-Eignung) · Echtzeit-Protokoll
· gezippte Protokollarchive pro Auftrag mit automatischer Bereinigung · Toast-Benachrichtigungen
und Infobereich · E-Mail-Berichte (SMTP), auf Wunsch nur bei Fehlern

**Es passt sich Ihnen an**
geführter Assistent oder vollständiger manueller Editor · exakte Befehlsvorschau ·
**Zeitplanung pro Auftrag (täglich / wöchentlich / monatlich)** · Netzwerkfreigaben mit
verschlüsselten Anmeldedaten (Windows-DPAPI) · **Kopier-Drosselung für Netzwerk-Backups** ·
**Konfiguration exportieren/importieren** · Befehlszeile zur Automatisierung · Aufträge per
Ziehen und Ablegen ordnen · 5 Sprachen · Portabler Modus

## Gut zu wissen

- **Nur Windows 10/11** — RoboKeep baut auf robocopy und andere Windows-eigene Funktionen.
- **Geänderte Dateien werden vollständig neu kopiert** (keine Block-/Delta-Kopie): bestens für
  Dokumente und Fotos, teuer für einzelne riesige Dateien, die sich täglich ändern.
- **Rotierende Datenträger werden per Identität erkannt, nicht per Buchstaben.** Binden Sie einen
  Auftrag im Editor an seinen Datenträger (**Mit diesem Datenträger schützen**); von da an wird
  der Auftrag übersprungen, sobald im Steckplatz ein anderer — oder gar kein — Datenträger sitzt,
  damit er nie auf den falschen spiegelt. Aufträge auf internen oder Netzwerk-Zielen brauchen das
  nicht und sind nicht betroffen.
- **Versionen brauchen ein lokales NTFS-Ziel** (Hardlinks gibt es nicht auf exFAT oder
  Netzwerkfreigaben).
- **Das Kopieren offener Dateien braucht eine lokale NTFS-Quelle** und verlangt eine
  Administrator-Bestätigung (UAC) pro Lauf.
- **Integritätsprüfungen lesen jede Datei auf beiden Seiten neu ein**: gründlich per Design,
  rechnen Sie also damit, dass eine Prüfung ungefähr so lange dauert wie ein erstes Backup.
  Aktivieren Sie „nach jedem Backup prüfen“ nur dort, wo es zählt.
- Ihre Einstellungen, Ergebnisse und Protokolle liegen in `%APPDATA%\RoboKeep` und überstehen so
  App-Aktualisierungen. Passwörter werden mit der Windows-DPAPI verschlüsselt, nie im Klartext
  gespeichert. Hinweis: Der Standard-Verschlüsselungsbereich ist **maschinenweit** (damit auch
  geplante Aufgaben sie entschlüsseln können) — auf einem gemeinsam genutzten PC stellen Sie die
  Einstellung auf den benutzerbezogenen Bereich um, wenn andere Konten sie nicht lesen können
  sollen.

## Datenschutz

RoboKeep sammelt **nichts**. Keine Telemetrie, keine Analyse, keine Update-Prüfungen, kein Konto,
überhaupt kein Netzwerkverkehr, es sei denn, *Sie* richten E-Mail-Berichte ein (SMTP-Server Ihrer
Wahl — TLS standardmäßig an). Alles, was die App weiß — Auftragseinstellungen, Ergebnisse,
Verlauf, Protokolle — liegt in lokalen Dateien auf Ihrem PC, lesbares JSON, das Sie jederzeit
einsehen können. Protokolle enthalten die Pfade der kopierten Dateien und werden nach 30 Tagen
automatisch bereinigt (konfigurierbar).

## Für Fortgeschrittene

```text
RoboKeep.exe --run-all              führt alle aktivierten Aufträge aus (Exit-Code 0 = alles gut)
RoboKeep.exe --job "Dokumente"      führt einen einzelnen Auftrag aus
RoboKeep.exe --run-all --dry-run    nur Vorschau, keine Änderungen
RoboKeep.exe --job "Fotos" --config "D:\Pfad\config.json"
```

Aus dem Quellcode bauen: `dotnet build src/RoboKeep.sln -c Release` (erfordert das .NET-10-SDK) —
die Testsuite läuft mit `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`.
Portabler Modus: Legen Sie eine leere Datei `portable.flag` neben die exe, und alles
(Konfiguration, Protokolle, Ergebnisse) bleibt im App-Ordner. Beispielkonfiguration:
[config/config.example.json](config/config.example.json). Projekthintergrund und technische
Entscheidungen: [ANALISI.md](ANALISI.md) *(auf Italienisch)*.

## Lizenz

Vertrieben unter der **MIT-Lizenz** — siehe [LICENSE](LICENSE). Komponenten von Drittanbietern
sind in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) aufgeführt. © 2026 Roberto Serafini.
