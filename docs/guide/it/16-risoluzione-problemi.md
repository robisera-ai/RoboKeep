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

### Compare «Errore imprevisto»

Qualcosa nell'app è andato storto in un punto non previsto. RoboKeep **resta aperto** e scrive i dettagli in `crash.log` nella cartella dati (`%APPDATA%\RoboKeep`, oppure la cartella dell'app in modalità portatile). Se qualcosa non risponde più, chiudi e riapri l'app. Il file è utile per segnalare il problema: contiene data, versione e la traccia dell'errore, nessun dato personale oltre ai percorsi eventualmente coinvolti.

### Un'attività pianificata non parte

Controlla che l'**ambito di cifratura DPAPI** delle credenziali sia **a livello macchina**: solo così l'attività pianificata può decifrare le credenziali e girare ad app chiusa. Con l'ambito per-utente, l'attività non riesce a leggerle. Vedi *Impostazioni*.

### «Destinazione non raggiungibile»

Il disco o la share di destinazione **non sono collegati**. Collega il disco esterno o verifica la connessione di rete, poi riprova.

### La verifica dice «modificati dopo il backup»

Non è **corruzione**. Significa che hai **modificato i file dopo averli copiati**: sono più recenti nella sorgente rispetto alla copia. RoboKeep te lo segnala così apposta, per non spaventarti con un falso allarme. Al prossimo backup tornano allineati.

### Un job si interrompe con «ERRORE HARDWARE» (CRC, controllo di ridondanza ciclico)

L'errore arriva da Windows, non da RoboKeep, e significa che **un disco — o il suo collegamento — ha segnalato un problema fisico**: errore nei dati (CRC), settore non trovato o errore del dispositivo. Le cause sono due, e vanno distinte: un **disco che sta cedendo** (settori danneggiati) oppure **cavo, box USB o alimentazione** difettosi, che su un disco esterno danno esattamente gli stessi messaggi.

In questi casi RoboKeep **si ferma subito**, al primo errore, e segna il job come fallito: continuare a leggere e scrivere su un supporto che dà errori fisici peggiora il danno. Per lo stesso motivo gli altri job della stessa sessione che usano quel disco **non vengono avviati**. Non rilanciare il backup a ripetizione: prima controlla. Il disco resta **a riposo anche nei giorni seguenti** (anche per i backup pianificati) finché non lo riattivi con il pulsante **Riattiva dischi** della finestra principale, che compare solo quando serve; per sicurezza si riattiva da solo dopo 7 giorni. L'email di esito ha oggetto «ERRORE HARDWARE» e riporta in cima cosa è successo e cosa controllare.

1. **Cavo e alimentazione** — prova un altro cavo e un'altra porta USB (meglio una porta posteriore, senza hub). Se il disco ha un alimentatore, verifica quello.
2. **Salute del disco** — con un programma come **CrystalDiskInfo** (gratuito) guarda le voci *05 Settori riallocati*, *C5 Settori in attesa* e *C6 Errori non correggibili*: se sono diverse da zero, o lo stato è «A rischio», **il disco sta cedendo**. La voce *C7 Errori CRC UltraDMA* in crescita, con le altre a zero, punta invece al **collegamento tra il box e il disco**. Attenzione: un **cavo USB** difettoso non lascia alcuna traccia nello SMART, quindi un C7 a zero non lo assolve. E diffida dei box economici: alcuni dichiarano modello, seriale e soglie di un **altro** disco — in quel caso fanno fede solo i *valori grezzi*.

### Prima di un backup compare «errori disco recenti»

Lo SMART di un disco USB non si può leggere senza privilegi di amministratore, e Windows, senza, dichiara «integro» anche un disco con settori in attesa. RoboKeep guarda allora il **registro eventi di Windows**, che conserva per settimane blocchi danneggiati, errori di I/O e scritture perse: sono i segnali che di solito **precedono** un danno, a volte di un mese. Se negli ultimi 14 giorni ne trova per il disco sorgente o di destinazione, te lo dice prima di partire (e, nei backup pianificati, lo scrive in fondo al log). Per leggere davvero lo SMART di quel disco c'è il pulsante **«Salute dischi»**, che chiede l'autorizzazione di amministratore quando serve: vedi *Leggere la salute del disco*, qui sotto.

L'avviso **non blocca** il backup, perché il registro nomina i dischi per lettera e numero, non per identità: se alterni due dischi sulla stessa lettera, gli errori dell'uno possono comparire mentre è collegato l'altro. Prendilo per quello che è — un buon motivo per controllare subito cavo, box, alimentazione e SMART.

Se il disco che cede è quello dei backup, **smetti di usarlo** e passa a uno sano: i tuoi dati originali sono al sicuro nella sorgente, le copie si ricreano. Se invece è il disco **sorgente**, metti in salvo i dati prima di ogni altra cosa.

### Leggere la salute del disco

Il pulsante **«Salute dischi»**, nella barra della finestra principale, legge lo SMART di ogni disco fisico collegato e mostra un verdetto in chiaro, con i valori che contano spiegati uno per uno. Per i dischi **NVMe** la lettura non chiede nulla; per i dischi **SATA e USB** serve **una richiesta di amministratore** (una sola per ogni lettura: è l'unica strada che attraversa i box USB). È una lettura **soltanto**: nessun test SMART viene avviato, niente viene scritto sui dischi.

Il verdetto è uno tra:

- **Buono** — nessun valore critico.
- **Attenzione** — un valore da tenere d'occhio (settori in attesa, usura, temperatura alta).
- **Pericolo** — il disco sta cedendo: metti al sicuro i dati e sostituiscilo appena puoi.

I valori che contano, per i dischi ATA/SATA/USB:

- **05 Settori riallocati** — sopra zero il disco sta cedendo: sostituiscilo.
- **C5 Settori in attesa** — settori illeggibili non ancora riscritti, spesso il segno di **scritture interrotte** (cavo staccato, alimentazione mancata) più che di un guasto. Una **formattazione completa** li riscrive; se dopo la formattazione i riallocati (05) salgono, allora il disco sta davvero cedendo. È successo proprio a un disco di questo progetto: 17 settori in attesa con 05 a zero, spariti dopo una formattazione completa.
- **C6 Settori non correggibili** — dati persi per sempre in quei settori: sostituisci il disco.
- **C7 Errori CRC sul collegamento** — errori di trasmissione tra il box (o il controller) e il disco, non sul disco stesso. La riga **compare solo quando il valore è maggiore di zero**: su un collegamento sano non avrebbe nulla da dire. Un **cavo USB difettoso non lascia alcuna traccia qui**: un C7 a zero non lo esclude.
- **BB Errori non correggibili segnalati** — storico cumulativo dall'origine del disco. Non conta il numero assoluto, ma se **cresce** tra una lettura e la successiva.
- **Temperatura** — un disco meccanico sopra i **50 °C** soffre; un NVMe sta bene fino a **70 °C**.

Per i dischi **NVMe** i valori sono diversi: **avviso critico** (qualunque cosa diversa da zero è grave), **riserva disponibile** rispetto alla soglia del produttore (sotto soglia è grave), **errori del supporto** (media errors, gravi), **percentuale di usura** (attenzione oltre il 90 %), **spegnimenti non protetti** (informativo).
