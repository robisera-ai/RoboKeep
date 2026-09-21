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
con **30 versioni**; puoi cambiare il numero, o mettere **0** per non avere limite (sconsigliato:
il disco si riempie di voci e rallenta). Il limite di età non tocca mai la versione più recente:
quella è il tuo backup attuale, anche se i file non cambiano da mesi.

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
