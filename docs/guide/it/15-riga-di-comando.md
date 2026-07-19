# Riga di comando

RoboKeep si può avviare anche da **riga di comando**, senza aprire la finestra. È utile se vuoi costruire automazioni tue con l'**Utilità di pianificazione di Windows** o con uno script.

## I comandi

```
RoboKeep.exe --run-all              esegue tutti i job abilitati
RoboKeep.exe --job "Documenti"      esegue un singolo job
RoboKeep.exe --run-all --dry-run    solo anteprima, nessuna modifica
RoboKeep.exe --job "Foto" --config "D:\percorso\config.json"
```

- **`--run-all`** avvia in sequenza tutti i job abilitati.
- **`--job "Nome"`** avvia un solo job, indicandolo per nome.
- **`--dry-run`** esegue solo l'**anteprima**: RoboKeep mostra cosa *verrebbe* copiato o cancellato, **senza toccare nulla**.
- **`--config`** usa un file di configurazione diverso da quello predefinito.

## Codice di uscita

Al termine, RoboKeep restituisce un **exit code**. Uno **0** significa **tutto bene**: puoi usarlo nei tuoi script per sapere se proseguire.

> Anche un **job saltato per la rotazione dei dischi** conta come esito buono e restituisce **0**: non è un errore, sta solo aspettando il suo disco (vedi *Rotazione dei dischi*).

## Meglio la pianificazione per-job

La riga di comando è pensata per automazioni **personalizzate**. Ma se ti serve solo far partire i backup a un orario, non costruire nulla a mano: la **Pianificazione automatica** per-job, direttamente dall'editor, è più semplice e crea per te l'attività pianificata di Windows. Le dedichiamo il capitolo *Pianificazione automatica*.
