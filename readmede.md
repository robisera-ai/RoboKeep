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

### Einfach im Start, sparsam mit dem Platz

| | |
|---|---|
| 🧙 **Geführte Einrichtung** | Beantworten Sie ein paar einfache Fragen, und RoboKeep richtet alles bestmöglich ein. Profis behalten die volle Kontrolle.<br>*✓ Einfach loslegen* |
| 🕰️ **Verlauf ohne Verschwendung** | Datierte Versionen, die nur die geänderten oder gelöschten Dateien aufbewahren.<br>*✓ Zehn Stände im Platz von einem* |
| ↩️ **Punktgenaue Wiederherstellung** | Holen Sie eine Datei, einen Ordner oder den ganzen Auftrag so zurück, wie er an einem bestimmten Datum war – wohin Sie möchten.<br>*✓ Sie wählen was und wann* |

### Höchste Rücksicht auf die Hardware, Gewissheit beim Datenträger

| | |
|---|---|
| 💿 **Rücksicht auf Ihre Datenträger** | Sofortiger Stopp beim ersten Hardwarefehler, um den Datenträger zu schonen. Der PC bleibt während des Backups wach.<br>*✓ Datenträger geschützt* |
| 🩺 **SMART-Zustand sofort** | Der Zustand des Datenträgers mit klaren Urteilen und jedem Wert erklärt.<br>`Gut` · `Achtung` · `Gefahr` |
| 🛡️ **Erkennt den eigenen Datenträger** | Zwei externe Datenträger, die Windows beide `E:` nennt? Jeder Auftrag erkennt seinen eigenen und wartet einfach auf ihn.<br>*✓ Nie der falsche Datenträger* |

### Volle Zuverlässigkeit, Privatsphäre und keine Bindung

| | |
|---|---|
| ✅ **SHA-256-Prüfung und offene Dateien** | Deckt stille Beschädigung auf und erfasst gesperrte Dateien oder Outlook-Archive über Schattenkopien (VSS).<br>*✓ Der Beweis, dass es geklappt hat* |
| ⏰ **Unauffällige Eigenständigkeit** | Zeitplan für jeden Auftrag, auch bei geschlossener App. Zurückhaltende Benachrichtigungen, nur wenn sie zählen.<br>*✓ Läuft von selbst* |
| 🏠 **100 % Ihres und quelloffen** | MIT-Lizenz, keine Telemetrie, alles lokal. Fünf Sprachen und eine portable Version, sofort einsatzbereit.<br>*✓ Ihre Dateien, unter Ihrer Kontrolle* |

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
| ![Versionen durchsuchen](docs/images/versions.png) | **Ein Datum wählen.** Was dieses Backup ersetzt oder gelöscht hat, öffnet sich im Datei-Explorer; *Wiederherstellen* baut den ganzen Baum wieder auf. |

## Wie sich ein Backup verhält

Jeder Auftrag ist ein Paar aus Quellordner → Zielordner. Bei jedem Lauf macht RoboKeep Folgendes:

- **überspringt**, was sich nicht geändert hat (der zweite Lauf dauert nur Sekunden);
- **aktualisiert**, was in der Quelle neuer ist;
- im **Spiegel**-Modus (Standard) **entfernt** es aus dem Ziel auch, was Sie gelöscht haben;
- bei ausgeschaltetem Spiegel wird nur hinzugefügt und aktualisiert, **nie gelöscht**.

Mit aktivierten Versionen wird der vorherige Stand der Dateien, die ersetzt oder gelöscht werden,
zuerst in einer datierten Version beiseitegelegt.

Vollständige Historie im [CHANGELOG](CHANGELOG.md).

<details>
<summary><b>Alle Funktionen</b></summary>

**Sichern**: Spiegeln oder Ansammeln · Schutz bei Datenträger-Rotation (Auftrag per
Volume-Identität an seinen Datenträger gebunden) · datierte Versionen auf jedem Datenträger mit
konfigurierbarer Aufbewahrung, keine leere Version, wenn sich nichts geändert hat · Kopieren
offener/gesperrter Dateien via VSS · Integritätsprüfung (SHA-256), auf Abruf oder periodisch ·
Stopp bei Hardwarefehler, der den Datenträger schont · automatische Thread-Begrenzung auf
mechanischen Festplatten · PC bleibt wach · Multithread-Kopieren · Ausschlüsse pro Auftrag ·
„Kopie erzwingen“ für Dateien, deren Datum/Größe sich nie ändern, mit optionalem
Inhalts-Hash-Modus · fortsetzbarer Modus für riesige Dateien · Vorschau/Trockenlauf.

**Hält Sie informiert**: Ausführungsverlauf mit einem Protokoll für jeden Eintrag, geöffnet im
Editor (Notepad) · Schaltfläche Log-Ordner · Zustandssymbole pro Auftrag mit klaren Tooltips ·
Symbole, die sich aktualisieren, sobald Sie einen Datenträger an- oder abstecken · Vorabprüfungen
(Ziel erreichbar, Speicherplatz, VSS-Eignung, aktuelle Datenträgerfehler aus dem
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

Die Releases sind derzeit **nicht signiert**: Windows SmartScreen zeigt beim ersten Start einer
neuen Version „unbekannter Herausgeber“ (siehe *In zwei Minuten loslegen*). Geplant ist die
Signierung über die [SignPath Foundation](https://signpath.org) (kostenlose Code-Signierung für
Open Source, Zertifikat im Namen der Foundation), die eine öffentliche Sichtbarkeit verlangt, die
das Projekt noch nicht hat: der Antrag wird gestellt, sobald es sie hat. Die erste signierte
Version wird im Changelog genannt.

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
