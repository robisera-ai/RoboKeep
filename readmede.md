# RoboKeep

*Lesen auf: [English](README.md) · [Italiano](readmeita.md) · [Español](readmees.md) · [Français](readmefr.md) · Deutsch*

**Einmal einrichten. Jede Datei, jede Version, sicher auf Ihrem eigenen Datenträger.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Sprachen](https://img.shields.io/badge/Sprachen-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Downloads](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep ist eine Windows-Backup-App, die auf `robocopy` aufbaut, der Kopier-Engine, die bereits in
jedem PC steckt. Beantworten Sie ein paar Fragen, und RoboKeep hält Ihre Ordner gespiegelt auf
einem externen Datenträger, mit datierten Versionen, zu denen Sie zurückkehren können. **Keine
Cloud, kein Konto, kein Abo.**

![Hauptfenster von RoboKeep: Aufträge mit Ergebnissen auf einen Blick und Live-Ausführungsprotokoll](docs/images/main-window.png)

## Warum RoboKeep

| | |
|---|---|
| 🧙 **Einfache Einrichtung** | Der Assistent fragt in klarer Sprache nach Ihren Daten und wählt die passenden Einstellungen. Wer möchte, kann alles von Hand anpassen. |
| 🕰️ **Zurück in der Zeit** | Jeder Lauf kann eine datierte Version speichern. Unveränderte Dateien werden zwischen den Versionen geteilt, sodass zehn Versionen nicht das Zehnfache an Platz kosten. |
| 💿 **Schont Ihre Datenträger** | Stoppt beim ersten Hardwarefehler, statt stundenlang weiterzukopieren, hält den PC während des Backups wach und geht behutsam mit mechanischen Festplatten um. |
| 🩺 **Datenträgerzustand auf einen Blick** | Ein Klick liest den SMART-Zustand jedes Datenträgers aus und liefert ein klares Urteil: Gut, Achtung, Gefahr — mit jedem Wert erklärt. *Neu in 1.8.* |
| 🛡️ **Nie der falsche Datenträger** | Wechseln Sie zwischen zwei externen Datenträgern, die Windows beide `E:` nennt? Jeder Auftrag erkennt seinen eigenen Datenträger an der Identität und wartet einfach auf ihn. |
| ✅ **Der Beweis, dass es geklappt hat** | Prüfen liest beide Seiten neu ein und vergleicht die SHA-256-Fingerabdrücke. Stille Beschädigung wird aufgedeckt. |
| 🔓 **Auch offene Dateien** | Outlook-Archive, Datenbanken, alles was gesperrt ist: ein Häkchen kopiert aus einer Windows-Schattenkopie. |
| ⏰ **Läuft von selbst** | Jeder Auftrag hat seinen eigenen Zeitplan (täglich, wöchentlich, monatlich, sogar am letzten Tag des Monats) und läuft auch bei geschlossener App. |
| 🚨 **Meldet sich nur, wenn es zählt** | Farbige Symbole und klare Tooltips für fehlgeschlagene, veraltete oder unterbrochene Aufträge. Ein abgezogener Datenträger ist eine Sanduhr, kein Alarm. |
| 🏠 **Wirklich Ihres** | Kostenlos, quelloffen (MIT), vollständig lokal, keine Telemetrie. Fünf Sprachen. Portabel, wenn Sie möchten. |

## In zwei Minuten loslegen

1. Holen Sie sich die neueste Version von **[Releases](../../releases)**:
   - **`…-selfcontained.zip`**: entpacken und starten, nichts zu installieren (größerer Download);
   - **`…-framework-dependent.zip`**: deutlich kleiner, benötigt die kostenlose
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Entpacken Sie es in einen **beschreibbaren Ordner**, z. B. `D:\Programme\RoboKeep` (nicht
   `C:\Program Files`).
3. Starten Sie **`RoboKeep.exe`** → **Neu** → beantworten Sie die Fragen des Assistenten →
   **Alle starten**. Fertig.

> **Windows SmartScreen warnt Sie?** RoboKeep ist noch nicht digital signiert. Klicken Sie auf
> *Weitere Informationen* → *Trotzdem ausführen*, oder klicken Sie mit der rechten Maustaste auf
> die Zip-Datei → Eigenschaften → **Zulassen**, bevor Sie sie entpacken.

## Sehen Sie es in Aktion

| | |
|---|---|
| ![Geführte Einrichtung](docs/images/wizard.png) | **Geführte Einrichtung.** Ein paar Fragen, unter anderem ob Dateien beim Arbeiten geöffnet bleiben, und der Auftrag ist für Sie eingerichtet. |
| ![Auftrags-Editor](docs/images/editor.png) | **Alles im Griff.** Spiegeln oder Ansammeln, Kopieren offener Dateien, Optionen für große Dateien — jede in einer Zeile erklärt. |
| ![Befehlsvorschau und Versionierung](docs/images/editor-preview.png) | **Nichts versteckt.** Datierte Versionen, Ausschlüsse pro Auftrag und der exakte robocopy-Befehl stets im Blick. |
| ![Ausführungsverlauf](docs/images/history.png) | **Jeder Lauf im Protokoll.** Backups und Integritätsprüfungen nebeneinander; Doppelklick öffnet das vollständige Protokoll. |
| ![Zeitplanung pro Auftrag](docs/images/editor-schedule.png) | **Einstellen und vergessen.** Täglich, wöchentlich oder monatlich, plus regelmäßige Integritätsprüfungen. |
| ![Versionen durchsuchen](docs/images/versions.png) | **Ein Datum wählen.** Das Backup dieses Tages öffnet sich im Datei-Explorer; kopieren Sie zurück, was Sie brauchen. |

## Wie sich ein Backup verhält

Jeder Auftrag ist ein Paar aus Quellordner → Zielordner. Bei jedem Lauf macht RoboKeep Folgendes:

- **überspringt**, was sich nicht geändert hat (der zweite Lauf dauert nur Sekunden);
- **aktualisiert**, was in der Quelle neuer ist;
- im **Spiegel**-Modus (Standard) **entfernt** es aus dem Ziel auch, was Sie gelöscht haben;
- bei ausgeschaltetem Spiegel wird nur hinzugefügt und aktualisiert, **nie gelöscht**.

Mit aktivierten Versionen wird zuerst der vorherige Stand als datierter Schnappschuss gespeichert.

## Neuerungen in 1.8

- **Fenster Datenträgerzustand**: SMART für jeden Datenträger, in klaren Worten erklärt. NVMe ohne
  Abfragen; SATA und USB mit einer Administratorbestätigung — der einzige Weg durch USB-Gehäuse.
- **Optionale Updateprüfung**: RoboKeep fragt einmal, ob es auf GitHub nach neuen Versionen suchen
  darf. Gibt es eine, bietet ein Banner *Neuerungen*, *Herunterladen*, *Ignorieren*. Die Dateien
  ersetzen Sie selbst.
- **Einfacherer Assistent**: fragt nur, was zählt, speichert den Auftrag, sobald Sie fertig sind,
  und verwaltet Netzwerk-Anmeldedaten von selbst.
- Versionen bei einem bestehenden Auftrag zu aktivieren **kopiert nicht mehr alles neu**; ein
  abgebrochener Auftrag hinterlässt weiterhin ein Protokoll; monatliche Zeitpläne können am
  **letzten Tag des Monats** laufen.

Vollständige Historie im [CHANGELOG](CHANGELOG.md).

<details>
<summary><b>Alle Funktionen</b></summary>

**Sichern**: Spiegeln oder Ansammeln · Schutz bei Datenträger-Rotation (Auftrag per
Volume-Identität an seinen Datenträger gebunden) · datierte Versionen mit Hardlinks und
konfigurierbarer Aufbewahrung, keine doppelte Version, wenn sich nichts geändert hat · Kopieren
offener/gesperrter Dateien via VSS · Integritätsprüfung (SHA-256), auf Abruf oder periodisch ·
Stopp bei Hardwarefehler, der den Datenträger schont · automatische Thread-Begrenzung auf
mechanischen Festplatten · PC bleibt wach · Multithread-Kopieren · Ausschlüsse pro Auftrag ·
„Kopie erzwingen“ für Dateien, deren Datum/Größe sich nie ändern, mit optionalem
Inhalts-Hash-Modus · fortsetzbarer Modus für riesige Dateien · Vorschau/Trockenlauf.

**Hält Sie informiert**: Ausführungsverlauf mit einem Protokoll für jeden Eintrag, geöffnet im
Editor (Notepad) · Schaltfläche Log-Ordner · Zustandssymbole pro Auftrag mit klaren Tooltips ·
Symbole, die sich aktualisieren, sobald Sie einen Datenträger an- oder abstecken · Vorabprüfungen
(Ziel erreichbar, Speicherplatz, VSS- und Versions-Eignung, aktuelle Datenträgerfehler aus dem
Windows-Ereignisprotokoll) · Fenster Datenträgerzustand (SMART) · Echtzeit-Protokoll · gezipptes
Protokollarchiv mit automatischer Bereinigung · Toast-Benachrichtigungen und Infobereich ·
E-Mail-Berichte (SMTP), auf Wunsch nur bei Fehlern.

**Passt sich Ihnen an**: Assistent oder vollständiger manueller Editor · exakte Befehlsvorschau ·
Zeitplanung pro Auftrag (täglich / wöchentlich / monatlich / letzter Tag des Monats) ·
Netzwerkfreigaben mit DPAPI-verschlüsselten Anmeldedaten · Kopier-Drosselung für Netzwerk-Backups
· Konfiguration exportieren/importieren · Befehlszeile für Automatisierung · Aufträge per Ziehen
und Ablegen sortieren · optionale Updateprüfung · 5 Sprachen · portabler Modus.

</details>

<details>
<summary><b>Gut zu wissen</b></summary>

- **Nur Windows 10/11.** RoboKeep baut auf robocopy und andere Windows-eigene Funktionen.
- **Geänderte Dateien werden vollständig neu kopiert** (keine Block-/Delta-Kopie): gut für
  Dokumente und Fotos, teuer für einzelne riesige Dateien, die sich täglich ändern.
- **Versionen brauchen ein lokales NTFS-Ziel** (Hardlinks gibt es nicht auf exFAT oder
  Netzwerkfreigaben).
- **Kopieren offener Dateien braucht eine lokale NTFS-Quelle** und eine Administratorbestätigung
  (UAC) pro Lauf.
- **Integritätsprüfungen lesen jede Datei auf beiden Seiten neu ein**, sie dauern daher etwa so
  lange wie ein erstes Backup. Deshalb läuft die automatische Prüfung standardmäßig alle 7 Tage
  (0 = nach jedem Lauf).
- **Ein Hardwarefehler stoppt den Auftrag absichtlich.** Prüfen Sie vor einem Neustart Kabel,
  USB-Gehäuse und Stromversorgung (bei externen Datenträgern verursachen sie genau dieselben
  Fehler wie ein defekter Datenträger), und öffnen Sie dann **Datenträgerzustand**.
- Einstellungen, Ergebnisse und Protokolle liegen in `%APPDATA%\RoboKeep` und überstehen
  Aktualisierungen. Passwörter werden mit der Windows-DPAPI verschlüsselt; der Standardbereich ist
  maschinenweit, damit auch geplante Aufgaben sie lesen können. Auf einem gemeinsam genutzten PC
  wechseln Sie zum benutzerbezogenen Bereich.

</details>

<details>
<summary><b>Für Fortgeschrittene</b></summary>

```text
RoboKeep.exe --run-all              führt alle aktivierten Aufträge aus (Exit-Code 0 = alles gut)
RoboKeep.exe --job "Documents"      führt einen einzelnen Auftrag aus
RoboKeep.exe --run-all --dry-run    nur Vorschau, keine Änderungen
RoboKeep.exe --job "Photos" --config "D:\path\config.json"
```

Aus dem Quellcode bauen: `dotnet build src/RoboKeep.sln -c Release` (.NET 10 SDK); Tests:
`dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`. Portabler Modus: eine leere Datei
`portable.flag` neben der exe hält alles im App-Ordner. Beispielkonfiguration:
[config/config.example.json](config/config.example.json). Hintergrund und technische
Entscheidungen: [ANALISI.md](ANALISI.md) *(auf Italienisch)*.

</details>

## Datenschutz

RoboKeep sammelt **nichts**. Keine Telemetrie, keine Analyse, kein Konto. Der einzige
Netzwerkverkehr ist der, den Sie selbst aktivieren: die optionale Updateprüfung (eine Anfrage an
GitHub) und E-Mail-Berichte (Ihr SMTP-Server, standardmäßig TLS). Alles, was die App weiß, liegt
in lesbaren JSON-Dateien auf Ihrem PC.

## Code signing policy

Kostenlose Code-Signierung bereitgestellt von [SignPath.io](https://about.signpath.io),
Zertifikat der [SignPath Foundation](https://signpath.org) (*Free code signing provided by
SignPath.io, certificate by SignPath Foundation*). *Stand: Antrag eingereicht; Versionen bis
1.8.2 sind unsigniert, die erste signierte Version wird im Changelog genannt.*

- **Autoren und Reviewer**: [Roberto Serafini](https://github.com/robisera-ai)
- **Freigeber**: [Roberto Serafini](https://github.com/robisera-ai)

Nur der Release-Workflow ([`.github/workflows/release.yml`](.github/workflows/release.yml))
reicht Builds zur Signierung ein, von einem Tag auf `main`; ein auf einem Entwicklungsrechner
erstellter Build wird nie signiert und nie veröffentlicht. Jede Signieranfrage wird von Hand
freigegeben.

Datenschutz: RoboKeep sammelt nichts, siehe [Datenschutz](#datenschutz) oben und das
[Kapitel des Handbuchs](docs/guide/en/17-privacy-security.md).

## Mitwirken

Die Übersetzungen werden von der Community gepflegt: Ist Ihnen eine Formulierung aufgefallen, die
ein Muttersprachler anders sagen würde? [Pull Requests sind willkommen](../../pulls), auch eine
einzeilige Korrektur.

## Lizenz

**MIT-Lizenz**, siehe [LICENSE](LICENSE). Komponenten von Drittanbietern in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). © 2026 Roberto Serafini.
