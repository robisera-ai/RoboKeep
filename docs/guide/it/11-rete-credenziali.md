# Backup su rete e credenziali

RoboKeep non lavora solo su dischi locali: sorgente o destinazione possono stare su una **share di
rete**, indicata con un percorso UNC nella forma `\\server\condivisione`.

## Usare un percorso di rete

Nell'editor del job scrivi il percorso UNC come sorgente o destinazione, esattamente come faresti
con una cartella locale. Se la share è aperta a tutti, non serve altro. Se invece richiede un
accesso, aggiungi una credenziale.

## Aggiungere una credenziale

Una credenziale è la terna **host, utente e password** con cui RoboKeep si autentica sul server.

La via più corta è la **creazione guidata**: se sorgente o destinazione sono una share di rete,
l'ultimo passo chiede utente e password e crea la credenziale al posto tuo (se per quel server ne
esiste già una, la usa senza chiedere nulla). Altrimenti, a mano:

1. Apri **Impostazioni** e vai alla gestione delle credenziali di rete.
2. Aggiungi una credenziale indicando l'host (il nome del server), l'utente e la password.
3. Salva.

La password viene conservata **cifrata con la DPAPI di Windows**, mai in chiaro: sul disco non
resta nulla di leggibile.

## Associare la credenziale a un job

Nell'editor del job scegli quale credenziale usare per quella connessione. Prima di eseguire il
job, RoboKeep si connette alla share con quelle credenziali; poi lancia il backup normalmente.

> Suggerimento: se copi verso una share dove contano i permessi, attiva **«Copia anche
> ACL/owner»** perché sulla destinazione vengano riportati anche i permessi e il proprietario dei
> file, non solo il contenuto.

## Ambito di cifratura e attivita' pianificate

La DPAPI può cifrare le credenziali con ambito **utente** oppure **macchina**. La differenza conta
per le attivita' pianificate: un backup pianificato può girare quando non hai effettuato l'accesso,
e con ambito solo-utente non riuscirebbe a decifrare la password. Per questo, se pianifichi job di
rete, usa l'ambito **macchina**, così l'attivita' può leggere le credenziali da sola. Trovi
l'impostazione in *Impostazioni* — su un PC condiviso, valuta il compromesso descritto lì.

## Rete e rotazione dei dischi

Una destinazione di rete **non ha un volume removibile**, quindi è **esente** dalla protezione
della rotazione dei dischi: un job di rete non viene mai «saltato» in attesa del disco giusto.
Quella protezione riguarda solo i dischi esterni a rotazione (vedi *Rotazione dei dischi*).
