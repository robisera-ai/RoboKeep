# Spec — Feature "Forza copia" (file a data/dimensione congelate)

Data: 2026-06-26
Stato: approvato (design)

## Problema

Robocopy decide se un file è cambiato confrontando **solo data di ultima modifica + dimensione**.
Non legge il contenuto e non calcola hash. Alcuni file cambiano nel contenuto **senza**
modificare data e dimensione:

- volumi/container a dimensione preallocata (VeraCrypt/TrueCrypt con "Preserve modification
  timestamp" attivo, immagini disco, VHD/VHDX);
- database e archivi applicativi a dimensione fissa (es. alcuni `.pst`, file DB).

Per questi file robocopy li considera sempre "identici" e li **salta**: il backup non cattura mai
le modifiche interne.

Caso correlato: un file grande la cui copia viene **interrotta** può, alla copia parziale, risultare
di nuovo "valido" (data/dimensione coincidenti) e venire saltato al run successivo.

## Soluzione scelta

Una lista **"Forza copia"** per-job (pattern per nome/estensione), realizzata come **seconda passata**
di robocopy ristretta a quei soli file, con `/IS /IT` (copia anche se identico). È la modalità
**semplice "copia sempre"** (variante A): ogni file che corrisponde ai pattern viene **ricopiato per
intero a ogni backup**. Tutto il resto del job resta incrementale.

Scelte di scoping decise con l'utente:

- I file **molto grandi** (es. container VeraCrypt da 100 GB) **non** vanno in questa lista: si
  risolvono **alla fonte** (in VeraCrypt: Settings → Preferences → togliere "Preserve modification
  timestamp of file containers", così la data si aggiorna allo smonta e robocopy li copia solo quando
  cambiano). La lista "Forza copia" è pensata per file **piccoli/medi** a metadati congelati.
- La variante **B "smart" (hash)** — ricopia solo se il contenuto è davvero cambiato — è
  **rimandata**: ha senso solo per file grandi che cambiano di rado, qui non necessari. Eventuale
  aggiunta futura.

La seconda passata copre **di riflesso** anche il caso del file interrotto (viene comunque ricopiato).
Per il caso generale di interruzione resta valida l'opzione già esistente `/Z` (Restartable).

## Design

### Modello — `BackupJob`
Aggiungere:

```csharp
/// <summary>Pattern di file da ricopiare sempre, anche se data/dimensione non cambiano
/// (seconda passata robocopy con /IS /IT). Es. *.pst, database.dat.</summary>
public List<string> ForceCopyFiles { get; set; } = new();
```

### Motore — `RobocopyArgsBuilder`
Nuovo metodo per la passata forzata, distinto e puro/testabile:

```csharp
public static IReadOnlyList<string> BuildForceCopyPass(
    BackupJob job, bool dryRun = false, string? logFile = null)
```

Comando generato (ordine: `<src> <dst> <pattern...> [opzioni]`):

- pattern di `ForceCopyFiles` come argomenti posizionali dopo sorgente/destinazione;
- `/E` (ricorsivo, include sottocartelle) — **mai** `/MIR` (la passata forzata non cancella nulla:
  una passata filtrata che fa purge sarebbe pericolosa);
- `/IS` (include same) e `/IT` (include tweaked) → forza la copia anche per file identici;
- **mai** `/XO` (non deve saltare i "più vecchi": deve copiare comunque);
- `/XJ`, `/COPY:DAT` o `/COPYALL` (come `job.CopyAll`), `/MT:n`, `/Z` o `/J` (come il job),
  `/R:n`, `/W:n` — coerenti con la passata principale;
- `/L` se `dryRun`;
- `/TEE` + `/LOG:<file>` se `logFile` valorizzato (per coerenza con `Build`, anche se oggi il runner
  cattura da stdout).

La passata principale (`Build`) resta **invariata**.

### Esecuzione — `RobocopyRunner`
In `RunAsync`, dopo la passata 1:

- se `job.ForceCopyFiles` è **vuoto** → comportamento identico a oggi (nessuna passata aggiuntiva,
  zero overhead);
- altrimenti esegue la **passata 2** con `BuildForceCopyPass(job, dryRun)`, riusando lo stesso
  meccanismo di avvio processo + cattura output (stdout della passata 2 **appeso** allo stesso
  `output`), rispettando la stessa `CancellationToken`.

Aggregazione del `JobResult`:

- conteggi (`FilesCopied`, `FilesSkipped`, `FilesExtra`, `FilesFailed`, `DirsCopied`, `DirsFailed`)
  = **somma** delle due passate;
- `ExitCode` = **OR bit a bit** dei due exit code (gli esiti robocopy 0–16 sono bitfield: 1=copiati,
  2=extra, 4=mismatch, 8=falliti, 16=fatale; l'OR preserva l'esito peggiore);
- `Status`/`Success` = `ExitCodeInterpreter.Interpret(exitCombinato)`;
- `Duration` = durata totale (entrambe le passate).

Refactor minimo previsto: estrarre la logica "avvia un processo robocopy con questi argomenti e
restituisci (exitCode, output)" in un metodo privato riusato dalle due passate, per non duplicare il
setup di `ProcessStartInfo`/encoding/cancellazione.

### UI — `JobEditorWindow` + `JobEditorViewModel`
- Nuova proprietà `ForceCopyFilesText` nel ViewModel (stesso pattern di `ExcludeFilesText`:
  split/join per righe, `RaisePreview()` sul setter).
- Nuovo riquadro **"Forza copia (ignora data/dimensione)"** sotto i due riquadri "Escludi",
  a tutta larghezza, con `InfoHint` che riporta il caveat:
  *"Questi file vengono ricopiati interi a ogni backup. Adatto a file piccoli/medi a data/dimensione
  fisse (DB, archivi). Per file molto grandi correggi alla fonte (es. impostazioni VeraCrypt)."*
  `PlaceholderText` esempio: `*.pst`.
- `CommandPreview`: quando `ForceCopyFiles` è valorizzato, mostrare **anche** la riga della passata 2
  (es. due righe: passata principale + passata forzata).

### Localizzazione — `Loc.cs`
Nuove chiavi in tutte e 5 le lingue (IT/EN/ES/FR/DE):

- `Editor_ForceCopy` (label riquadro)
- `Editor_ForceCopyTip` (testo `InfoHint`/caveat)

### Persistenza — `ConfigStore`
Nessuna modifica strutturale: `ForceCopyFiles` è una nuova proprietà serializzata con il resto del job
(default lista vuota → retro-compatibile con i config esistenti).

## Test

In `RobocopyArgsBuilderTests`:

- `BuildForceCopyPass` include `/IS`, `/IT` e i pattern indicati;
- `BuildForceCopyPass` **non** contiene `/MIR` né `/XO`;
- `BuildForceCopyPass` rispetta `/MT`, `/COPYALL`, `/Z`/`/J` come il job;
- `dryRun` aggiunge `/L`;
- coerenza sorgente/destinazione come posizionali iniziali.

(L'aggregazione del runner — due processi reali — non è coperta da unit test del processo; la logica di
OR/somma resta semplice e verificabile a mano nel test end-to-end.)

## Verifica end-to-end

1. Creare un file a metadati "congelati" (es. scrivere contenuto diverso mantenendo data e dimensione)
   in una sorgente di prova; aggiungere il suo nome/estensione alla lista "Forza copia".
2. Backup → il file viene **copiato** nonostante data/dimensione invariate (passata 2).
3. Un file non in lista, identico, resta **saltato** (incrementale invariato).
4. Lista vuota → nessuna passata aggiuntiva, esito identico a prima.
5. Esito/conteggi nel report coerenti con la somma delle due passate.

## Fuori scope (YAGNI)

- Modalità "smart" via hash (variante B) — rimandata.
- Copia a blocchi/delta per file grandi — robocopy non la supporta; non in scope.
- Forzatura a intervalli (ogni N run) — rimandata.
