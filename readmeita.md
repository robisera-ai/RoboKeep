# RoboKeep

*Leggi in: [English](README.md) · Italiano*

**Una GUI moderna per `robocopy`: backup e mirroring su Windows, semplici, trasparenti e affidabili.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Lingue](https://img.shields.io/badge/lingue-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)

RoboKeep mette una GUI moderna e una configurazione centralizzata sopra `robocopy`, lo
strumento di copia di Windows: definisci i tuoi backup (sorgente → destinazione) una volta, e li
avvii a mano, da riga di comando o pianificati — senza scrivere né tenere aggiornati script a mano.

![Finestra principale di RoboKeep](docs/images/main-window.jpg)

*Lista dei job con l'ultimo esito a colpo d'occhio e il log di esecuzione in tempo reale.*

## Perché RoboKeep

- **Motore collaudato, non reinventato.** La copia la fa il `robocopy` di Windows: veloce,
  multi-thread, affidabile, sempre aggiornato col sistema. RoboKeep ci mette sopra comodità e
  chiarezza, non un nuovo algoritmo di copia da fidarsi al buio.
- **Trasparente.** L'editor mostra in tempo reale **l'esatto comando robocopy** che verrà eseguito:
  nessuna scatola nera, sai sempre cosa succede.
- **Una sola configurazione.** Tutti i job in un unico file JSON, editabili da GUI, invece che
  scritti e duplicati a mano.
- **Creazione guidata.** Un wizard ti fa poche domande (tipo di dischi, comportamento, file
  speciali) e **imposta le opzioni ottimali**, evitando gli errori classici di robocopy.
- **Locale, gratuito, senza cloud.** Nessuna telemetria, nessun account. Le credenziali delle share
  di rete sono cifrate con DPAPI di Windows.
- **Portatile.** App, configurazione e log nella stessa cartella: copi la cartella e hai spostato tutto.
- **Multilingua:** italiano, inglese, spagnolo, francese, tedesco.

## Come funziona un backup

Per ogni job (coppia sorgente → destinazione, ricorsivo sulle sottocartelle):

- **salta** i file identici (stessa data/ora e dimensione);
- **sovrascrive** in destinazione i file la cui sorgente è più recente;
- in **mirror** (`/MIR`, default) **rimuove** dalla destinazione i file/cartelle non più presenti in sorgente;
- con mirror disattivato (`/E`) copia e aggiorna soltanto, **senza mai cancellare**.

## Requisiti

- **Windows 10 o 11** (usa il `robocopy` di sistema).
- **.NET 10 Desktop Runtime** — serve **solo** per il download *framework-dependent*; il download
  *self-contained* include già .NET (10.0.9) e non richiede nulla. Il **.NET 10 SDK** serve solo per
  compilare dai sorgenti.

## Installazione

### Opzione A — Release pronta all'uso (consigliata)

1. Scarica l'ultima versione dalla pagina **[Releases](../../releases)** del progetto. Sono disponibili due build:
   - **`…-selfcontained.zip`** — include **.NET 10.0.9**: estrai e avvia, **niente da installare** (download più grande).
   - **`…-framework-dependent.zip`** — download piccolo, ma richiede il
     **[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)**.
2. Estrai lo `.zip` in una cartella **scrivibile** (es. `D:\Programmi\RoboKeep`).
   > Evita `C:\Program Files` (sola lettura per gli utenti): se la metti lì, imposta percorsi
   > log/temp scrivibili dalle Impostazioni.
3. Avvia **`RoboKeep.exe`**. Nessuna installazione: l'app è portatile.

Se hai usato la build framework-dependent e Windows segnala la mancanza del runtime, installa il
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) e riprova.

### Opzione B — Compila dai sorgenti

```powershell
git clone https://github.com/robisera-ai/RoboKeep.git
cd RoboKeep
dotnet build src/RoboKeep.sln -c Release
dotnet test  src/RoboKeep.Tests/RoboKeep.Tests.csproj   # facoltativo
# eseguibile in: src/RoboKeep/bin/Release/net10.0-windows/RoboKeep.exe
```

## Uso

### Interfaccia grafica

Avvia `RoboKeep.exe` senza argomenti. In breve:

