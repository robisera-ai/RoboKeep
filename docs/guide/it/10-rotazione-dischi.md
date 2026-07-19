# Rotazione dei dischi

Se fai i backup su **più dischi esterni che colleghi a turno**, questo capitolo ti riguarda da
vicino: protegge dalla perdita di dati più insidiosa.

## Il problema

Windows assegna le lettere di unità nell'ordine in cui colleghi i dischi. Così due dischi esterni
diversi possono ricevere, in momenti diversi, la **stessa lettera** — per esempio `E:` — con le
**stesse cartelle di destinazione**.

Immagina un job in *mirror* configurato per scrivere su `E:\Backup`. Se colleghi il disco
sbagliato, quel job vedrebbe `E:\Backup` sul disco sbagliato e — essendo un mirror — lo
renderebbe identico alla sorgente, **cancellando quello che ci trova**. Su un disco che magari
custodisce le tue versioni storiche, è un disastro.

## La soluzione: riconoscere il disco, non la lettera

RoboKeep identifica ogni disco dal suo **volume** — un codice univoco assegnato alla
formattazione, che *non* cambia con la lettera. Puoi legare un job a un disco preciso: da quel
momento, se nell'alloggio non c'è quel disco, il job **viene saltato senza toccare nulla**.

## Come proteggere un job

1. Collega il disco **giusto** (quello su cui quel job deve scrivere).
2. Apri il job con **Modifica**. Sotto la destinazione trovi la riga **Disco**.
3. Se il job non è ancora protetto, premi **Proteggi con questo disco**: l'etichetta del disco
   compare e il job è ora legato a quel volume.
4. Salva.

Ripeti per ogni job che vive su un disco a rotazione. I job su dischi interni o su cartelle di
rete non ne hanno bisogno.

## Cosa vedi quando il disco cambia

- **Disco giusto collegato** → il job gira normalmente.
- **Disco sbagliato, o nessun disco** → il job risulta **Saltato**, con un messaggio chiaro tipo
  *«scrive sul disco BACKUP1, ma il disco collegato è ALTRO»*. Nessun errore, niente cancellato.
- Nella lista, un job che aspetta il suo disco mostra una **clessidra grigia** («in attesa»),
  non un allarme rosso: non è in ritardo, sta solo aspettando il suo disco.

> Le icone si aggiornano da sole quando colleghi o scolleghi un disco: non serve premere
> *Aggiorna*.

## Cambiare disco a un job

Se vuoi davvero riassociare un job a un disco diverso, aprilo con quel disco collegato: comparirà
l'avviso *«Ora è collegato il disco …»* e il pulsante **Usa questo disco**. È l'unico modo di
riassociare senza cambiare la destinazione — apposta, perché un salvataggio qualsiasi non possa
spostare per sbaglio un job sul disco sbagliato.
