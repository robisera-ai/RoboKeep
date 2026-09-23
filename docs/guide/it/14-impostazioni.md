# Impostazioni

Le **Impostazioni** raccolgono le scelte che valgono per tutta l'app, non per un singolo job. Le apri dalla barra in alto. Ecco cosa trovi.

## Lingua

RoboKeep parla **cinque lingue**: italiano, inglese, spagnolo, francese, tedesco. Scegli la tua e l'interfaccia cambia subito.

## Log e cartelle

- **Cartella dei log**: dove RoboKeep archivia i registri di ogni esecuzione.
- **Giorni di ritenzione**: per quanti giorni conservare i log prima della pulizia automatica.
- **Cartella temporanea**: lo spazio di lavoro che l'app usa durante le operazioni.

## Cifratura delle credenziali (DPAPI)

Le password (share di rete, mittente email) sono cifrate con **DPAPI di Windows**, mai in chiaro. Qui scegli l'**ambito** della cifratura:

- **A livello macchina** (predefinito): qualsiasi account sul PC può decifrarle. Serve così perché le **attività pianificate** possano leggere le credenziali e girare ad app chiusa.
- **Per-utente**: solo il tuo account può decifrarle. Consigliato su un **PC condiviso**, dove altri utenti non devono poterle leggere.

> Se cambi questa opzione, le password già salvate vengono **ricifrate in automatico** con il nuovo ambito. Non devi reinserirle.

## Notifiche e area di notifica

Da qui attivi le **notifiche toast** e i comportamenti del **tray** (riduci nel tray, avvio minimizzato, gira in background). I dettagli sono nel capitolo *Notifiche ed email*.

## Avviso di backup fermo

La soglia **«avvisa se un backup è fermo da N giorni»** fa comparire un'icona d'allerta sui job che non girano da troppo tempo. Imposta **0** per non ricevere mai questo avviso.

## Aggiornamenti

La casella **«Cerca aggiornamenti automaticamente»** decide se RoboKeep controlla da solo l'esistenza di una versione nuova: una richiesta a `api.github.com` all'avvio, al massimo una volta al giorno. Se trova una versione più recente, l'avviso compare nella finestra principale con **Novità**, **Scarica** e **Ignora**. Spenta, RoboKeep non contatta mai GitHub di sua iniziativa.

Nella scheda **Info** trovi anche il pulsante **«Controlla ora»**, che fa la verifica subito — funziona anche a casella spenta, perché è un'azione esplicita che decidi tu lì per lì. Il risultato compare sotto il pulsante: *sei aggiornato*, oppure *è disponibile la versione X* (con l'avviso che compare nella finestra principale), oppure *controllo non riuscito* se manca la rete o GitHub non è raggiungibile.

## Salute dischi

Il pulsante **«Salute dischi»**, nella barra della finestra principale, legge lo SMART di ogni disco fisico e mostra un verdetto spiegato. Per i dischi SATA e USB serve una richiesta di amministratore, una per ogni lettura; il pulsante **Aggiorna** nella finestra ripete la lettura. Il significato dei valori è nel capitolo *Risoluzione problemi*.

## Esporta e importa configurazione

Puoi salvare tutta la tua configurazione in un file e ricaricarla altrove.

- **Esporta**: scrive impostazioni e job in un file.
- **Importa**: prima **convalida** il file; poi salva una **copia di sicurezza** della configurazione attuale; infine **risincronizza le attività pianificate** con i nuovi job.

## Dove vivono le impostazioni

Tutto risiede in **`%APPDATA%\RoboKeep`**, così sopravvive agli aggiornamenti dell'app. In **modalità portatile**, invece, sta nella cartella dell'app.
