# Le versioni

Le versioni sono una **macchina del tempo** per i tuoi file. Con le versioni attive, prima di
sovrascrivere qualcosa RoboKeep mette da parte lo stato precedente come **istantanea datata**.
Hai cancellato un paragrafo martedì scorso? Apri la versione di martedì e lo recuperi.

## Come funziona

Le versioni si attivano **per singolo job**: le accendi dove servono e le lasci spente dove non
ti interessano. Da quel momento, a ogni esecuzione RoboKeep salva prima la copia precedente come
snapshot datato, poi aggiorna il backup.

## Perché costano poco spazio

Il timore naturale è: dieci versioni occuperanno dieci volte lo spazio? No. I file rimasti
**invariati** tra una versione e l'altra non vengono duplicati: sono **condivisi** tramite
hard-link, cioè più versioni puntano allo stesso identico file sul disco. Così dieci versioni
non costano dieci volte lo spazio — paghi solo ciò che è **davvero cambiato**.

Proprio per questo le versioni richiedono una **destinazione NTFS locale**: gli hard-link non
esistono su exFAT né sulle share di rete. Se la destinazione non è idonea, RoboKeep te lo dice
prima di partire.

## Ritenzione: quante versioni tenere

Le istantanee non si accumulano all'infinito. Nel job decidi la **ritenzione**:

- tieni le ultime **N versioni**, e/o
- tieni le versioni fino a un'**età massima in giorni**.

Le più vecchie vengono rimosse in automatico, senza che tu debba pensarci. Un job nuovo parte
con **10 versioni**; puoi cambiare il numero, o mettere **0** per non avere limite (sconsigliato:
il disco si riempie di voci e rallenta). Il limite di età non tocca mai la versione più recente:
quella è il tuo backup attuale, anche se i file non cambiano da mesi.

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
perso né rifatto: al primo avvio RoboKeep **adotta la copia esistente come prima versione**,
spostandola in una cartella datata (uno spostamento sullo stesso disco, istantaneo, senza
ricopiare nulla). Da quel momento ogni versione costa solo ciò che è cambiato. Lo scrive nel log.

## Niente versioni doppie

Se dall'ultima esecuzione **non è cambiato nulla**, RoboKeep **non crea una nuova versione**
identica alla precedente: lo scrive nel log e il backup risulta comunque riuscito e aggiornato.
Non è solo ordine: creare una versione vuol dire scrivere una voce sul disco per **ogni** file,
anche quelli intatti, ed è il lavoro più pesante che RoboKeep chieda a un disco meccanico.
Farlo solo quando serve allunga la vita del disco.

## Sfogliare le versioni

Quando ti serve recuperare qualcosa:

1. Seleziona il job e premi **Versioni...**.
2. Scegli la **data** che ti interessa.
3. La versione si apre in **Esplora file**.
4. Ricopia da lì i file o le cartelle che vuoi recuperare.

Nessun formato speciale, nessuna estrazione: sono file normali, esattamente com'erano quel
giorno.

> Le versioni si sposano bene con la *verifica dell'integrità*: i job con versioni verificano
> l'ultimo snapshot. Ne parla il capitolo dedicato.
