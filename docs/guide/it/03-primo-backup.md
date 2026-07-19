# Il tuo primo backup

Creare un backup con RoboKeep richiede pochi minuti. Ti serve solo sapere **cosa** vuoi salvare
(la cartella sorgente) e **dove** (la cartella di destinazione, di solito su un disco esterno).

## Passo 1 — Avvia la creazione guidata

Nella barra in alto premi **Nuovo**. Si apre la procedura guidata, che ti fa qualche domanda in
linguaggio semplice: quale cartella copiare, su quale destinazione, e se i file restano aperti
mentre lavori. In base alle risposte sceglie per te le impostazioni migliori.

Se preferisci fare tutto a mano, puoi saltare la guida e compilare direttamente l'editor.

## Passo 2 — Controlla le impostazioni

Prima di salvare, l'editor mostra le scelte principali:

- **Mirror**: la destinazione diventa una copia esatta della sorgente. Attenzione: in mirror,
  ciò che cancelli dalla sorgente viene rimosso anche dalla destinazione. Se vuoi solo aggiungere
  e aggiornare senza mai cancellare, togli la spunta.
- **Destinazione**: la cartella dove finiranno i file. Sotto compare la riga *Disco*, utile se
  usi dischi a rotazione (vedi il capitolo dedicato).

In basso trovi sempre l'**anteprima del comando** esatto che verrà eseguito: nessuna magia
nascosta.

## Passo 3 — Prova e avvia

Un buon riflesso, la prima volta: premi **Anteprima**. RoboKeep elenca cosa *verrebbe* copiato o
cancellato, **senza toccare nulla**. Così eviti sorprese.

Quando sei sicuro, premi **Avvia selezionato** (o **Avvia tutti** per eseguire tutti i job
abilitati). Vedrai il log scorrere in tempo reale e, alla fine, un riepilogo con quanti file sono
stati copiati.

> La seconda esecuzione sarà quasi istantanea: RoboKeep salta i file che non sono cambiati e
> ricopia solo ciò che serve.

Fatto: hai il tuo primo backup. Da qui puoi aggiungere le versioni datate, la pianificazione
automatica o la verifica dell'integrità — un capitolo per ciascuno.
