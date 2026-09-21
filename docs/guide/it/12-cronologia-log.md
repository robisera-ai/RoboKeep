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
quell'esecuzione nel **Blocco note**, dove puoi cercare (Ctrl+F), copiare e salvare, mentre la
cronologia resta utilizzabile. Il log viene estratto da solo dallo zip in cui è archiviato, quindi
non devi cercare file né estrarre nulla a mano.

**Ogni voce ha il suo log**: anche le **verifiche** — automatiche o lanciate a mano — salvano un
log proprio (il file ha il nome del job seguito da `-verifica`), con l'esito e l'elenco completo
degli eventuali file differenti. Il log del backup riporta invece, quando capita, che la verifica
non era prevista quel giorno e quando sarà la prossima.

## Dove sono i file di log

I log stanno nella cartella dati di RoboKeep (`%APPDATA%\RoboKeep\logs`), **non** accanto al
programma, divisi in una cartella per giorno. Per arrivarci premi **Cartella log** nel pannello
del log della finestra principale: si apre Esplora risorse con il giorno più recente selezionato.

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
