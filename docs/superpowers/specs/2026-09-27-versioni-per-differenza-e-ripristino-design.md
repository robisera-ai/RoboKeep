# Versioni per differenza, ritenzione per spazio, ripristino guidato — design

Data: 2026-09-27. Stato: approvato (decisioni dell'utente: 5a automatica; ripristino B;
5c = messaggio chiaro + pulizia opzionale spenta di default).

## Scopo

Oggi le versioni esistono solo su NTFS (alberi di hard-link) e la ritenzione ignora lo spazio
libero: su exFAT/rete niente versioni, e un disco pieno fa fallire il job invece di dirlo
chiaramente. Il ripristino è «apri la cartella datata in Esplora file». Tre lavori, in
quest'ordine:

1. **Ritenzione per spazio** (5c) — piccolo, subito.
2. **Versioni per differenza** (5a) — per le destinazioni senza hard-link, scelte in automatico.
3. **Ripristino guidato** (B) — file, cartella o intero job «com'era alla data X», in una
   cartella a scelta; vale per entrambi i modelli di versione.

Il settore va verso repository deduplicati e opachi (restic, Borg) o snapshot del filesystem:
RoboKeep resta deliberatamente sui **file normali leggibili con Esplora file**; il secondo
modello quando gli hard-link mancano è quello di File History / `rsync --backup-dir`.

---

## 1. Ritenzione per spazio (5c)

- Impostazione globale nuova `AppSettings.FreeSpaceCleanup` (bool, **default false**), casella
  in Impostazioni → Generale: «Quando il disco di backup è pieno, cancella le versioni più
  vecchie per far posto (mai l'ultima)». Soglia = `MinFreeSpaceMb` già esistente (casella
  «Spazio minimo libero»), il cui **default sale a 10 240 MB** per le configurazioni che non lo
  hanno impostato.
- Prima del run di un job **con versioni** (entrambi i modelli): se l'impostazione è attiva e lo
  spazio libero del volume di destinazione è sotto la soglia, `SnapshotService` cancella le
  versioni più vecchie **di quel job** una alla volta, ricontrollando lo spazio dopo ciascuna,
  finché torna sopra la soglia o resta solo la più recente. Ogni cancellazione è una riga nel
  log e nell'email (`Space_Freed`: «liberati {0} cancellando la versione {1}»). Se non basta,
  il run prosegue e fallisce come oggi.
