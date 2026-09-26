# Mirror o accumulo

È la scelta più importante di un job, e la più semplice da capire: decide **cosa succede in
destinazione ai file che cancelli dalla sorgente**.

## Cosa fanno entrambe

In tutti e due i casi RoboKeep **salta i file che non sono cambiati** (per questo la seconda
esecuzione dura pochi secondi) e **aggiorna quelli più recenti** nella sorgente. La differenza è
una sola.

## Mirror (predefinito)

Il mirror rende la destinazione una **copia esatta** della sorgente. Oltre ad aggiungere e
aggiornare, **rimuove dalla destinazione ciò che hai cancellato dalla sorgente**.

Usalo quando vuoi che la copia rispecchi fedelmente com'è la sorgente *adesso*: un secondo disco
sempre allineato, senza vecchi file di troppo.

## Accumulo (mirror disattivato)

Con il mirror tolto, RoboKeep **aggiunge e aggiorna soltanto**, e **non cancella mai** nulla in
destinazione. Se elimini un file dalla sorgente, in destinazione resta.

Usalo quando la destinazione deve *conservare tutto*, anche ciò che nella sorgente non c'è più —
per esempio un archivio che cresce nel tempo.

## Attenzione: il mirror cancella

Il mirror è potente proprio perché cancella, ma è anche lì che si fanno i danni. **La prima
volta, e ogni volta che hai un dubbio, premi *Anteprima***: RoboKeep ti elenca cosa *verrebbe*
copiato o cancellato, senza toccare nulla.

> È proprio il mirror a rendere pericolosa la rotazione dei dischi: un mirror puntato sul disco
> sbagliato lo renderebbe identico alla sorgente, cancellando quello che ci trova. Se alterni
> più dischi esterni, leggi *Rotazione dei dischi*.

## La soglia sulle cancellazioni

Il modo più comune di perdere dati con un mirror non è un guasto: è una sorgente che **si è
svuotata senza che tu lo sapessi** — una cartella spostata o rinominata, un'unità di rete che non
si è montata e appare vuota, un ransomware che ha cifrato tutto. Il mirror fa il suo dovere e rende
la destinazione identica alla sorgente: vuota.

Per questo ogni job in mirror ha una soglia nell'editor — **«Ferma il mirror se cancellerebbe il …
% dei file o più»**, predefinita al **20 %**. Prima di partire RoboKeep conta, senza toccare nulla,
quanti file spariranno dalla destinazione; se raggiungono la soglia:

- job avviato **dalla finestra**: ti chiede conferma con i numeri in chiaro («cancellerebbe 1812
  file su 2014, il 90 %»). *Sì* prosegue per questa volta, *No* ferma il job.
- job avviato da **un'attività pianificata o dalla riga di comando**: si ferma, perché non c'è
  nessuno a cui chiedere. Il job risulta fallito con «BLOCCATO: troppe cancellazioni», nel log ci
  sono i numeri, l'email di esito lo annuncia e la riga nella finestra principale lo segnala.

Per sbloccarlo: **avvia il job dalla finestra e conferma** (vale solo per quel run), oppure alza la
soglia nell'editor del job — **0** la disattiva del tutto. Sotto i **20 file** non scatta mai: una
cartella con pochi file cambia di natura in un pomeriggio. Con l'*Anteprima* non blocca niente, il
riepilogo dice soltanto che si andrebbe oltre soglia. L'accumulo non c'entra: non cancella mai.

I job con le versioni riusano il controllo che il versioning fa già per sapere se qualcosa è
cambiato, quindi non costano nulla in più; per i mirror senza versioni è una lettura in più della
destinazione, che su cartelle enormi può richiedere qualche decina di secondi (nel log lo dice la
riga «anteprima delle cancellazioni»). Con le versioni, però, **nessun file esistente viene
cancellato** — l'ultima versione resta intatta — e la domanda lo dice così com'è: quanti file *in
meno* avrebbe la versione nuova rispetto all'ultima.
