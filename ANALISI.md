# RobocopySW — Documento di Analisi

> Data analisi: 19 giugno 2026 · Autore: rielaborazione del sistema batch esistente in applicazione .NET

## 1. Scopo

Sostituire l'attuale insieme di script batch (`Vecchi CMD/`) che pilotano **robocopy** per i backup/mirroring, con un'**applicazione desktop Windows configurabile da interfaccia grafica**, mantenendo invariato il comportamento di copia ma eliminando la modifica manuale dei file.

Comportamento di backup richiesto (invariato): per ogni coppia sorgente→destinazione, ricorsivo su sottocartelle, robocopy
- **salta** i file identici (stessa data/ora di ultima modifica e dimensione),
- **sovrascrive** in destinazione i file la cui sorgente è più recente,
- **rimuove** dalla destinazione i file/cartelle non più presenti in sorgente (modalità mirror).

## 2. Analisi del sistema esistente (`Vecchi CMD/`)

Sistema a 3 livelli in batch + robocopy:

| Livello | File rappresentativi | Ruolo |
|---|---|---|
| **Creator** | `scripts/Test/CreateJobs-v1.cmd`, `scripts/old/CreateRobocopyJobs.cmd` | Generano file job `.RCJ` con `robocopy … /save:` |
| **Executor** | `scripts/StartRobocopy.cmd` | Esegue un `.RCJ` con `robocopy /job:`, scrive il log, lo comprime in `.zip` con 7-Zip in `logs\AAAAMMGG\`, opzionale email via `vbs/sendmail.vbs` |
| **Orchestrator** | `scripts/LaunchRobocopyJobs.cmd` | Esegue in sequenza i job |

Switch robocopy usati nei `.RCJ` e negli script: `/MIR /XJ /COPY:DATS` (o `/COPYALL`) `/R:n /W:n /V /TEE`. Naming log: `AAAAMMGG-HHMMSS-<job>.log` → zip in `logs\AAAAMMGG\`. Logica esito da exit code in `sendmail.vbs`.

### Punti deboli rilevati
- **Configurazione sparsa e duplicata** su decine di `.cmd`, con percorsi *hardcoded* e incoerenti tra installazioni: `c:\BackupSW`, `f:\RobocopySW`, `D:\Programmi\RobocopySW`.
- Aggiungere/modificare un backup = editare a mano più file batch (fragile, error-prone).
- Dipendenze esterne incluse a mano: `bin/7z.exe`, `bin/robocopy.exe` (copia **datata**), `vbs/sendmail.vbs`.
- Nessuna anteprima prima di un mirror distruttivo; nessun multi-threading; gestione data/ora fragile (dipende dal formato locale di `DATE/T`/`TIME/T`).

## 3. Verifica tecnologica ("è aggiornato o c'è qualcosa di più nuovo?")

### 3.1 Motore di copia: robocopy
- **robocopy è ancora pienamente supportato e attuale.** Microsoft lo documenta per Windows 10/11 e Windows Server 2016→2025; **nessuna deprecazione** annunciata.
- È **parte integrante di Windows**: non esiste un pacchetto/versione scaricabile separatamente; si aggiorna **solo con Windows Update**. La versione interna del motore (`XP010`) è stabile da anni.
- Versione presente su **questa macchina** (Windows 11, build 26100): **`Robocopy.exe 10.0.26100.8457`** → è la **più recente** disponibile per questo sistema.
- Decisione: l'app userà **il robocopy di sistema** (`%WINDIR%\System32\Robocopy.exe`), così erediterà sempre l'ultima versione fornita da Windows. Il `robocopy.exe` datato in `Vecchi CMD/bin/` **non** verrà usato.

### 3.2 Alternative valutate (e perché restiamo su robocopy)
| Alternativa | Tipo | Verdetto |
|---|---|---|
| **FreeFileSync** | GUI sync open source | Ottimo ma è un prodotto a sé: non riusa la logica/job esistenti, ennesima UI di terzi da imparare |
| **rsync / cwRsync** | CLI Unix portata | Potente ma estraneo all'ecosistema, semantica e permessi NTFS meno naturali su Windows |
| **FastCopy / TeraCopy** | GUI copia veloce | Orientati a copie manuali, non a mirroring schedulato con log/credenziali |
| **Rclone** | CLI cloud/sync | Eccellente per cloud, sovradimensionato per mirror locale/UNC |
| **Macrium / Acronis / Veeam** | Backup commerciale | Backup a immagine/incrementale, a pagamento, diverso caso d'uso |
| **robocopy + GUI custom (questa soluzione)** | wrapper nativo | ✅ Mantiene **identico** il comportamento già in uso, **zero dipendenze nuove**, nativo, e aggiunge proprio lo strato GUI/config mancante |

**Conclusione:** la cosa "più nuova e indicata" non è cambiare motore, ma **incapsulare robocopy in un'app moderna**. È esattamente l'approccio adottato.

### 3.3 Piattaforma applicativa: .NET
- Inizialmente installato **.NET 8 SDK**, ma **.NET 8 va in end-of-life il 10 novembre 2026** (≈5 mesi).
- **.NET 10 è l'attuale LTS**, supportato fino al **10 novembre 2028**. Su questa macchina: SDK **10.0.301**, runtime WindowsDesktop **10.0.9**.
- Decisione: **target `net10.0` / `net10.0-windows`**. Il .NET 8 SDK è stato **disinstallato** (non più necessario).
- Compressione log e invio email vengono fatti **nativamente in .NET** (`System.IO.Compression`, `System.Net.Mail`): si eliminano `7z.exe` e `sendmail.vbs`.

## 4. Nuovo design

App **WPF .NET 10 (MVVM)**, configurazione centralizzata in **un unico `config.json`** editabile da GUI. Robocopy come motore. Due modalità d'uso:
- **Interattiva (GUI):** gestione job, avvio manuale, **anteprima/dry-run**, log e report.
- **Silenziosa (CLI):** `RobocopySW.exe --run-all` / `--job "Nome"` → per le attività pianificate di Task Scheduler.

### Architettura (3 progetti)
```
src/
  RobocopySW.Core/    libreria pura e testabile: Models + Services (engine, config, log, email, scheduler, credenziali)
  RobocopySW/         app WPF (Views + ViewModels + entry/CLI) → eseguibile RobocopySW.exe
  RobocopySW.Tests/   xUnit (args builder, exit-code interpreter, config round-trip)