- Messaggio chiaro in ogni caso: se robocopy fallisce per spazio (`ERROR 112` nell'output,
  riconoscimento indipendente dalla lingua), lo stato del job diventa «Disco pieno»
  (`Space_Status`), il tooltip di salute e l'email dicono quante versioni ci sono e quanto
  occupano (`Space_Detail`: «E:\ pieno: {0} versioni del job occupano {1}. Cancellane
  dall'editor o attiva la pulizia automatica nelle Impostazioni»). Il calcolo dell'occupazione
  delle versioni (per gli hard-link: dimensione dei file con un solo link + quota degli altri;
  approssimazione: somma delle dimensioni delle cartelle datate divisa per il numero di
  cartelle è fuorviante — usare `GetFileInformationByHandle.NumberOfLinks` per contare ogni
  file una volta sola) è best-effort e può dire «n/d».
- Funzione pura testata: `SpaceCleanupPlanner.NextToDelete(versions, freeBytes, thresholdBytes)`
  → il nome della prossima versione da cancellare o null (mai la più recente, mai
  `.inprogress`). `FreeSpaceReader` iniettabile nei test.
- Guida cap. 06 (versioni) e 14 (impostazioni); CHANGELOG.

---

## 2. Versioni per differenza (5a)

### Scelta del modello (automatica)

- `VersioningMode { HardLinks, Differential }`. Per un job con `Versioned == true`:
  - se la destinazione contiene già uno dei due layout, **vince il layout** (cartelle datate
    nella radice → hard-link; cartella `current\` → differenza): non si cambia modello sotto i
    piedi di un backup esistente;
  - altrimenti `HardLinkSupport.IsSupported(dest)` → hard-link, se no → differenza.
- Nessuna impostazione nuova. L'editor, sotto «Tieni le versioni», mostra una riga di stato
  aggiornata quando la destinazione cambia (al LostFocus, in `Task.Run`): «Versioni: cartelle
  complete via hard-link (NTFS)» oppure «Versioni: per differenza (il disco non supporta gli
  hard-link: exFAT, FAT32 o rete)» con una frase su cosa significa. Il wizard la mostra nel
  passo 2 se la destinazione del passo 1 è già nota. Il pre-avvio non avvisa più «niente
  versioni», dice solo quale modello userà (informativo).

### Layout su disco (modello per differenza)

```
E:\Backup\Documenti\
  current\                      il mirror vero e proprio (robocopy /MIR lavora qui)
  versions\
    2026-09-27_213000\          i file com'erano PRIMA del backup di quella data
      sub\lettera.docx          (percorso relativo identico a current)
      _manifest.json            { "createdAt", "changed": [...], "deleted": [...], "added": [...] }
    2026-09-28_213000\
```

- **Adozione** di un mirror piatto esistente (job che passa a versioni, o vecchio job): tutto
  il contenuto della destinazione viene spostato in `current\` (rinomina sullo stesso disco,
  istantanea), come oggi fa `AdoptPlainMirror` per gli hard-link. Le cartelle `versions` e
  `current` e `RoboKeep-config` non sono mai considerate contenuto da adottare.

### Il run

1. Anteprima `/L /FP /BYTES` senza `/MT` e senza forza-copia, sorgente (o snapshot VSS) →
   `current`. È la stessa anteprima che oggi serve alla soglia sulle cancellazioni: **una
   sola**, riusata per entrambe le cose.
2. `RobocopyListParser.Parse(lines, sourceRoot, destRoot)` (puro, testato su output reale
   catturato dalla macchina) → elenco di voci `(Kind, RelativePath)` con `Kind ∈ { NewFile,
   Overwrite (Newer/Older/Changed/Tweaked), ExtraFile, NewDir, ExtraDir }`. Le righe di
   riepilogo e di intestazione si ignorano. Con `/FP` ogni riga porta il percorso completo:
   per le voci EXTRA è quello di destinazione, per le altre quello di sorgente; il relativo si
   ottiene togliendo la radice corrispondente.
3. Se l'elenco è vuoto → «niente da fare», nessuna cartella versione (come oggi).
4. Guardia delle cancellazioni (spec 26/09) sugli stessi conteggi.
5. **Prima** del mirror: crea `versions\<data>.inprogress\`; per ogni `Overwrite` ed
   `ExtraFile` sposta `current\rel` → `versions\<data>.inprogress\rel` (`File.Move`, stesso
   volume); per ogni `ExtraDir` sposta l'intera cartella; scrive `_manifest.json` con
   `changed` (Overwrite), `deleted` (ExtraFile + ExtraDir), `added` (NewFile). Un file che non
   si riesce a spostare (lock) resta e verrà sovrascritto: riga nel log, non un errore.
6. Mirror vero su `current` (con `/MT` e tutto il resto). La passata forza-copia: tramite il
   gancio `beforeForceCopyPass` già esistente, i file della lista vengono spostati nella
   cartella versione e aggiunti a `changed` prima di essere riscritti.
7. Esito riuscito → rinomina `.inprogress` → nome definitivo; fallito → resta `.inprogress` e
   viene ripulita al run successivo (stessa regola di oggi). Nota: dopo lo spostamento e un
   mirror fallito a metà, `current` è incompleta ma **nessun dato è perso**: i file stanno nella
   `.inprogress`; il run successivo li rimette (robocopy ricopia dalla sorgente) e la pulizia
   della `.inprogress` avviene solo dopo un mirror riuscito.
8. Ritenzione: `SnapshotPlanner` sulle cartelle di `versions\` (stessi `SnapshotKeepCount` /
   `SnapshotMaxAgeDays`). Cancellare una versione per differenza perde gli stati più vecchi di
   quella successiva: è il significato di «tieni N versioni», la guida lo dice.

### Dove cambia il resto

- `VerifyTargetResolver`: modello per differenza → `current`.
- Sfoglia versioni (`SnapshotsWindow`): elenca `versions\`; il testo spiega che ogni cartella
  contiene «i file sostituiti o cancellati da quel backup»; pulsante **Ripristina…** (parte 3).
- Guardia cancellazioni: invariata, riusa l'anteprima.
- Copia configurazione, `/XD` radice: invariati.
- Guida cap. 06 riscritto con i due modelli; 16 («il disco non supporta gli hard-link» non è
  più un problema); CHANGELOG.

### Test

- `RobocopyListParser` su output reale di robocopy (catturare con `robocopy src dst /MIR /L /FP
  /BYTES /NJH /NJS` su cartelle temporanee: file nuovo, modificato, extra, cartella extra,
  nomi con spazi e accenti).
- `SnapshotService` differenziale con robocopy vero su cartelle temporanee: run 1 (adozione),
  run 2 dopo modifica/cancellazione → `versions\<data>\` contiene le copie precedenti e il
  manifest corretto; run 3 senza modifiche → nessuna versione; ritenzione; `.inprogress`
  residua ripulita.
- Scelta del modello: layout esistente vince; senza layout decide `HardLinkSupport`
  (iniettabile).
- `VerifyTargetResolver` per il nuovo layout.

---

## 3. Ripristino guidato (B)

- Finestra **Ripristina** (modale, da Sfoglia versioni e dalla barra principale accanto a
  Sfoglia versioni, attiva per i job con versioni): job → **data** (elenco delle versioni,
  più «adesso» = stato corrente) → **albero** dei file com'erano a quella data (TreeView a
  caricamento pigro: le cartelle si espandono a richiesta) con caselle → **cartella di
  destinazione** (Sfoglia; obbligatoria; mai la sorgente del job senza conferma esplicita, e
  mai in silenzio: il pulsante dice «Ripristina in …») → pulsante «Ripristina tutto com'era
  alla data» per l'intero job.
- Core puro `RestorePlanner`:
  - hard-link: `Resolve(snapshotDir)` → mappa `rel → percorso` = i file della cartella datata;
  - differenza: `Resolve(currentDir, versions ordinate per data, dataScelta)`: si parte
    dall'elenco di `current`; per ogni versione con data **≥** dataScelta, in ordine
    crescente, per ogni `changed`/`deleted` non ancora risolto → `versions\V\rel`; per ogni
    `added` non ancora risolto → tolto dalla mappa (il file non esisteva a quella data). La
    prima versione dopo la data vince.
  - Restituisce anche i totali (file, byte) per la conferma.
- Esecuzione: copia con `File.Copy` in un `Task.Run` con avanzamento (n/N) e annullamento;
  conserva le date dei file; ricrea le cartelle vuote note. Al termine riepilogo e pulsante
  «Apri cartella». Errori per file elencati alla fine, non bloccanti.
- Guida: nuovo capitolo «Ripristinare» (it/en) con i tre casi; CHANGELOG; README (5 lingue):
  una riga nella tabella «Perché RoboKeep» («Torna indietro nel tempo» diventa «…e rimetti a
  posto file, cartelle o tutto il job com'era a una data»).

### Test

- `RestorePlanner`: casi con manifest costruiti a mano (file aggiunto dopo la data → escluso;
  modificato due volte → vince la versione più vicina alla data; cancellato → dalla versione;
  intatto → da current); hard-link: mappa = cartella.
- Copia: cartella temporanea, date conservate, file bloccato → errore elencato, resto copiato.

## Fuori scopo

Modalità versioni a scelta manuale; ripristino sopra la sorgente in un clic; deduplica a
blocchi; controllo degli hard-link rotti.
