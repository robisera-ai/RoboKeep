# Spec — Creazione guidata di un job ("wizard nuovo job")

Data: 2026-06-26
Stato: approvato (design)

## Problema

L'editor job espone molte opzioni robocopy (mirror, `/XO`, `/COPYALL`, `/J`, `/Z`, `/MT`,
esclusioni, Forza copia, retry…). Per chi non conosce robocopy sono criptiche e si sbagliano
facilmente — gli stessi errori incontrati in questi giorni:

- **mirror** che cancella in destinazione senza che l'utente se l'aspetti;
- **`/J`** che maschera le copie interrotte (file pre-allocato a dimensione piena);
- **`/MT`** lasciato alto su un HDD meccanico → testine in thrashing, più lento;
- **`/COPYALL`** che provoca "accesso negato" nelle ricopie;
- modalità **smart** che rilegge GB inutilmente.

## Soluzione

Una **creazione guidata**: poche domande in linguaggio chiaro, ognuna con **spiegazione e
consiglio**, che derivano le opzioni robocopy ottimali. Alla fine **non salva un job opaco**:
**precompila l'editor esistente** con i valori dedotti + l'anteprima del comando, così l'utente
rivede e modifica tutto prima di salvare. Niente UI di editing duplicata.

La logica di derivazione (risposte → `BackupJob`) è una **funzione pura e testabile**, dove si
concentra il valore (codifica le decisioni e gli avvisi imparati sul campo).

## Domande e mappatura

| # | Domanda | Imposta | Consiglio/avviso mostrato |
|---|---|---|---|
| 1 | Nome, **sorgente** (percorso + tipo: SSD / HDD / USB / rete) | `Source`; tipo → base `/MT`; rete→credenziale | — |
| 2 | **Destinazione** (percorso + tipo: SSD / HDD / USB / rete) | `Destination`; `/MT` sul più lento; rete→credenziale, `/R`/`/W` alti | "Con un HDD pochi thread vanno più veloci." |
| 3 | La destinazione **rispecchia** o **accumula**? | `Mirror` (`/MIR`) vs `/E` | "Mirror cancella in destinazione ciò che sparisce dalla sorgente." |
| 4 | Ci sono **file molto grandi** (VM, container)? | `Restartable` (`/Z`), `UnbufferedIO=false` | "Con `/Z` una copia interrotta riprende; evita `/J` se rischi interruzioni." |
| 5 | File a **data/dimensione congelate** (container cifrati, DB)? + pattern | `ForceCopyFiles`; `ForceCopySmart` resta off | "Se è VeraCrypt, meglio il flag timestamp alla fonte che ricopiare tutto." |
| 6 | Servono **permessi/ACL** e proprietari? | `CopyAll` (`/COPYALL`) vs `/COPY:DAT` | "Solo per ripristino su altro PC/server: può dare accesso negato in ricopia." |
| 7 | Escludere **cache/temp** comuni? | `ExcludeDirs`/`ExcludeFiles` precompilati | — |

### Regole di derivazione (precise)

**`/MT` (multithread)** — rango per ciascun estremo: `HDD=2`, `USB=4`, `Rete=8`, `SSD=16`;
`MultiThread = min(rango(sorgente), rango(destinazione))`. Così un HDD coinvolto abbassa sempre a 2.
- SSD+SSD → 16; SSD+HDD → 2; SSD+USB → 4; SSD+Rete → 8; Rete+Rete → 8; USB+Rete → 4.

**Rete** — se sorgente **o** destinazione è "rete": `Retries=3`, `Wait=10`; nel riepilogo un avviso
"ricordati di impostare la credenziale di rete nell'editor" (la credenziale non si crea nel wizard).

**File grandi** (Q4 = sì) → `Restartable=true`, `UnbufferedIO=false`. Altrimenti entrambi `false`.

**Mirror** (Q3) → `Mirror = (scelta == rispecchia)`.

**Permessi** (Q6 = sì) → `CopyAll=true`; altrimenti `false` (cioè `/COPY:DAT`).

