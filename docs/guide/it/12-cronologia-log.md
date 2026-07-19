# Cronologia e log

Un backup che fallisce in silenzio è peggio di nessun backup. Per questo RoboKeep tiene memoria di
ogni esecuzione e ti lascia rileggere tutto, quando vuoi.

## La finestra Cronologia

Apri la **Cronologia** per vedere l'elenco di ogni backup e di ogni verifica dell'integrita'. Per
ciascuna voce trovi:

- la **data** e il **job** a cui si riferisce;
- l'**esito** (riuscito, saltato o fallito);
- i **conteggi** dei file coinvolti;
- la **durata**.

Puoi **filtrare per job** per concentrarti su un solo backup e seguirne l'andamento nel tempo.

## Leggere il log completo

Fai **doppio clic** su una voce della cronologia: RoboKeep apre il **log completo** di
quell'esecuzione e te lo mostra dentro l'app. Il log viene letto direttamente dallo zip in cui è
archiviato, quindi non devi cercare file né estrarre nulla a mano.

## Il log in tempo reale

Mentre un job è in esecuzione, il **log scorre in tempo reale** nella parte bassa della finestra
principale: vedi riga per riga cosa viene copiato, aggiornato o rimosso, e alla fine il riepilogo.

## Archivio e pulizia automatica

Il log di ogni esecuzione viene **archiviato e compresso in uno zip**, tenuto separato **per job**.
Così la cronologia resta ordinata e ogni backup conserva la propria traccia.

Per non far crescere l'archivio all'infinito, RoboKeep applica una **pulizia automatica**: i log
più vecchi del periodo di ritenzione vengono eliminati da soli. Il periodo è **configurabile in
Impostazioni** (per impostazione predefinita 30 giorni), così scegli tu quanto a lungo tenere la
storia dei tuoi backup.

> I log contengono i percorsi dei file copiati. Restano solo sul tuo PC e vengono cancellati alla
> scadenza che hai impostato: nessun dato lascia i tuoi dischi.
