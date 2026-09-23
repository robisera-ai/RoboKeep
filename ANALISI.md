# RoboKeep — Obiettivo, scelte di progetto ed evoluzione

Com'è nato, perché è fatto così, e le decisioni ingegneristiche che lo tengono in piedi.

## Obiettivo

Tutto è partito da alcuni script batch personali costruiti attorno a **robocopy**: funzionavano,
ma la configurazione era sparsa in decine di `.cmd` con percorsi duplicati, e ogni modifica
significava editare script a mano. Il primo obiettivo era una **app desktop con interfaccia
grafica**: stessa copia affidabile di sempre, configurazione centralizzata in un unico file,
tutto modificabile da GUI.

Da lì la visione si è allargata: **un backup vero per Windows, di cui fidarsi** — che conserva
uno **storico**, **avverte** quando qualcosa non va, copre anche i **file aperti** e permette di
**verificare matematicamente** che le copie siano integre. Senza abbandonare ciò che lo rende
solido: robocopy, locale, trasparente, portatile.

## Scelta di fondo: usare gli strumenti nativi di Windows

L'idea guida è **non reinventare nulla e non aggiungere dipendenze esterne**. Ogni funzione
importante poggia su un pezzo di Windows collaudato da decenni:

| Funzione | Strumento nativo |
|---|---|
| Motore di copia | `robocopy` di sistema (multi-thread, sempre aggiornato con Windows Update) |
| File aperti/bloccati | **Volume Shadow Copy** (snapshot del volume via WMI) |
| Versioni senza sprecare spazio | **hard-link NTFS** (modello Time Machine/rsnapshot) |
| Pianificazione | **Utilità di pianificazione** di Windows |
| Password mai in chiaro | **DPAPI** (cifratura legata a macchina o utente) |
| Compressione log, email, hash | .NET puro (`System.IO.Compression`, `System.Net.Mail`, SHA-256) |

L'app costruisce il comando giusto, lo esegue e ne legge l'esito — e mostra sempre in
**anteprima l'esatta riga di comando**: non è una scatola nera. Perché non un sincronizzatore
già pronto (FreeFileSync, rsync)? Perché il valore mancante non era il motore di copia, ma lo
strato di **fiducia e usabilità** sopra robocopy: GUI, configurazione, storico, avvisi, verifica.

## Architettura

App **WPF .NET 10 (MVVM)**, configurazione in **un unico `config.json`**. Due modalità:
- **Interattiva (GUI):** gestione job, creazione guidata, anteprima/dry-run, log live, cronologia.
- **Silenziosa (CLI):** `RoboKeep.exe --run-all` / `--job "Nome"` → usata dalle attività pianificate.

```
src/
  RoboKeep.Core/    libreria pura e testabile: Models + Services (engine, versioning, VSS,
                    verifica, cronologia, scheduler, config, log, email, credenziali)
  RoboKeep/         app WPF (Views + ViewModels + entry/CLI) → RoboKeep.exe
  RoboKeep.Tests/   xUnit — 225 test sul cuore della logica
```

La regola architetturale: **la logica che decide è pura e testata; l'I/O è isolato nei servizi.**
`RobocopyArgsBuilder` (opzioni → argomenti), `ExitCodeInterpreter`, `SnapshotPlanner` (ritenzione),
`VssPathMapper`, `SchtasksArgs` (XML delle attività), `JobWizardPlanner` sono funzioni pure:
ognuna ha i suoi test, nessuna tocca il disco. Ogni funzione nata da un errore visto sul campo
ha un test che lo blocca per sempre.

## L'evoluzione, tappa per tappa

### v1.0 — La GUI su robocopy
Editor completo con **anteprima del comando**, **creazione guidata** che deriva le opzioni
ottimali da domande in linguaggio semplice (tipo di dischi → `/MT`, file grandi → `/Z`,
permessi → `/COPYALL`), anteprima/dry-run, log zippati per data, email, credenziali DPAPI,
pianificazione, **forza copia** per i file a data/dimensione congelate (container cifrati) con
modalità *smart* a confronto di hash. In **5 lingue**.

