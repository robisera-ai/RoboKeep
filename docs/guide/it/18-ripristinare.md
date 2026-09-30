# Ripristinare

Un backup serve il giorno in cui qualcosa va storto. **Ripristina...** è la finestra che rimette a
posto quel giorno: scegli **com'era il backup a una certa data**, spunti **che cosa** recuperare e
dici **dove** metterlo. Funziona con entrambi i modelli di versione, e non tocca mai gli originali.

Si apre in due modi:

- dalla barra principale, con il job selezionato: **Ripristina...** (attivo per i job con versioni);
- da **Versioni...**, con il pulsante **Ripristina...**: la data che hai selezionato lì arriva già
  impostata.

## La regola che conta: «com'era alla data» ≠ «cosa è cambiato quel giorno»

Sono due cose diverse, e confonderle è il modo più facile per credere di aver perso dei file.

- **Versioni...** apre una cartella datata in Esplora file. Con il modello **per differenza**, quella
  cartella contiene **solo i file che quel backup ha sostituito o cancellato**. Se ci trovi tre file
  non è un backup incompleto: gli altri non erano cambiati e stanno in `current`.
- **Ripristina...** invece ricostruisce **l'albero intero**: i file cambiati li prende dalle
  cartelle-versione, tutti gli altri da `current`, e lascia fuori quelli che allora **non
  esistevano ancora**. Quasi sempre sono molti più file di quelli che vedi nella cartella datata —
  ed è esattamente quello che ti serve.

Con il modello a **hard-link** la distinzione non esiste: ogni cartella datata è già l'albero intero
di quel giorno, e il ripristino ti risparmia solo il lavoro di ricopiare a mano.

### Che cosa vuol dire una data: dipende dal modello, e l'elenco lo dice

I due modelli conservano cose diverse, quindi una data non vuol dire la stessa cosa. L'elenco lo
scrive per esteso, voce per voce:

- **per differenza → «Prima del backup del …»**. La cartella di una versione contiene le copie dei
  file che quel backup ha **sostituito o cancellato**: lo stato di *prima* di quel backup. Scegliere
  quel punto ti ridà proprio quello. Se un file è sparito con il backup del 28, lo ritrovi in
  **«Prima del backup del 28»**.
- **hard-link → «Dopo il backup del …»**. La cartella datata è l'albero **come quel backup lo ha
  lasciato**: scegliere quel punto ti ridà proprio quello.

Nel modello per differenza compaiono **tutti** i punti nel tempo, anche i backup che hanno solo
aggiunto file (senza cartella, e senza il conteggio dei file cambiati): ognuno è uno stato diverso.

Perché non «dopo» anche per differenza? Perché le copie della cartella-versione **più vecchia**
diventerebbero irraggiungibili: servirebbe il punto precedente, che spesso è un backup di sole
aggiunte il cui elenco è già stato tolto dalla ritenzione. Un file cancellato dalla sorgente, la cui
unica copia sta proprio lì, non si potrebbe più recuperare dalla finestra. Con «prima del backup»
ogni cartella-versione si raggiunge scegliendo il suo punto.

### Come RoboKeep torna indietro (modello per differenza)

Parte da `current`, cioè da **adesso**, e risale il tempo una versione alla volta, dalla più vecchia
alla più recente tra il **backup che hai scelto** e quelli **successivi**:

- un file che quella versione aveva **sostituito** o **cancellato** torna dalla sua cartella — lì
  c'è la copia *precedente*, cioè proprio quella che c'era allora;
- un file che quella versione aveva **aggiunto** esce dall'elenco: allora non c'era ancora;
- un file che nessuna versione ha toccato resta quello di `current`: non è mai cambiato, quindi
  quello di oggi **è** quello di allora.

Vince sempre la versione **più vicina** al punto scelto. Se un file è stato riscritto il 27 e il 28
e scegli **«Prima del backup del 27»**, arriva la copia messa da parte dal backup del **27** — non
quella del 28, che è lo stato di un giorno dopo.

Fin dove puoi tornare? Fino a **prima della versione più vecchia che hai tenuto**. La ritenzione
(«tieni le ultime N versioni») è anche il limite della tua macchina del tempo: se tieni 10 versioni e
fai un backup al giorno, torni indietro di dieci giorni, non oltre.

## Caso 1: recuperare un file o una cartella

Il caso normale: hai sovrascritto un documento, o hai svuotato una cartella per sbaglio.

