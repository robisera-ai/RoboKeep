# Pianificazione automatica

Un backup serve davvero solo se parte da solo, senza che tu debba ricordartene. Con la
**pianificazione** dai a ogni job il suo orario e lo lasci lavorare, anche ad app chiusa.

## Un orario per ogni job

Apri il job con **Modifica**: nella sezione della pianificazione scegli con che frequenza deve
partire.

- **Giornaliero** — ogni giorno all'ora che indichi.
- **Settimanale** — nei giorni della settimana che scegli, all'ora indicata.
- **Mensile** — una volta al mese, nel giorno e all'ora che imposti. Attenzione ai giorni 29, 30
  e 31: Windows li fa scattare solo nei mesi che li hanno (il 31 gira sette volte l'anno). Per
  "a fine mese" spunta **Ultimo giorno del mese**: scatta il 28, 29, 30 o 31 a seconda del mese.

Ogni job ha il *suo* ritmo: i documenti ogni sera, le foto la domenica, gli archivi una volta al
mese. Salvi il job e la pianificazione è attiva.

## L'attivita' pianificata di Windows

Quando imposti un orario, RoboKeep crea per te un'**attivita' pianificata** di Windows. Da quel
momento il job parte all'ora prevista **anche con l'app chiusa**: è Windows a svegliarla, eseguire
quel job e chiuderla.

L'attivita' resta sempre **sincronizzata** con il job. Se lo rinomini, elimini o modifichi
l'orario, l'attivita' viene aggiornata di conseguenza. E se importi una configurazione da un altro
PC (vedi *Editor del job* per l'esportazione), le attivita' pianificate vengono ricreate in base a
ciò che importi.

> Le attivita' sono registrate tramite un file XML **indipendente dalla lingua**: i nomi dei
> giorni non vengono scritti a parole, così la pianificazione funziona identica su un Windows
> italiano, inglese o di qualunque altra lingua.

## Alternativa: una sola attivita' per tutto

Se preferisci non dare un orario a ogni singolo job, vai in **Impostazioni** e crea un'unica
attivita' **«avvia tutto»**: a un orario stabilito, Windows esegue in un colpo solo tutti i job
abilitati. È la scelta più semplice quando i tuoi backup possono girare tutti insieme.

## Credenziali e attivita' pianificate

Un'attivita' pianificata gira anche quando non hai effettuato l'accesso, quindi deve poter leggere
le eventuali credenziali di rete da sola. Per questo conta l'**ambito di cifratura** con cui sono
salvate. Se pianifichi job che scrivono su share di rete, leggi *Impostazioni* e *Backup su rete e
credenziali* per configurarlo nel modo giusto.