### v1.1 — Stop ai fallimenti silenziosi
La lezione: un backup che smette di girare senza dirlo è il peggior nemico. Quindi: **dati
dell'app fuori da `bin/`** (`%APPDATA%\RoboKeep`, sopravvive ad aggiornamenti e pulizie),
**allerta** per job falliti o fermi da troppo tempo, **pre-check** prima di ogni avvio
(destinazione raggiungibile, spazio sufficiente), notifiche toast e area di notifica.

### v1.2 — Versioning: lo storico che non costa spazio
Ogni esecuzione può salvare uno **snapshot datato**: appare come un albero completo, ma i file
invariati sono **hard-link condivisi** tra le versioni — dieci versioni non costano dieci volte
lo spazio. Le decisioni delicate: la pre-passata che **rompe gli hard-link dei file cambiati**
prima di robocopy (mai modificare sul posto un file condiviso con le versioni precedenti); la
cancellazione **POSIX-safe** che rimuove una versione senza toccare il bit read-only degli inode
ancora condivisi; il pattern **`.inprogress` + rinomina finale**, così un run interrotto non
lascia mai una versione a metà spacciata per buona; il prefisso `\\?\` per i percorsi oltre i
260 caratteri.

### v1.3 — File aperti: lo snapshot VSS
Outlook aperto, database in uso: robocopy li salta. Con un'opzione per job, RoboKeep fotografa
il volume per un istante (**Volume Shadow Copy**) e copia dall'immagine congelata. Le decisioni:
il processo elevato è **lo stesso RoboKeep.exe** rilanciato con un flag (un solo prompt UAC per
esecuzione, nessun secondo eseguibile); i due processi comunicano con un **protocollo a file**
(request/ready/release); un **registro con PID** degli snapshot creati permette di ripulire i
residui dei run morti senza mai toccare gli snapshot vivi di processi concorrenti; e la regola
d'oro: **qualunque problema VSS degrada a copia normale con avviso** — un backup parziale batte
sempre un backup bloccato.

### v1.4 — Operatività e integrità
- **Pianificazione per-job**: un'attività di Windows per ogni job. Registrata via **XML** e non
  coi parametri `/SC /D` di schtasks: le abbreviazioni dei giorni sono localizzate (`MON` in
  inglese, `LUN` in italiano) e l'XML è l'unico formato identico su ogni lingua di Windows.
- **Cronologia + visualizzatore log**: ogni backup e ogni verifica a registro (JSON, tetto 500
  voci, lock cross-processo), log rileggibili in-app direttamente dagli zip.
- **Verifica integrità**: confronto **SHA-256** file per file. Il dettaglio che fa la differenza:
  se un file risulta diverso ma la sorgente è stata modificata *dopo* il backup, viene contato
  come "modificato dopo", **non come corruzione** — una funzione che esiste per dare fiducia non
  può gridare falsi allarmi. Le esclusioni del job vengono rispettate; i job versionati
  verificano l'ultimo snapshot.
- **Rallentamento** (`/IPG`) per i backup su rete — e quando è attivo il multi-thread si spegne,
  perché robocopy applica il ritardo per thread e il limite diventerebbe imprevedibile.
- **Esporta/importa configurazione**, con validazione, copia di sicurezza automatica e
  riallineamento delle attività pianificate.

### v1.5 — Rotazione dei dischi: l'identità, non la lettera

Due dischi esterni alternati prendono la stessa lettera. Un mirror sul disco sbagliato cancella
quello che trova. La difesa è identificare il disco per **volume GUID** (assegnato alla
formattazione) e saltare, senza toccare nulla, quando il disco nello slot non è quello del job.
"Saltato" è un terzo esito: né successo né errore, con la sua icona neutra e exit code 0 per
l'attività pianificata.

### v1.6 — Guida in-app

Diciassette capitoli in italiano e inglese, resi nativamente in WPF. Il versioning imparò a
saltare un file illeggibile nel vecchio snapshot: **scelta poi rovesciata** (v1.7).

### v1.7 — La salute del disco viene prima del backup

Nata da un incidente reale: un box USB che faceva sparire il disco a metà scrittura, 17 settori
scritti a metà, la `$Mft` rovinata — e RoboKeep che aggravava con 8 thread su un 5400 rpm, ore
di copia sopra centinaia di errori CRC e nessun avviso nel mese in cui il registro eventi di
Windows già lo diceva. Le decisioni:

- **Fermarsi al primo errore hardware** (Win32 23/27/1117) invece di aggirarlo: si uccide
  robocopy prima del retry; il clone hard-link non salta più il file. Continuare a scrivere su un
  disco che segnala errori fisici è la scelta sbagliata, sempre. Il disco resta a riposo per la
  sessione; dalla versione successiva il riposo è persistente (`faulted-disks.json`, per identità
  di volume, scadenza 7 giorni) e si toglie con un pulsante esplicito.
- **Tenere sveglio il PC** con una power request (non `SetThreadExecutionState`, legata al
  thread: il codice async lo cambia).
- **Tetto `/MT:2` sui dischi meccanici**, riconosciuti via `IOCTL_STORAGE_QUERY_PROPERTY`
  (nessuna elevazione). Un bridge USB muto senza TRIM è trattato da HDD: limitare un SSD costa
  minuti, non limitare un HDD lo maltratta.
- **Niente snapshot se nulla è cambiato**: un'anteprima `/L` decide. Fatta **senza `/MT`**:
  in multi-thread robocopy conta come copiate tutte le cartelle e dà exit 0 anche con una
  cartella nuova.
- **Salute dal registro eventi**, non dallo SMART: lo SMART di un disco USB non si legge senza
  privilegi di amministratore, e Windows senza elevazione dichiara "Healthy" un disco con settori
  pendenti. Il registro Sistema conserva blocchi danneggiati, errori di I/O e scritture perse, e
  si legge da utente normale. Avvisa, non blocca: nomina i dischi per lettera e numero.
- **Forza copia con `/IM`**: i robocopy recenti classificano "modificato" un file riscritto con
  stessa data e dimensione e lo saltano nonostante `/IS /IT`. Nei job versionati i file da
  forzare vengono prima scollegati dallo snapshot: la passata sovrascrive sul posto.
- **Verifica periodica** (7 giorni) invece che a ogni backup, con un log a sé per ogni verifica.
- Lezione di processo: un test asseriva su testo localizzato mentre un altro cambiava la lingua
  del processo in parallelo; la release è fallita in CI al primo tentativo. Ora nessun codice
  riconosce righe dal loro testo, e i test che toccano la lingua girano in una collection isolata.

## Qualità del processo

Ogni tappa segue lo stesso ciclo: **brainstorming → spec scritta → piano di implementazione →
sviluppo TDD → doppia revisione del codice → prova manuale sul campo → release**. I difetti
trovati dalle revisioni (una corsa sul rilascio degli snapshot VSS, i falsi allarmi della
verifica sulle esclusioni, i giorni localizzati di schtasks) sono stati corretti **prima** di
arrivare a un utente. La suite conta **225 test**; le release pubblicano due pacchetti
(self-contained e framework-dependent) con changelog completo.

## Confini di scopo

**Dentro:** tutto ciò che si appoggia a strumenti nativi Windows e resta locale/LAN.
**Fuori (per scelta):** cloud/`rclone`, versioni macOS/Linux, copia delta/a blocchi (robocopy
ricopia il file intero: limite accettato e documentato). Mantenere RoboKeep semplice, locale e
trasparente vale più di ogni casella in più su una matrice di confronto.

Gli script batch da cui tutto è partito non fanno parte del repository — ma è giusto ricordare
che senza di loro RoboKeep non esisterebbe.