**Metadati congelati** (Q5 = sì) → `ForceCopyFiles = pattern indicati`; `ForceCopySmart=false` (default
prudente, l'editor mostra poi la spiegazione per attivarlo). Se Q5 = no → lista vuota.

**Esclusioni** (Q7 = sì) → `ExcludeDirs += [cache, tmp, Temp, node_modules]`,
`ExcludeFiles += [*.tmp, ~$*]`. Se no → liste vuote.

**Default invariati** non chiesti: `ExcludeOlder=false`, `CredentialId=null`, `Enabled=true`,
`Retries=1`/`Wait=5` quando non c'è rete.

## Design

### Core (puro e testabile)
- `Models/StorageKind.cs` — enum `Ssd, Hdd, Usb, Network`.
- `Models/JobWizardAnswers.cs` — record con: `Name`, `Source`, `Destination` (string);
  `SourceStorage`, `DestStorage` (StorageKind); `Mirror` (bool); `HasLargeFiles` (bool);
  `FrozenMetadataPatterns` (List<string>, vuota = no); `PreservePermissions` (bool);
  `ExcludeCommonTemp` (bool).
- `Services/JobWizardPlanner.cs` — `public static BackupJob BuildJob(JobWizardAnswers a)`:
  applica le regole sopra e restituisce un `BackupJob`. Espone anche
  `public static int RecommendedThreads(StorageKind source, StorageKind dest)` (riusato dai test e
  dall'anteprima).

### UI
- `JobWizardWindow.xaml(.cs)` + `ViewModels/JobWizardViewModel.cs`.
  **Struttura: pagina unica guidata** in un `ScrollViewer` (come l'editor), con sezioni intestate
  ("Dove", "Comportamento", "File speciali", "Avanzate"); ogni domanda ha titolo, controllo, `InfoHint`
  con la spiegazione, e l'opzione consigliata evidenziata. In fondo un riquadro **"Cosa verrà
  impostato"** che mostra in tempo reale il comando robocopy risultante
  (`RobocopyArgsBuilder.ToDisplayString(JobWizardPlanner.BuildJob(answers))`). Pulsanti:
  **Annulla** e **Apri nell'editor**. (Una variante a passi multipli è possibile in futuro; per ora la
  pagina unica è più semplice da costruire e localizzare.)
- I tipi sorgente/destinazione sono due `ComboBox` (SSD / HDD / USB / Rete) nella stessa pagina.

### Integrazione con "Nuovo" — `MainWindow`
Oggi `OnNewJob` crea un `BackupJob` vuoto e apre l'editor. Diventa:
1. apre `JobWizardWindow`;
2. se l'utente conferma → `var job = JobWizardPlanner.BuildJob(vm.Answers)` → apre il **normale**
   `JobEditorWindow(job, …)` precompilato → se l'utente salva, aggiunge il job e `PersistJobs()`;
3. il wizard ha anche **"Salta (job vuoto)"** → apre l'editor con un `BackupJob` vuoto (comportamento
   odierno), per gli utenti esperti;
4. se annulla il wizard → niente.

`Clone`/`CopyInto` non cambiano (il wizard produce un job nuovo, l'editor lo gestisce come sempre).

### Localizzazione — `Loc.cs`
Nuove chiavi in tutte e 5 le lingue: titolo wizard, le 7 domande, le etichette delle opzioni
(SSD/HDD/USB/Rete, rispecchia/accumula, sì/no), gli `InfoHint`/consigli, intestazioni di sezione,
pulsanti, l'avviso credenziale di rete. È la parte più voluminosa.

## Test

`JobWizardPlannerTests`:
- `RecommendedThreads`: SSD+SSD→16; SSD+HDD→2; SSD+USB→4; SSD+Rete→8; Rete+Rete→8; HDD+Rete→2.
- Mirror sì/no → `Mirror` true/false (`/MIR` vs `/E` verificabile via `RobocopyArgsBuilder.Build`).
- File grandi → `Restartable=true`, `UnbufferedIO=false`; no → entrambi false.
- Permessi → `CopyAll` true/false.
- Rete (sorgente o dest) → `Retries=3`, `Wait=10`; senza rete → 1/5.
- Metadati congelati con pattern → `ForceCopyFiles` valorizzato, `ForceCopySmart=false`; senza → vuota.
- Esclusioni comuni → liste precompilate attese; senza → vuote.
- `BuildJob` copia `Name`/`Source`/`Destination` dalle risposte.

(La UI del wizard e l'apertura dell'editor precompilato si verificano nello smoke manuale.)

## Verifica end-to-end (smoke)

1. "Nuovo" → si apre la creazione guidata.
2. Compilo nome/percorsi, scelgo es. sorgente SSD + destinazione HDD, "rispecchia", file grandi sì →
   l'anteprima mostra `/MIR /MT:2 /Z …`. "Apri nell'editor" → l'editor è precompilato con quei valori.
3. Salvo → il job compare in lista e funziona come gli altri.
4. "Salta (job vuoto)" → apre l'editor vuoto come prima.

## Fuori scope (YAGNI)

- Wizard a passi multipli con avanti/indietro (per ora pagina unica).
- Rilevamento automatico del tipo di disco (SSD/HDD) dal sistema: lo chiede l'utente.
- Creazione della credenziale di rete dentro il wizard: si fa nell'editor/Impostazioni.
- Suggerimento automatico di `ForceCopySmart`: resta scelta manuale nell'editor.
