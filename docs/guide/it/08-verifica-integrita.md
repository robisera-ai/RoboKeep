# Verifica dell'integrità

Un backup può sembrare a posto e non esserlo. Un file può **corrompersi in silenzio** sul disco —
un bit che cambia, un settore che cede — senza che data e dimensione se ne accorgano. La verifica
dell'integrità è la prova matematica che la copia è davvero intatta.

## Cosa fa il pulsante Verifica

Premi **Verifica** e RoboKeep **rilegge ogni file da entrambi i lati** — sorgente e destinazione —
e ne calcola l'impronta digitale **SHA-256**, un codice che cambia al minimo byte diverso. Se le
due impronte coincidono, i file sono identici bit per bit. Se differiscono, qualcosa non torna. È
un controllo che scova la corruzione silenziosa, **invisibile** a qualunque confronto su data o
dimensione.

## Intelligente sui falsi allarmi

Rileggere tutto potrebbe generare allarmi inutili. RoboKeep li evita:

- Un file che hai **modificato dopo** il backup viene segnalato come *"cambiato dopo il backup"*,
  non come corruzione: è una differenza legittima, non un difetto.
- La verifica **rispetta le esclusioni del job**: ciò che non copi non viene nemmeno controllato.
- I job con **versioni** verificano l'**ultimo snapshot**, cioè la copia più recente e coerente.

## Quando eseguirla

- **A richiesta**, quando vuoi una conferma: seleziona il job e premi **Verifica**.
- **Automatica dopo ogni backup**, con l'opzione per job **"Verifica dopo ogni backup"**: ogni
  esecuzione si conclude con il suo controllo di integrità, registrato in cronologia.

## Quanto dura

Poiché rilegge davvero **tutto**, una verifica dura all'incirca **quanto un primo backup**. È
lenta per scelta: la sicurezza si paga in lettura. Perciò attiva la verifica automatica **solo
dove conta davvero** — i dati critici — e per il resto eseguila a richiesta quando ti serve.

> Ogni verifica finisce in *cronologia*, con esito e durata; un doppio clic apre il log completo
> nell'app.
