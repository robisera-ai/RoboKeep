# Spec — Feature "Forza copia" (file a data/dimensione congelate)

Data: 2026-06-26
Stato: approvato (design) — variante C (semplice + smart, tutto opzionale)

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

## Soluzione scelta (variante C, tutto opzionale)

Una lista **"Forza copia"** per-job (pattern per nome/estensione), realizzata come **seconda passata**
di robocopy ristretta a quei soli file, con `/IS /IT` (copia anche se identico). Tutto il resto del
job resta incrementale e invariato.

La feature è **completamente opzionale**:

- **Disattivata** quando la lista è vuota (default) → comportamento identico a oggi, zero overhead.
- Quando valorizzata, un interruttore **per-job** sceglie la modalità:
  - **Semplice "copia sempre"** (default della lista): ogni file che corrisponde ai pattern viene
    **ricopiato per intero a ogni backup**. Adatta a file piccoli/medi a metadati congelati.
  - **Smart "copia solo se cambiato"**: l'app calcola un **hash** dei file che corrispondono ai
    pattern e ricopia **solo quelli realmente cambiati** dall'ultimo backup. Sicura anche per file
    grandi che cambiano di rado; costo: lettura completa dei file per l'hash a ogni run.

Note di scoping:

- I file **molto grandi** che cambiano spesso (es. container VeraCrypt da 100 GB usati ogni giorno)
  conviene comunque risolverli **alla fonte** (in VeraCrypt: Settings → Preferences → togliere
  "Preserve modification timestamp of file containers"): lì robocopy li copia da solo solo quando
  cambiano. La modalità smart serve per i casi senza un'impostazione equivalente.
- La seconda passata copre **di riflesso** anche il caso del file interrotto (viene comunque
  ricopiato). Per il caso generale di interruzione resta valida l'opzione esistente `/Z` (Restartable).

## Design

### Modello — `BackupJob`
Aggiungere:

```csharp
/// <summary>Pattern di file da forzare in copia anche se data/dimensione non cambiano
/// (seconda passata robocopy con /IS /IT). Es. *.pst, database.dat. Vuoto = feature disattivata.</summary>
public List<string> ForceCopyFiles { get; set; } = new();

/// <summary>Modalità della lista ForceCopyFiles:
/// false = "copia sempre" (ricopia integrale a ogni run);
/// true  = "smart" (ricopia solo i file il cui hash è cambiato dall'ultimo backup).</summary>
public bool ForceCopySmart { get; set; }
```

### Motore — `RobocopyArgsBuilder`
Nuovo metodo per la passata forzata, che riceve **i filtri già risolti** (pattern in modalità semplice,
oppure nomi dei file cambiati in modalità smart), così resta puro e testabile:

```csharp
public static IReadOnlyList<string> BuildForceCopyPass(
    BackupJob job, IReadOnlyList<string> filters, bool dryRun = false, string? logFile = null)
```

Comando generato (ordine: `<src> <dst> <filtri...> [opzioni]`):

- `filters` come argomenti posizionali dopo sorgente/destinazione;
- `/E` (ricorsivo) — **mai** `/MIR` (la passata forzata non cancella nulla: una passata filtrata che
  fa purge sarebbe pericolosa);
- `/IS` (include same) e `/IT` (include tweaked) → forza la copia anche per file identici;
- **mai** `/XO` (non deve saltare i "più vecchi": deve copiare comunque);
- `/XJ`, `/COPY:DAT` o `/COPYALL` (come `job.CopyAll`), `/MT:n`, `/Z` o `/J` (come il job),
  `/R:n`, `/W:n` — coerenti con la passata principale;
- `/L` se `dryRun`;
- `/TEE` + `/LOG:<file>` se `logFile` valorizzato.

La passata principale (`Build`) resta **invariata**.

### Pianificazione modalità smart — `ForceCopyPlanner` (nuovo servizio Core)
Responsabilità: decidere **quali filtri** passare alla seconda passata.

```csharp
public sealed record ForceCopyPlan(
    IReadOnlyList<string> Filters,                 // pattern (semplice) o nomi file cambiati (smart)
    IReadOnlyDictionary<string, string> NewHashes  // hash da salvare a copia riuscita (solo smart)
);

public ForceCopyPlan Plan(BackupJob job, IProgress<string>? progress, CancellationToken ct);
```

- Lista vuota → `ForceCopyPlan([], {})` (nessuna passata).
- **Semplice** (`ForceCopySmart == false`) → `Filters = ForceCopyFiles` (i pattern), `NewHashes` vuoto.
- **Smart** (`ForceCopySmart == true`):
  1. enumera ricorsivamente i file sotto `Source` che corrispondono ai pattern;
  2. per ciascuno calcola un hash **streaming** (`SHA256` su stream, niente caricamento in memoria),
     onorando `ct` e segnalando avanzamento via `progress`;
  3. confronta con l'hash salvato dall'ultimo backup (`IForceCopyHashStore`): file **assente dallo
     store** o con hash diverso = "cambiato";
  4. `Filters` = nomi dei file cambiati; `NewHashes` = hash aggiornati dei file cambiati.
  - Se nessun file è cambiato → `Filters` vuoto → nessuna seconda passata.

### Persistenza hash — `ForceCopyHashStore` (nuovo servizio Core)
Memorizza gli hash per job, in JSON nella **stessa cartella di `lastresults.json`** (accanto agli altri
stati persistiti), retro-compatibile (file assente = nessun hash noto).

