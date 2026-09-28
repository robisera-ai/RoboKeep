# Le versioni

Le versioni sono una **macchina del tempo** per i tuoi file. Con le versioni attive, prima di
sovrascrivere qualcosa RoboKeep mette da parte lo stato precedente in una **cartella datata**.
Hai cancellato un paragrafo martedì scorso? Apri la versione di martedì e lo recuperi.

## Come funziona

Le versioni si attivano **per singolo job**: le accendi dove servono e le lasci spente dove non
ti interessano. Da quel momento, a ogni esecuzione RoboKeep salva prima lo stato precedente, poi
aggiorna il backup.

Il *come* lo salva dipende dal disco di destinazione, e lo decide RoboKeep da sé. Ci sono due
modelli, e non c'è niente da configurare.

## I due modelli

### Cartelle complete via hard-link (NTFS locale)

Su un disco **NTFS locale** ogni versione è la **cartella intera** di quel giorno: la apri e ci
trovi tutto il backup com'era, non solo i file cambiati.

Il timore naturale è: dieci versioni occuperanno dieci volte lo spazio? No. I file rimasti
**invariati** tra una versione e l'altra non vengono duplicati: sono **condivisi** tramite
hard-link, cioè più cartelle datate puntano allo stesso identico file sul disco. Così dieci
versioni non costano dieci volte lo spazio — paghi solo ciò che è **davvero cambiato**.

```
E:\Backup\Documenti\
  2026-09-27_213000\      il backup intero com'era quel giorno
  2026-09-28_213000\      idem, e i file invariati sono lo stesso file sul disco
```

### Per differenza (exFAT, FAT32, rete)

Gli hard-link esistono solo su NTFS. Su un disco **exFAT** o **FAT32** (le chiavette e molti
dischi esterni escono così di fabbrica) e su una **share di rete** non ci sono — ma le versioni
sì. Con questo modello il backup corrente vive in una cartella **`current`**, e ogni versione
contiene **soltanto i file che quel backup ha sostituito o cancellato**:

```
E:\Backup\Documenti\
  current\                          il backup di adesso: sempre completo e aggiornato
  versions\
    2026-09-27_213000\              i file com'erano PRIMA del backup di quella data
      sotto\lettera.docx            (stesso percorso relativo che hanno in current)
    2026-09-27_213000.manifest.json l'elenco di che cosa quel backup ha cambiato
    2026-09-28_213000\
    2026-09-28_213000.manifest.json
```

Il file `.manifest.json` sta **accanto** alla cartella, non dentro: così nessun file tuo può
finirci sopra, nemmeno uno che si chiamasse proprio `_manifest.json`. Se lo cancelli non perdi i
file, solo l'elenco che serve al ripristino a sapere che cosa quel backup aveva cambiato.

I nomi **`current`** e **`versions`** nella radice di una destinazione con versioni sono
**riservati** a RoboKeep. Se la tua sorgente ha una cartella di primo livello con uno di quei due
nomi, scegli una sottocartella di destinazione diversa: RoboKeep se ne accorge, non sposta niente e
te lo scrive nel log, ma il backup resterebbe senza versioni.

È il modello della *Cronologia file* di Windows e di `rsync --backup-dir`. Costa una scrittura
sola per ogni file cambiato, invece delle migliaia di scritture di metadati che costa clonare un
albero di hard-link: su un disco meccanico è molto più leggero. In cambio una cartella datata
**non è** l'albero completo di quel giorno: è la differenza. Se ci guardi dentro e trovi tre file,
non è un backup incompleto — gli altri non erano cambiati, e stanno in `current`.

### Un backup che aggiunge soltanto non crea una cartella

Se un backup non ha **sostituito né cancellato** niente — il caso normale di un archivio di foto o
documenti, che cresce e basta — non c'è nessuno stato precedente da conservare, e RoboKeep **non
crea la cartella**: resta solo il suo `.manifest.json`, che pesa un nulla e serve al ripristino per
sapere quali file a quella data non esistevano ancora. Non è un dettaglio estetico: se ogni backup
creasse una cartella (vuota), con **«tieni le ultime 3 versioni»** basterebbero tre giorni di sole
aggiunte perché la ritenzione cancellasse l'unica cartella che conteneva davvero qualcosa — la
copia di un file sparito dalla sorgente. Contando solo le cartelle, «tieni N versioni» conta **N
versioni vere**.

### Come capire quale dei due hai

Due modi, scegli il più comodo:

- nell'**editor del job**, sotto la casella *«Mantieni versioni datate»*, c'è una riga che lo dice
  a chiare lettere per la destinazione che hai scelto (si aggiorna da sola quando la cambi);
- guarda la **destinazione** con Esplora file: se trovi cartelle con la data nel nome è il modello
  a hard-link; se trovi `current` e `versions` è quello per differenza.

Il modello si decide **una volta sola**, alla prima esecuzione, e poi **non cambia più**: se un
backup è già avviato, RoboKeep rispetta il layout che trova, anche se il disco intanto potrebbe
permettere l'altro. Cambiarlo sotto i piedi renderebbe irraggiungibili le versioni già fatte.

## Ritenzione: quante versioni tenere

