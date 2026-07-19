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