```csharp
IReadOnlyDictionary<string,string> Load(string jobName);          // path-file -> hash
void Save(string jobName, IReadOnlyDictionary<string,string> hashes);
```

Chiave per file: percorso assoluto del file sorgente. Aggiornato **solo dopo copia riuscita**
(così un errore lascia il file "da ricopiare" al run successivo).

### Esecuzione — `RobocopyRunner`
Inietta opzionalmente un `ForceCopyPlanner` (default costruito in produzione, sostituibile nei test).
In `RunAsync`, dopo la passata 1:

- se `job.ForceCopyFiles` è **vuoto** → comportamento identico a oggi;
- altrimenti calcola `plan = planner.Plan(job, progress, ct)`:
  - se `plan.Filters` è vuoto (smart e nulla è cambiato) → nessuna passata 2;
  - altrimenti esegue la **passata 2** con `BuildForceCopyPass(job, plan.Filters, dryRun)`, riusando lo
    stesso meccanismo di avvio processo + cattura output (stdout appeso allo stesso `output`), stessa
    `CancellationToken`;
- a esito complessivo positivo e **non** dryRun, se smart: `hashStore.Save(job.Name, plan.NewHashes)`
  (merge con gli hash già presenti).

Aggregazione del `JobResult`:

- conteggi = **somma** delle due passate;
- `ExitCode` = **OR bit a bit** dei due exit code (esiti robocopy 0–16 sono bitfield: 1=copiati,
  2=extra, 4=mismatch, 8=falliti, 16=fatale; l'OR preserva l'esito peggiore);
- `Status`/`Success` = `ExitCodeInterpreter.Interpret(exitCombinato)`;
- `Duration` = durata totale (hashing + entrambe le passate).

Refactor minimo: estrarre "avvia un processo robocopy con questi argomenti → (exitCode, output)" in un
metodo privato riusato dalle due passate, per non duplicare setup `ProcessStartInfo`/encoding/cancel.

### UI — `JobEditorWindow` + `JobEditorViewModel`
- `ForceCopyFilesText` (split/join per righe, `RaisePreview()` come `ExcludeFilesText`).
- `ForceCopySmart` (bool, `RaisePreview()`).
- Nuovo riquadro **"Forza copia (ignora data/dimensione)"** sotto i due "Escludi", a tutta larghezza,
  con `InfoHint` (caveat) e, sotto, una `CheckBox` **"Copia solo se il contenuto è cambiato (calcola
  hash; più lento in lettura)"** legata a `ForceCopySmart`. La checkbox è abilitata solo se la lista
  non è vuota. `PlaceholderText` esempio: `*.pst`.
- `CommandPreview`: quando `ForceCopyFiles` è valorizzato, mostra **anche** la riga della passata 2.
  In modalità smart i filtri reali dipendono dall'hash a runtime, quindi la preview usa un segnaposto
  (es. `<file modificati>`) al posto dei nomi.

### Localizzazione — `Loc.cs`
Nuove chiavi in tutte e 5 le lingue (IT/EN/ES/FR/DE):

- `Editor_ForceCopy` (label riquadro)
- `Editor_ForceCopyTip` (caveat InfoHint)
- `Editor_ForceCopySmart` (label checkbox)
- `Editor_ForceCopySmartTip` (hint checkbox)

### Persistenza config — `ConfigStore`
Nessuna modifica strutturale: `ForceCopyFiles` e `ForceCopySmart` sono nuove proprietà serializzate con
il resto del job (default lista vuota + false → retro-compatibile con i config esistenti).

## Test

`RobocopyArgsBuilderTests`:

- `BuildForceCopyPass` include `/IS`, `/IT` e i filtri indicati;
- **non** contiene `/MIR` né `/XO`;
- rispetta `/MT`, `/COPYALL`, `/Z`/`/J` come il job;
- `dryRun` aggiunge `/L`;
- sorgente/destinazione come posizionali iniziali.

`ForceCopyPlannerTests` (con file temporanei reali e uno store in-memory):

- modalità **semplice** → `Filters` = i pattern, `NewHashes` vuoto;
- modalità **smart**, file mai visto → risulta "cambiato" (entra nei `Filters`);
- modalità **smart**, file con hash invariato nello store → **non** entra nei `Filters`;
- modalità **smart**, contenuto modificato (anche a dimensione invariata) → entra nei `Filters` e
  `NewHashes` contiene il nuovo hash;
- lista vuota → piano vuoto.

(L'aggregazione del runner — due processi reali — non è coperta da unit test del processo; la logica di
OR/somma resta semplice e verificabile nel test end-to-end.)

## Verifica end-to-end

1. Sorgente di prova con un file a metadati "congelati" (contenuto diverso, stessa data/dimensione).
2. Aggiungere il pattern alla lista "Forza copia", modalità **semplice** → il file viene copiato a ogni
   backup nonostante data/dimensione invariate; un file identico non in lista resta saltato.
3. Passare a modalità **smart**: primo backup copia il file (hash nuovo); secondo backup senza modifiche
   → il file **non** viene copiato (nessuna passata 2); dopo aver cambiato il contenuto → torna a
   copiarlo.
4. Lista vuota → nessuna passata aggiuntiva, esito identico a prima.
5. Esito/conteggi nel report coerenti con la somma delle due passate.

## Fuori scope (YAGNI)

- Copia a blocchi/delta per file grandi — robocopy non la supporta; non in scope.
- Forzatura a intervalli fissi (ogni N run) — non richiesta.
- Hash parziale/campionato — si usa hash completo per affidabilità.