Le versioni non si accumulano all'infinito. Nel job decidi la **ritenzione**, identica per
entrambi i modelli:

- tieni le ultime **N versioni**, e/o
- tieni le versioni fino a un'**età massima in giorni**.

Le più vecchie vengono rimosse in automatico, senza che tu debba pensarci. Un job nuovo parte
con **10 versioni**; puoi cambiare il numero, o mettere **0** per non avere limite (sconsigliato:
il disco si riempie di voci e rallenta). Il limite di età non tocca mai la versione più recente:
quella è il tuo backup attuale, anche se i file non cambiano da mesi.

Una nota sul modello per differenza: cancellare una versione perde gli stati **più vecchi** di
quella successiva. È esattamente il significato di «tieni le ultime N versioni» — indietro nel
tempo arrivi fino alla più vecchia che hai tenuto, non oltre.

## Quando il disco è pieno

Prima o poi il disco di backup si riempie. RoboKeep non ti lascia indovinare: se un backup si ferma
per mancanza di spazio, il job non mostra un codice di errore ma **«Disco pieno»**, e log, tooltip
d'allerta ed email dicono su quale disco, **quante versioni** ci sono e **quanto occupano davvero** —
ogni file fisico contato una volta sola, perché sommare le dimensioni delle cartelle datate, con gli
hard-link, darebbe un numero molto più grande del vero. Da lì decidi: abbassi **«Numero massimo di
versioni»** nell'editor del job — al run successivo le più vecchie vengono cancellate — oppure passi
a un disco più capiente. Se ti serve spazio subito, **Versioni...** → **Apri in Esplora risorse** e
cancelli a mano qualche cartella datata (la finestra Versioni non cancella niente da sé: mostra e
apre).

Se preferisci non pensarci, in **Impostazioni → Affidabilità** c'è la casella **«Quando il disco di
backup è pieno, cancella le versioni più vecchie per far posto (mai l'ultima)»**. Attiva, prima di
ogni backup con versioni RoboKeep guarda lo spazio libero: se è sotto la soglia **«Spazio libero
minimo in destinazione»** (10 GB di default, nella stessa scheda), cancella la versione più vecchia
di quel job, ricontrolla lo spazio, e continua finché torna sopra la soglia oppure **resta solo la
più recente** — quella è il tuo backup attuale e non si tocca mai. Ogni cancellazione è una riga nel
log, con il nome della versione rimossa e quanto spazio ha liberato.

La casella è **spenta di default**: cancellare per far posto è una decisione tua, non un'iniziativa
del programma. E se anche dopo la pulizia lo spazio non basta, il backup parte comunque e, se non
riesce, te lo racconta come sopra.

## Attivare le versioni su un job che esisteva già

Se un job faceva finora una copia semplice e attivi le versioni, il backup che hai già non va
perso né rifatto: al primo avvio RoboKeep **adotta la copia esistente**, spostandola in una
cartella datata (modello a hard-link) o nella cartella `current` (modello per differenza). È uno
spostamento sullo stesso disco: istantaneo, senza ricopiare nulla. Lo scrive nel log.

Se nella destinazione ci sono file che nella **sorgente non esistono**, RoboKeep non adotta niente
e lo dice: quella non è una copia fatta da questo job, e spostarla sarebbe spostare roba d'altri.
Quei file restano dove sono e il backup parte da zero.

## Se un backup si interrompe a metà

Con il modello per differenza i file messi da parte vengono **spostati** fuori da `current`: per
quelli cancellati dalla sorgente, quella è l'unica copia rimasta. Perciò, se il backup fallisce o
lo annulli a metà, la versione **non viene buttata via**: diventa comunque una versione vera —
incompleta, ma contiene esattamente gli stati precedenti dei file che aveva già spostato — e il log
lo dice. Il backup successivo riparte da lì e rimette a posto `current` ricopiando dalla sorgente.
Se un'esecuzione interrotta ha lasciato una cartella a metà, il run dopo la **recupera** invece di
cancellarla.

## Niente versioni doppie

Se dall'ultima esecuzione **non è cambiato nulla**, RoboKeep **non crea una nuova versione**: lo
scrive nel log e il backup risulta comunque riuscito e aggiornato. Non è solo ordine: con il
modello a hard-link creare una versione vuol dire scrivere una voce sul disco per **ogni** file,
anche quelli intatti, ed è il lavoro più pesante che RoboKeep chieda a un disco meccanico. Farlo
solo quando serve allunga la vita del disco.

## Sfogliare le versioni

Quando ti serve recuperare qualcosa:

1. Seleziona il job e premi **Versioni...**.
2. Scegli la **data** che ti interessa.
3. La versione si apre in **Esplora file**.
4. Ricopia da lì i file o le cartelle che vuoi recuperare.

Nessun formato speciale, nessuna estrazione: sono file normali, esattamente com'erano quel
giorno. Con il modello per differenza la finestra te lo ricorda: nella cartella datata trovi i
file **sostituiti o cancellati** da quel backup, e tutto il resto è in `current`.

> Le versioni si sposano bene con la *verifica dell'integrità*: i job con versioni verificano
> l'ultimo snapshot (modello a hard-link) o la cartella `current` (modello per differenza). Ne
> parla il capitolo dedicato.
