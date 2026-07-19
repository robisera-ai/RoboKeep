# Risoluzione problemi e FAQ

Le situazioni più comuni, con la risposta breve. Se non trovi la tua, quasi sempre il capitolo dedicato all'argomento ha il dettaglio.

### Un job risulta «Saltato»

È **normale** con la rotazione dei dischi, non è un errore. Il job scrive su un disco preciso e in questo momento nell'alloggio c'è un altro disco, o nessuno. Collega il disco giusto e riparte da solo. Vedi *Rotazione dei dischi*.

### Compare una richiesta UAC

È il **VSS**: la copia dei file aperti fotografa il disco per un istante e chiede **una conferma di amministratore** per esecuzione. Confermi e il backup prosegue. Vedi *Copiare i file aperti (VSS)*.

### Le versioni non funzionano

Le versioni datate usano gli hard-link, che esistono solo su **NTFS locale**. Servono una destinazione **NTFS** e **su disco locale**: su exFAT o su una share di rete non sono possibili. Vedi *Le versioni*.

### SmartScreen dice «editore sconosciuto»

L'app **non è ancora firmata digitalmente**, perciò Windows SmartScreen ti avvisa. È **sicura**: premi **«Ulteriori informazioni»** e poi **«Esegui comunque»**.

### Un'attività pianificata non parte

Controlla che l'**ambito di cifratura DPAPI** delle credenziali sia **a livello macchina**: solo così l'attività pianificata può decifrare le credenziali e girare ad app chiusa. Con l'ambito per-utente, l'attività non riesce a leggerle. Vedi *Impostazioni*.

### «Destinazione non raggiungibile»

Il disco o la share di destinazione **non sono collegati**. Collega il disco esterno o verifica la connessione di rete, poi riprova.

### La verifica dice «modificati dopo il backup»

Non è **corruzione**. Significa che hai **modificato i file dopo averli copiati**: sono più recenti nella sorgente rispetto alla copia. RoboKeep te lo segnala così apposta, per non spaventarti con un falso allarme. Al prossimo backup tornano allineati.