1. **Nuovo** → la **creazione guidata** ti accompagna in 5 passi (dati di base, tipo di dischi
   sorgente/destinazione, comportamento, casi speciali, riepilogo) e precompila l'editor con le
   opzioni consigliate. In alternativa **Salta e configura a mano**.
2. Controlla l'**anteprima del comando robocopy** nell'editor, poi **Salva**.
3. **Anteprima** (dry-run) per vedere cosa verrebbe copiato/cancellato **senza toccare nulla**;
   quando sei sicuro, **Avvia selezionato** o **Avvia tutti**. Il log scorre in tempo reale.

![Creazione guidata di un nuovo job](docs/images/wizard.jpg)

*La creazione guidata: poche domande e le opzioni robocopy vengono impostate per te.*

![Editor con anteprima del comando robocopy](docs/images/editor-preview.jpg)

*L'editor mostra in tempo reale l'esatto comando robocopy che verrà eseguito.*

### Riga di comando (per la pianificazione)

```text
RoboKeep.exe --run-all              Esegue tutti i job abilitati
RoboKeep.exe --job "Documenti"      Esegue un singolo job
RoboKeep.exe --run-all --dry-run    Anteprima (nessuna modifica)
RoboKeep.exe --job "Foto" --config "D:\percorso\config.json"
```

Exit code: `0` tutti i job riusciti · `1` almeno un errore · `2` job non trovato.

La pianificazione si crea dalle **Impostazioni → Pianificazione** (usa l'Utilità di pianificazione
di Windows) e lancia l'app con `--run-all` all'orario scelto.

## Funzioni

- **Creazione guidata** del job, con spiegazioni e opzioni consigliate per tipo di disco/dati.
- **Editor** completo con **anteprima del comando** e **log live**.
- **Anteprima / dry-run** (`/L`): mostra le azioni senza modificare nulla.
- **Mirror** (`/MIR`) o **copia/accumulo** (`/E`); **non sovrascrivere i più recenti** (`/XO`);
  **copia ACL/owner** (`/COPYALL`).
- **Multi-thread** (`/MT`), **esclusioni** file e cartelle per job.
- **File grandi:** modalità **riavviabile** (`/Z`, riprende le copie interrotte) o **I/O non
  bufferizzato** (`/J`).
- **Forza copia:** ricopia i file a **data/dimensione congelate** (container cifrati, DB) anche
  quando robocopy li salterebbe; modalità **smart** che ricopia solo se l'hash del contenuto è cambiato.
- **Log dettagliato** opzionale (`/V`, registra anche i file saltati).
- **Log per job** compressi in `.zip`, archiviati per data, con **pulizia automatica**.
- **Notifiche email** (SMTP) con esito e **report** conteggi (copiati / saltati / extra / falliti).
- **Credenziali** per share di rete UNC, cifrate con **DPAPI** (ambito utente o macchina), con
  test di connessione.
- **Pianificazione** integrata via Utilità di pianificazione di Windows.
- Riordino dei job con **drag &amp; drop**; ultimo esito visibile in lista (anche dopo i run pianificati).

## Limitazioni

- **Solo Windows:** dipende da `robocopy`. Niente versione macOS/Linux.
- **Nessuna copia a blocchi/delta:** quando un file cambia, robocopy lo ricopia **per intero**. Per
  file molto grandi che cambiano spesso il costo è quello del trasferimento completo.
- **Non è un sistema di versioni/snapshot:** è mirror/copia, non conserva versioni storiche dei file.
  Per il versioning serve uno strumento dedicato.
- La **pianificazione** e l'opzione credenziali “ambito utente” richiedono che l'attività giri con
  l'utente adeguato; alcune azioni (es. `/COPYALL`) possono richiedere privilegi sufficienti.

## Configurazione

Modello **portatile**: per default `config.json`, `logs\` e `temp\` stanno **nella stessa cartella
dell'eseguibile**. Esempio in **[config/config.example.json](config/config.example.json)**.

Le password (credenziali di rete ed email) non sono mai in chiaro: vengono cifrate con **DPAPI**.
Il file `config.json` reale e `lastresults.json` restano locali (non versionati).

## Approfondimenti

Vedi **[ANALISI.md](ANALISI.md)** per l'obiettivo del progetto e le scelte tecniche (perché robocopy e .NET nativi).

## Licenza

Distribuito con licenza **MIT** — vedi il file [LICENSE](LICENSE). © 2026 Roberto Serafini.
