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

### Un job si interrompe con «ERRORE HARDWARE» (CRC, controllo di ridondanza ciclico)

L'errore arriva da Windows, non da RoboKeep, e significa che **un disco — o il suo collegamento — ha segnalato un problema fisico**: errore nei dati (CRC), settore non trovato o errore del dispositivo. Le cause sono due, e vanno distinte: un **disco che sta cedendo** (settori danneggiati) oppure **cavo, box USB o alimentazione** difettosi, che su un disco esterno danno esattamente gli stessi messaggi.

In questi casi RoboKeep **si ferma subito**, al primo errore, e segna il job come fallito: continuare a leggere e scrivere su un supporto che dà errori fisici peggiora il danno. Per lo stesso motivo gli altri job della stessa sessione che usano quel disco **non vengono avviati**. Non rilanciare il backup a ripetizione: prima controlla.

1. **Cavo e alimentazione** — prova un altro cavo e un'altra porta USB (meglio una porta posteriore, senza hub). Se il disco ha un alimentatore, verifica quello.
2. **Salute del disco** — con un programma come **CrystalDiskInfo** (gratuito) guarda le voci *05 Settori riallocati*, *C5 Settori in attesa* e *C6 Errori non correggibili*: se sono diverse da zero, o lo stato è «A rischio», **il disco sta cedendo**. La voce *C7 Errori CRC UltraDMA* in crescita, con le altre a zero, punta invece al **collegamento tra il box e il disco**. Attenzione: un **cavo USB** difettoso non lascia alcuna traccia nello SMART, quindi un C7 a zero non lo assolve. E diffida dei box economici: alcuni dichiarano modello, seriale e soglie di un **altro** disco — in quel caso fanno fede solo i *valori grezzi*.

### Prima di un backup compare «errori disco recenti»

Lo SMART di un disco USB non si può leggere senza privilegi di amministratore, e Windows, senza, dichiara «integro» anche un disco con settori in attesa. RoboKeep guarda allora il **registro eventi di Windows**, che conserva per settimane blocchi danneggiati, errori di I/O e scritture perse: sono i segnali che di solito **precedono** un danno, a volte di un mese. Se negli ultimi 14 giorni ne trova per il disco sorgente o di destinazione, te lo dice prima di partire (e, nei backup pianificati, lo scrive in fondo al log).

L'avviso **non blocca** il backup, perché il registro nomina i dischi per lettera e numero, non per identità: se alterni due dischi sulla stessa lettera, gli errori dell'uno possono comparire mentre è collegato l'altro. Prendilo per quello che è — un buon motivo per controllare subito cavo, box, alimentazione e SMART.

Se il disco che cede è quello dei backup, **smetti di usarlo** e passa a uno sano: i tuoi dati originali sono al sicuro nella sorgente, le copie si ricreano. Se invece è il disco **sorgente**, metti in salvo i dati prima di ogni altra cosa.
