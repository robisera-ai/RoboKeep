# RobocopySW — Obiettivo e scelte di progetto

Cosa si voleva ottenere e come è stato realizzato, sfruttando gli strumenti già presenti in Windows.

## Obiettivo

Partendo da alcuni semplici script batch personali costruiti attorno a **robocopy**, si voleva
un'**applicazione desktop con interfaccia grafica** per gestire i backup/mirroring: la stessa copia
affidabile di sempre, ma con la configurazione centralizzata in un unico file e modificabile da GUI,
senza più editare script a mano.

Comportamento di copia (per ogni job sorgente → destinazione, ricorsivo sulle sottocartelle):
- **salta** i file identici (stessa data/ora di ultima modifica e dimensione);
- **sovrascrive** in destinazione i file la cui sorgente è più recente;
- in **mirror** (`/MIR`, default) **rimuove** dalla destinazione i file/cartelle non più presenti in sorgente;
- con mirror disattivato (`/E`) copia e aggiorna soltanto, **senza mai cancellare**.

## Scelta di fondo: usare gli strumenti nativi di Windows

L'idea guida è **non reinventare nulla e non aggiungere dipendenze esterne**:

- **Motore di copia → il `robocopy` di sistema.** È già incluso in Windows, collaudato da anni,
  multi-thread e **sempre aggiornato con Windows Update**. L'app si limita a costruire il comando
  giusto, eseguirlo e leggerne l'esito — e ne mostra in **anteprima l'esatta riga di comando**, così
  non è una scatola nera. Si usa `%WINDIR%\System32\Robocopy.exe`, non un eseguibile incluso a mano.
- **Contorno → .NET nativo.** Compressione dei log (`System.IO.Compression`) e invio email
  (`System.Net.Mail`) fatti direttamente in .NET, **senza tool esterni** di archiviazione o di posta.
- **Piattaforma → .NET 10 (LTS) con WPF/MVVM.** Supporto a lungo termine e interfaccia desktop nativa Windows.

Perché non un programma di sincronizzazione già pronto (FreeFileSync, rsync e simili)? Perché si
voleva **restare sul robocopy già in uso** e avere un'app cucita su questi job, senza imparare né
dipendere da software di terzi: il valore aggiunto mancante non era il motore di copia, ma lo strato
di **GUI + configurazione** sopra robocopy.

## Design

App **WPF .NET 10 (MVVM)**, configurazione centralizzata in **un unico `config.json`** editabile da
GUI. Due modalità d'uso:
- **Interattiva (GUI):** gestione job, creazione guidata, avvio manuale, **anteprima/dry-run**, log e report.
- **Silenziosa (CLI):** `RobocopySW.exe --run-all` / `--job "Nome"` → per le attività pianificate di Task Scheduler.

### Architettura (3 progetti)
```
src/
  RobocopySW.Core/    libreria pura e testabile: Models + Services (engine, config, log, email, scheduler, credenziali, planner)
  RobocopySW/         app WPF (Views + ViewModels + entry/CLI) → eseguibile RobocopySW.exe
  RobocopySW.Tests/   xUnit (args builder, exit-code interpreter, config round-trip, planner forza copia/wizard, integrazione runner)
```

Il cuore della logica è **puro e testabile** (nessun I/O): `RobocopyArgsBuilder` (opzioni →
argomenti), `ExitCodeInterpreter` (exit code → esito), `ForceCopyPlanner` e `JobWizardPlanner`. L'I/O
(processo robocopy, file, rete, email, scheduler) è isolato nei servizi.

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

## Prodotto realizzato

- **Engine puro in TDD:** `RobocopyArgsBuilder`, `ExitCodeInterpreter`, `ConfigStore`,
  `ForceCopyPlanner`, `JobWizardPlanner`, con suite di unit/integration test xUnit verde.
- **Servizi IO:** runner robocopy (output live + cancellazione), log + archiviazione zip + pulizia,
  credenziali DPAPI + connessione UNC, email SMTP, scheduler.
- **GUI WPF (WPF-UI / Fluent):** lista job con drag &amp; drop ed esito, editor con anteprima del
  comando, creazione guidata, impostazioni (generale, email, credenziali, pianificazione).
- **CLI headless** per la schedulazione (`--run-all`, `--job`, `--dry-run`, `--config`).
- **Verifica end-to-end** su cartelle di prova e su backup reali.

Gli script batch iniziali non fanno parte del repository.