1. Seleziona il job e premi **Ripristina...**.
2. In **Com'era il:** scegli il momento. La prima voce, **Adesso (stato attuale)**, è il backup
   aggiornato; sotto, dalla più recente, le voci dei singoli backup, con quanti file ciascuno aveva
   cambiato — spesso è così che si riconosce «il giorno in cui è successo il pasticcio».
   - Per differenza scegli proprio quella voce, **«Prima del backup del …»**: ti ridà i file come
     erano prima che quel backup li sostituisse o cancellasse.
   - Con gli hard-link le voci dicono **«Dopo il backup del …»**: scegli quella **sotto** il backup
     sospetto, cioè l'ultimo backup prima del pasticcio.
3. Apri le cartelle nell'albero e **spunta** quello che ti serve. Spuntare una cartella prende tutto
   ciò che sta sotto, anche i rami che non hai aperto; se poi togli la spunta a un file dentro,
   la cartella diventa «in parte spuntata» e l'eccezione viene rispettata.
   La casella di ricerca in alto filtra per nome quando sai già che cosa cerchi.
4. In **Ripristina in:** scegli la cartella dove far atterrare i file. Vedi il punto successivo:
   non può essere la sorgente del job.
5. **Ripristina**. Una conferma dice quanti file, quanto spazio e in quale cartella.

Alla fine la finestra dice quanti file sono arrivati e apre la cartella con **Apri cartella**. I
file conservano la **data di ultima modifica** che avevano: è quella che ti fa riconoscere la
lettera di marzo.

## Caso 2: rimettere in piedi tutto il job

Disco sostituito, PC nuovo, cartella cancellata per intero: qui non si spunta niente.

1. **Ripristina...**, scegli il momento: di solito **Adesso**; se la sorgente è stata svuotata o
   rovinata e un backup ha già registrato il danno, **«Prima del backup del …»** di quel backup
   (per differenza) o **«Dopo il backup del …»** di quello precedente (hard-link).
2. Scegli una cartella di destinazione vuota.
3. Spunta la casella in cima all'elenco, accanto a «File e cartelle»: seleziona tutto. Poi **Ripristina**.

RoboKeep ricrea l'albero completo, comprese le **cartelle che allora erano vuote** — un recupero che
le lasciasse indietro restituirebbe un albero diverso da quello che avevi.

Poi, quando hai controllato che ci sia tutto, sposti i file al loro posto con Esplora file. Il
passaggio in più è voluto: vedi sotto.

## Perché non si ripristina direttamente sopra la sorgente

La cartella di destinazione **non può essere la sorgente del job, né una sua sottocartella**. Il
pulsante resta spento e la finestra lo dice.

Non è una limitazione tecnica: è che un ripristino sopra la sorgente sovrascriverebbe i file di
**oggi** con quelli della data scelta, con un clic e senza ritorno. Ripristinare in una cartella a
parte ti lascia guardare che cosa è arrivato prima di decidere che cosa sostituire — e se la data
era quella sbagliata, non hai perso niente.

Per lo stesso motivo il ripristino **non sovrascrive mai** un file che nella cartella di
destinazione esiste già. Se ne trova uno, lo lascia stare e lo elenca alla fine tra i **non
ripristinati**: quel file potrebbe essere proprio quello che stavi cercando di salvare. Se la
cartella che hai scelto non è vuota, la finestra te lo dice prima di partire.

## Mentre copia, e quando qualcosa non va

La copia mostra una barra di avanzamento e si può **annullare**: i file già arrivati restano dove
sono, nessuno viene tolto.

Un file che non si riesce a copiare — è aperto in un altro programma, i permessi non bastano, il
disco ha un problema — **non ferma gli altri**: finisce nell'elenco dei non ripristinati, con il
motivo accanto, e tutto il resto arriva comunque. Chiudi il programma che lo teneva aperto e ripeti
il ripristino di quel solo file.

Se una versione è stata cancellata dalla ritenzione, o se un file non si era potuto mettere da parte
perché in uso al momento del backup, RoboKeep usa la copia **più vicina** che possiede. È il meglio
che esista sul disco: non c'è niente di nascosto da recuperare altrove.

> I file ripristinati sono **file normali**: nessun formato da estrarre, nessun programma da
> installare. Anche se RoboKeep sparisse domani, la tua destinazione resta una cartella che si apre
> con Esplora file — questa finestra ti risparmia il lavoro di ricostruire a mano, non è la chiave
> di una cassaforte.
