# Notifiche ed email

RoboKeep può dirti com'è andato un backup senza che tu resti a guardare la finestra. Scegli tu quanto vuoi essere avvisato: una notifica al volo, un'app che gira defilata, o un report che arriva nella tua casella.

## Notifiche toast

Alla fine di ogni job, RoboKeep può mostrare una **notifica toast** di Windows — quella che compare in basso a destra — con l'esito. Un colpo d'occhio e sai se è andato tutto bene, senza aprire l'app.

Attivi e disattivi le notifiche dalle **Impostazioni**.

## Area di notifica

Se vuoi che RoboKeep resti a portata di mano ma fuori dai piedi, usa l'**area di notifica** (il tray, vicino all'orologio):

- **Riduci nel tray**: chiudendo la finestra, l'app non esce ma si nasconde tra le icone di sistema.
- **Avvio minimizzato**: all'accensione RoboKeep parte già ridotto nel tray.
- **Gira in background**: resta attivo per mostrarti le notifiche e reagire quando colleghi o scolleghi un disco.

Un clic sull'icona nel tray riporta su la finestra.

## Report via email (SMTP)

Se vuoi ricevere un resoconto anche quando non sei al PC, configura i **report via email**. RoboKeep li invia tramite un server **SMTP** a tua scelta.

Ti servono pochi dati:

- **Server e porta**: l'indirizzo del tuo server SMTP. Il **TLS è attivo di default**, sulla **porta 587** — la configurazione consigliata per la posta cifrata.
- **Credenziali del mittente**: utente e password dell'account da cui parte l'email. La password viene cifrata come tutte le altre.
- **Destinatario**: l'indirizzo a cui recapitare il report.

C'è anche l'opzione **solo in caso di errori**: se la attivi, ricevi un'email soltanto quando qualcosa è andato storto, e nessuna quando fila tutto liscio.

> I report partono **dopo ogni esecuzione reale** di un job. Un'anteprima non genera email: sta solo mostrando cosa succederebbe, non tocca nulla.

Le impostazioni email fanno parte della configurazione: le trovi tutte raccolte nel capitolo *Impostazioni*.
