# Installazione e primo avvio

RoboKeep non ha un vero installer: scarichi un archivio, lo estrai e avvii l'app. In pochi
minuti sei pronto.

## Requisiti

Serve **Windows 10 o 11**. RoboKeep si appoggia a robocopy e ad altre funzioni native di
Windows, quindi non gira su altri sistemi.

## Scarica il pacchetto giusto

Dalla pagina **Releases** trovi due zip. Scegline uno:

- **Self-contained** — estrai e avvia, **non c'è nulla da installare**: include già .NET. Il
  download è più grande.
- **Framework-dependent** — molto più piccolo, ma richiede il **.NET 10 Desktop Runtime**
  (gratuito) installato una volta sul PC.

Se non sai quale prendere, scegli il self-contained: funziona e basta.

## Estrai e avvia

Estrai lo zip in una **cartella scrivibile** — per esempio `D:\Programmi\RoboKeep`. Evita
`C:\Program Files`: lì Windows limita la scrittura e RoboKeep salva accanto a sé alcuni file di
lavoro.

Poi apri la cartella e avvia **`RoboKeep.exe`**. Si apre la finestra principale.

## Un giro della finestra principale

- **La lista dei job** al centro: una riga per ogni backup, con le colonne *Attivo*, *Nome*,
  *Modalità*, *Sorgente*, *Destinazione* e *Ultimo esito*. Un'**icona di salute** ti segnala a
  colpo d'occhio se qualcosa non va (o se un job sta solo aspettando il suo disco).
- **La barra in alto**, con i comandi: *Nuovo*, *Modifica*, *Elimina*, *Versioni*, *Anteprima*,
  *Avvia selezionato*, *Avvia tutti*, *Verifica*, *Cronologia*, *Guida*, *Impostazioni*.
- **Il log di esecuzione** in basso: scorre in tempo reale mentre un backup gira.

## Dove vivono i dati

Impostazioni, esiti e log stanno in `%APPDATA%\RoboKeep`, così **sopravvivono agli
aggiornamenti** dell'app.

Preferisci tenere tutto insieme all'eseguibile — per esempio su una chiavetta? Attiva la
**modalità portatile**: crea un file vuoto chiamato `portable.flag` accanto a `RoboKeep.exe` e
da quel momento configurazione, log ed esiti restano nella cartella dell'app.

> Pronto? Nel capitolo *Il tuo primo backup* crei il primo job in pochi clic.
