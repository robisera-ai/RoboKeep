# Privacy e sicurezza

RoboKeep è pensato per stare **a casa tua**: i tuoi file restano sui tuoi dischi e l'app non ha nulla da guadagnare dai tuoi dati.

## Non raccoglie niente

RoboKeep **non raccoglie niente**: zero telemetria, zero statistiche, nessun account e nessun traffico di rete, con **due eccezioni, entrambe a tua scelta**.

Il **controllo aggiornamenti**: al primo avvio RoboKeep chiede se vuoi che controlli l'esistenza di una versione nuova; se dici sì, all'avvio (al massimo una volta al giorno) fa una richiesta HTTPS a `api.github.com` per leggere il numero dell'ultima versione pubblicata. La richiesta porta con sé il tuo indirizzo IP (come qualunque richiesta di rete) e uno User-Agent `RoboKeep/<versione>`; nient'altro — nessun dato sui tuoi job, dischi o file. Lo scaricamento parte solo se lo chiedi tu. Puoi spegnere tutto in Impostazioni.

I **report via email**: se li configuri, l'app contatta il server **SMTP** che hai scelto tu (con TLS attivo di default) per spedire il resoconto. Fine. Nient'altro esce dal tuo PC.

Anche la lettura della **salute dei dischi** (SMART) è del tutto locale: l'app parla direttamente con i dischi collegati al PC, niente esce verso l'esterno.

## Tutto in file locali

Tutto ciò che l'app sa — impostazioni, esiti, cronologia, log — vive in **file locali** sul tuo PC. È **JSON leggibile**: puoi aprirlo e ispezionarlo quando vuoi, senza strumenti speciali. Nessun database opaco, nessun formato segreto.

## Password cifrate

Le password (share di rete, mittente email) sono cifrate con **DPAPI di Windows**, il meccanismo di cifratura del sistema operativo. **Non vengono mai salvate in chiaro.** L'ambito della cifratura lo regoli nelle *Impostazioni*.

## La copia della configurazione sul disco di backup

Dopo ogni backup riuscito RoboKeep scrive, nella radice del disco di destinazione, la cartella **`RoboKeep-config`** con la tua configurazione (vedi *Impostazioni*). Sapere cosa contiene è giusto, perché quel disco spesso viaggia:

- i **percorsi** di sorgenti e destinazioni, i nomi dei job e le esclusioni;
- gli **host delle share di rete** e i **nomi utente** delle credenziali salvate: il nome del NAS o del server a cui RoboKeep si collega e l'utente con cui lo fa;
- gli **indirizzi email** del mittente e del destinatario dei report, con nome utente e server SMTP;
- le **password** (share di rete, email) cifrate con **DPAPI di Windows**: si decifrano solo su quel PC — e solo con il tuo utente Windows, se nelle *Impostazioni* hai scelto «cifra le password solo per il mio utente Windows». Chi si porta via il file su un altro PC non le legge; per questo, ripristinando su un PC nuovo, vanno reinserite.

Non contiene **nessun tuo file**, nessun log e nessuna cronologia: solo la configurazione.

Se non la vuoi — per esempio perché il disco è condiviso, o lo porti fuori casa — spegni **«Salva una copia della configurazione sui dischi di backup»** nelle *Impostazioni*. Le cartelle `RoboKeep-config` già scritte puoi cancellarle a mano: RoboKeep non le rimette se l'opzione è spenta.

## I log e la loro pulizia

I log contengono i **percorsi** dei file copiati — utili per capire cosa è successo in un'esecuzione. Per non lasciarli accumulare, vengono **ripuliti automaticamente dopo 30 giorni**, un valore che puoi cambiare nelle *Impostazioni*.

## Open source

RoboKeep è **open source**, con licenza **MIT**. Il codice è pubblico: chiunque può leggerlo e verificare con i propri occhi che faccia esattamente quel che dice — niente di nascosto.
