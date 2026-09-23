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

## I log e la loro pulizia

I log contengono i **percorsi** dei file copiati — utili per capire cosa è successo in un'esecuzione. Per non lasciarli accumulare, vengono **ripuliti automaticamente dopo 30 giorni**, un valore che puoi cambiare nelle *Impostazioni*.

## Open source

RoboKeep è **open source**, con licenza **MIT**. Il codice è pubblico: chiunque può leggerlo e verificare con i propri occhi che faccia esattamente quel che dice — niente di nascosto.
