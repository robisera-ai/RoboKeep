# L'editor del job

L'editor è dove definisci un backup nel dettaglio. Ci arrivi da **Nuovo** — che apre la
creazione guidata o, se la salti, un editor vuoto — oppure da **Modifica** su un job esistente.

## I campi principali

- **Nome** — come chiami questo job. Compare nella lista e nella cronologia.
- **Sorgente** (+ *Sfoglia*) — la cartella da salvare, sottocartelle incluse.
- **Destinazione** (+ *Sfoglia*) — la cartella dove finiscono i file.
- **Disco** — la riga sotto la destinazione, per legare il job a un disco esterno preciso. Vedi
  *Rotazione dei dischi*.

## Le opzioni

- **Mirror** — la destinazione resta una copia esatta della sorgente. Vedi *Mirror o accumulo*.
- **Non sovrascrivere i file più recenti in destinazione** — se in destinazione c'è una versione
  più nuova, la lascia stare.
- **Copia anche ACL/owner** — porta con sé permessi e proprietario dei file; utile sulle share
  di rete.
- **Multi-thread** — copia più file in parallelo, più veloce sui tanti file piccoli. Conviene sugli
  **SSD**; su un **disco meccanico** un valore alto fa saltare di continuo la testina tra file diversi
  e spesso rallenta: lì tieniti su valori bassi (2-4).
- **Ottimizza file grandi** — modalità pensata per i file molto grossi.
- **Copia riavviabile** — se la copia di un file enorme si interrompe, riparte da dove era
  arrivata.
- **Registra tutti i file nel log** — annota ogni file copiato, non solo il riepilogo.
- **Tentativi** e **Attesa (secondi)** — quante volte riprovare su un file bloccato, e quanto
  aspettare tra un tentativo e l'altro.
- **Esclusioni file** e **cartelle** — cosa saltare, una voce per riga.
- **Forza copia** — ricopia sempre i file la cui data e dimensione non cambiano mai (container
  cifrati, certi database), che altrimenti verrebbero saltati. C'è una modalità opzionale a
  **confronto di contenuto**, che decide in base a ciò che c'è davvero dentro il file.
- **Rallentamento della copia** — limita la velocità: comodo per non saturare la rete durante un
  backup su una share.

## L'anteprima del comando

In fondo all'editor c'è sempre l'**anteprima del comando** esatto che verrà eseguito. Cambia
un'opzione e la vedi aggiornarsi: nessuna magia nascosta, sai sempre cosa gira.

## Funzioni con un capitolo tutto loro

Alcune opzioni dell'editor sono trattate a parte, per esteso:

- *Le versioni*
- *Pianificazione automatica*
- *Copiare i file aperti (VSS)*
- *Verifica dell'integrità*
- *Backup su rete e credenziali*