```

### Mappatura opzioni → switch robocopy (`RobocopyArgsBuilder`)
| Opzione (GUI) | Switch |
|---|---|
| Mirror = on (default) | `/MIR` (cancella in dest ciò che non c'è in sorgente) |
| Mirror = off | `/E` (copia/aggiorna ricorsivo, **non** cancella) |
| Non sovrascrivere file più recenti in dest | `/XO` |
| Copia ACL/owner (utile su share) | `/COPYALL` o `/COPY:DAT` (default) |
| Esclude junction (anti-loop) | `/XJ` (sempre) |
| Multi-thread | `/MT:<n>` |
| Esclusioni file / cartelle | `/XF …` / `/XD …` |
| Retry / attesa | `/R:<n>` / `/W:<n>` |
| **Anteprima** | `/L` (elenca soltanto, non modifica) |

### Funzionalità (opzionali, scelta utente)
Log zip per data + pulizia automatica oltre N giorni · notifiche email (sempre/solo errori) · schedulazione via Task Scheduler · report riepilogo (copiati/sovrascritti/cancellati/errori da exit code) · credenziali per share UNC cifrate con **DPAPI**.

## 5. Stato e roadmap
- [x] Verifica tecnologica (robocopy attuale, .NET 10 LTS)
- [x] Scaffold soluzione .NET 10 (build verde)
- [ ] Modelli + engine puro in TDD (`RobocopyArgsBuilder`, `ExitCodeInterpreter`, `ConfigStore`)
- [ ] Servizi IO (runner, log, credenziali, email, scheduler)
- [ ] GUI WPF (lista job, editor, impostazioni, anteprima/log)
- [ ] CLI headless per schedulazione
- [ ] Build + unit test + verifica end-to-end su cartelle di prova

La cartella `Vecchi CMD/` resta intatta come riferimento storico.
