# Copiare i file aperti (VSS)

Alcuni file non si lasciano copiare mentre li stai usando: l'archivio di Outlook, un database, un
documento tenuto aperto da un altro programma. Windows li **blocca** e una copia normale li salta
o fallisce. RoboKeep ha una soluzione.

## Il problema

Quando un programma tiene un file aperto, il file è bloccato: nessun altro può leggerlo per intero
in modo affidabile. È il motivo per cui i backup dei dati "vivi" — posta, gestionali, database —
spesso lasciano indietro proprio i file che contano di più.

## La soluzione: la copia shadow

Spunta la casella **"Copia anche i file aperti (VSS)"** nel job. Prima di copiare, RoboKeep chiede
a Windows una **copia shadow** (VSS): un'istantanea congelata del disco in quell'istante. Il backup
legge da quell'istantanea, non dai file vivi — così anche ciò che è bloccato viene copiato in modo
coerente, come fotografato in un momento preciso.

## Cosa serve

- Una **sorgente NTFS locale**: la copia shadow è una funzione di Windows che vale per i dischi
  locali, non per le share di rete.
- **Una conferma amministratore (UAC) per ogni esecuzione**: scattare l'istantanea richiede i
  privilegi giusti, quindi al via ti comparirà la richiesta di Windows. È normale.

## Se l'istantanea non è possibile

Non tutti gli ambienti permettono la copia shadow. Se per qualsiasi motivo lo snapshot non riesce,
il backup **prosegue normalmente, con un avviso**: copia ciò che può copiare e ti segnala che i
file aperti potrebbero non essere inclusi. Non si blocca **mai** per questo motivo — meglio un
backup con un avviso che nessun backup.

> Usa questa opzione dove hai davvero file sempre aperti (posta, database). Dove i file sono
> chiusi durante il backup, non serve.
