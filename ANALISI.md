# RobocopySW — Documento di Analisi e Design

Analisi che ha portato al prodotto: dal sistema a script batch esistente all'applicazione .NET
finita, con le scelte tecniche e il design realizzato.

## 1. Scopo

Sostituire l'insieme di script batch che pilotano **robocopy** per i backup/mirroring, con un'**applicazione desktop Windows configurabile da interfaccia grafica**, mantenendo invariato il comportamento di copia ma eliminando la modifica manuale dei file.

Comportamento di backup richiesto (invariato): per ogni coppia sorgente→destinazione, ricorsivo su sottocartelle, robocopy
- **salta** i file identici (stessa data/ora di ultima modifica e dimensione),
- **sovrascrive** in destinazione i file la cui sorgente è più recente,
- **rimuove** dalla destinazione i file/cartelle non più presenti in sorgente (modalità mirror).

## 2. Analisi del sistema esistente (script batch)

Sistema a 3 livelli in batch + robocopy:

| Livello | Ruolo |
|---|---|
| **Creator** | Genera file job `.RCJ` con `robocopy … /save:` |
| **Executor** | Esegue un `.RCJ` con `robocopy /job:`, scrive il log, lo comprime in `.zip` con 7-Zip in `logs\AAAAMMGG\`, opzionale email via `vbs/sendmail.vbs` |
| **Orchestrator** | Esegue in sequenza i job |

Switch robocopy usati nei `.RCJ`: `/MIR /XJ /COPY:DATS` (o `/COPYALL`) `/R:n /W:n /V /TEE`. Naming log: `AAAAMMGG-HHMMSS-<job>.log` → zip in `logs\AAAAMMGG\`. Logica esito da exit code in `sendmail.vbs`.

### Punti deboli rilevati
- **Configurazione sparsa e duplicata** su decine di `.cmd`, con percorsi *hardcoded* e incoerenti tra installazioni.
- Aggiungere/modificare un backup = editare a mano più file batch (fragile, error-prone).
- Dipendenze esterne incluse a mano: `7z.exe`, un `robocopy.exe` **datato**, `sendmail.vbs`.
- Nessuna anteprima prima di un mirror distruttivo; nessun multi-threading; gestione data/ora fragile (dipende dal formato locale di `DATE/T`/`TIME/T`).

## 3. Verifica tecnologica ("è aggiornato o c'è qualcosa di più nuovo?")

### 3.1 Motore di copia: robocopy
- **robocopy è pienamente supportato e attuale.** Documentato da Microsoft per Windows 10/11 e Windows Server fino alle versioni più recenti; **nessuna deprecazione** annunciata.
- È **parte integrante di Windows**: non esiste un pacchetto scaricabile separatamente; si aggiorna **solo con Windows Update**. Il motore interno è stabile da anni.
- Decisione: l'app usa **il robocopy di sistema** (`%WINDIR%\System32\Robocopy.exe`), così eredita sempre l'ultima versione fornita da Windows. Il `robocopy.exe` datato dei vecchi script **non** viene usato.

### 3.2 Alternative valutate (e perché restiamo su robocopy)
| Alternativa | Tipo | Verdetto |
|---|---|---|
| **FreeFileSync** | GUI sync open source | Ottimo ma è un prodotto a sé: non riusa la logica/job esistenti, ennesima UI di terzi da imparare |
| **rsync / cwRsync** | CLI Unix portata | Potente ma estraneo all'ecosistema, semantica e permessi NTFS meno naturali su Windows |
| **FastCopy / TeraCopy** | GUI copia veloce | Orientati a copie manuali, non a mirroring schedulato con log/credenziali |
| **Rclone** | CLI cloud/sync | Eccellente per cloud, sovradimensionato per mirror locale/UNC |
| **Macrium / Acronis / Veeam** | Backup commerciale | Backup a immagine/incrementale, a pagamento, diverso caso d'uso |
| **robocopy + GUI custom (questa soluzione)** | wrapper nativo | ✅ Mantiene **identico** il comportamento già in uso, **zero dipendenze nuove**, nativo, e aggiunge proprio lo strato GUI/config mancante |

**Conclusione:** la cosa "più nuova e indicata" non è cambiare motore, ma **incapsulare robocopy in un'app moderna**. È l'approccio adottato.

### 3.3 Piattaforma applicativa: .NET
- **.NET 10 è l'attuale LTS** (supporto a lungo termine), preferito a .NET 8 che è prossimo al fine vita. Target: **`net10.0` / `net10.0-windows`**.
- Compressione log e invio email vengono fatti **nativamente in .NET** (`System.IO.Compression`, `System.Net.Mail`): si eliminano `7z.exe` e `sendmail.vbs`.

## 4. Design realizzato

App **WPF .NET 10 (MVVM)**, configurazione centralizzata in **un unico `config.json`** editabile da GUI. Robocopy come motore. Due modalità d'uso:
- **Interattiva (GUI):** gestione job, creazione guidata, avvio manuale, **anteprima/dry-run**, log e report.
- **Silenziosa (CLI):** `RobocopySW.exe --run-all` / `--job "Nome"` → per le attività pianificate di Task Scheduler.

### Architettura (3 progetti)
```
src/
  RobocopySW.Core/    libreria pura e testabile: Models + Services (engine, config, log, email, scheduler, credenziali, planner)
  RobocopySW/         app WPF (Views + ViewModels + entry/CLI) → eseguibile RobocopySW.exe
  RobocopySW.Tests/   xUnit (args builder, exit-code interpreter, config round-trip, planner forza copia/wizard, integrazione runner)
