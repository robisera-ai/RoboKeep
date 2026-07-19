# Privacy e sicurezza

RoboKeep è pensato per stare **a casa tua**: i tuoi file restano sui tuoi dischi e l'app non ha nulla da guadagnare dai tuoi dati.

## Non raccoglie niente

RoboKeep **non raccoglie niente**: zero telemetria, zero statistiche, nessun controllo aggiornamenti, nessun account, nessun traffico di rete. L'unica eccezione la decidi **tu**: se configuri i **report via email**, l'app contatta il server **SMTP** che hai scelto tu (con TLS attivo di default) per spedire il resoconto. Fine. Nient'altro esce dal tuo PC.

## Tutto in file locali

Tutto ciò che l'app sa — impostazioni, esiti, cronologia, log — vive in **file locali** sul tuo PC. È **JSON leggibile**: puoi aprirlo e ispezionarlo quando vuoi, senza strumenti speciali. Nessun database opaco, nessun formato segreto.

## Password cifrate

Le password (share di rete, mittente email) sono cifrate con **DPAPI di Windows**, il meccanismo di cifratura del sistema operativo. **Non vengono mai salvate in chiaro.** L'ambito della cifratura lo regoli nelle *Impostazioni*.

## I log e la loro pulizia

I log contengono i **percorsi** dei file copiati — utili per capire cosa è successo in un'esecuzione. Per non lasciarli accumulare, vengono **ripuliti automaticamente dopo 30 giorni**, un valore che puoi cambiare nelle *Impostazioni*.

## Open source

RoboKeep è **open source**, con licenza **MIT**. Il codice è pubblico: chiunque può leggerlo e verificare con i propri occhi che faccia esattamente quel che dice — niente di nascosto.