```

Il cuore della logica è **puro e testabile** (nessun I/O): `RobocopyArgsBuilder` (opzioni → argomenti), `ExitCodeInterpreter` (exit code → esito), `ForceCopyPlanner` e `JobWizardPlanner`. L'I/O (processo robocopy, file, rete, email, scheduler) è isolato nei servizi.

### Mappatura opzioni → switch robocopy (`RobocopyArgsBuilder`)
| Opzione (GUI) | Switch |
|---|---|
| Mirror = on (default) | `/MIR` (cancella in dest ciò che non c'è in sorgente) |
| Mirror = off | `/E` (copia/aggiorna ricorsivo, **non** cancella) |
| Non sovrascrivere file più recenti in dest | `/XO` |
| Copia ACL/owner (utile su share) | `/COPYALL` o `/COPY:DAT` (default) |
| Esclude junction (anti-loop) | `/XJ` (sempre) |
| Multi-thread | `/MT:<n>` |
| File grandi — riavviabile / I/O non bufferizzato | `/Z` / `/J` (mutuamente esclusivi) |
| Esclusioni file / cartelle | `/XF …` / `/XD …` |
| **Forza copia** (file a data/dimensione congelate) | seconda passata `/IS /IT` sui soli pattern indicati |
| Registra tutti i file nel log | `/V` |
| Retry / attesa | `/R:<n>` / `/W:<n>` |
| **Anteprima** | `/L` (elenca soltanto, non modifica) |

**Forza copia — modalità smart:** per i file il cui contenuto cambia senza variare data/dimensione
(container cifrati, DB), una seconda passata li ricopia comunque. In modalità *smart* l'app calcola
un **hash SHA256** del file e lo ricopia solo se è davvero cambiato dall'ultimo backup (stato
persistito), evitando ricopie inutili.

**Creazione guidata (wizard):** poche domande (tipo di dischi sorgente/destinazione, comportamento,
file speciali) → `JobWizardPlanner` deriva le opzioni ottimali (es. `/MT` in base al disco più lento,
`/Z` per i file grandi, `/COPYALL` solo se servono i permessi) e **precompila l'editor**.

### Funzionalità (opzionali, scelta utente)
Log `.zip` per data + pulizia automatica oltre N giorni · notifiche email (sempre / solo errori) ·
report riepilogo (copiati / saltati / extra / falliti da exit code) · credenziali per share UNC
cifrate con **DPAPI** (ambito utente o macchina) con test di connessione · pianificazione via Task
Scheduler · riordino job in **drag &amp; drop** · ultimo esito persistito e visibile in lista (anche
dopo i run pianificati) · interfaccia in **5 lingue** (it/en/es/fr/de).

## 5. Prodotto realizzato

- **Engine puro in TDD:** `RobocopyArgsBuilder`, `ExitCodeInterpreter`, `ConfigStore`,
  `ForceCopyPlanner`, `JobWizardPlanner`, con suite di unit/integration test xUnit verde.
- **Servizi IO:** runner robocopy (output live + cancellazione), log + archiviazione zip + pulizia,
  credenziali DPAPI + connessione UNC, email SMTP, scheduler.
- **GUI WPF (WPF-UI / Fluent):** lista job con drag &amp; drop ed esito, editor con anteprima del
  comando, creazione guidata, impostazioni (generale, email, credenziali, pianificazione).
- **CLI headless** per la schedulazione (`--run-all`, `--job`, `--dry-run`, `--config`).
- **Verifica end-to-end** su cartelle di prova e su backup reali.

Gli script batch originali sono stati rimossi dal repository (anonimizzazione dei riferimenti interni).
