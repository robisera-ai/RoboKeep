# RoboKeep

*Leggi in: [English](README.md) · Italiano*

**Lo configuri una volta — ogni file, ogni versione, al sicuro sul tuo disco.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Lingue](https://img.shields.io/badge/lingue-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Download](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep è un'app Windows amichevole che trasforma `robocopy` — il motore di copia solido come
una roccia già incluso in ogni PC Windows — in un **vero strumento di backup**: configurazione
punta-e-clicca, **versioni datate** dei tuoi file, copia dei **file che stai ancora usando** e
avvisi chiari quando un backup resta indietro. **Niente cloud, niente account, niente
abbonamenti** — i tuoi file non lasciano mai i tuoi dischi.

![Finestra principale di RoboKeep: job con esiti a colpo d'occhio e log in tempo reale](docs/images/main-window.png)

## Perché ti piacerà

- 🧙 **Rispondi a qualche domanda, ottieni il backup giusto.** Non serve conoscere robocopy: la
  creazione guidata chiede dei tuoi dischi e dei tuoi dati in linguaggio semplice, e sceglie per
  te le impostazioni ottimali. Chi è esperto può comunque regolare tutto a mano.
- 🕰️ **Una macchina del tempo per i tuoi file.** Ogni esecuzione può salvare una **versione
  datata** del backup. Hai cancellato un paragrafo martedì scorso? Apri la versione di martedì e
  lo recuperi. Il trucco intelligente: i file invariati sono *condivisi* tra le versioni, quindi
  dieci versioni non costano dieci volte lo spazio — solo ciò che è cambiato davvero.
- 🔓 **Copia i file anche mentre li stai usando** *(novità della 1.3)*. Archivi di Outlook,
  database, file bloccati da altri programmi: con una casella, RoboKeep fotografa il disco per un
  istante (una "copia shadow" di Windows) e copia da quell'immagine congelata. E se lo snapshot
  non è possibile, il backup prosegue semplicemente nel modo normale — non si blocca mai.
- 🚨 **Ti avvisa quando qualcosa non va.** Un backup che fallisce in silenzio è peggio di nessun
  backup. RoboKeep segna ogni job con problemi con un'icona colorata — rossa per fallito, ambra
  per "non eseguito da troppo tempo", arancio per "era stato interrotto" — con la spiegazione in
  parole semplici passandoci sopra il mouse.
- 🔍 **Niente di nascosto.** L'editor mostra sempre il **comando esatto** che verrà eseguito.
  Puoi provare qualsiasi backup in anteprima per vedere cosa verrebbe copiato o cancellato,
  prima di toccare qualunque cosa.
- 🏠 **Davvero tuo.** Gratuito e open source (MIT), tutto in locale, zero telemetria. Parla
  italiano, inglese, spagnolo, francese e tedesco. Portatile, se lo vuoi.

## Guardalo in azione

| | |
|---|---|
| ![Creazione guidata](docs/images/wizard.png) | **Creazione guidata.** Poche domande in linguaggio semplice — inclusa quella sui file che restano aperti mentre lavori — e il wizard configura il job per te. |
| ![Editor del job](docs/images/editor.png) | **Tutto sotto controllo.** Mirror o accumulo, copia dei file aperti, opzioni per i file grandi: ogni scelta spiegata in una riga, con un suggerimento dove serve. |
| ![Anteprima comando e versioning](docs/images/editor-preview.png) | **Trasparenza totale.** Versioni datate con pulizia automatica, esclusioni per job, e il comando robocopy esatto sempre in vista. |
| ![Sfoglia le versioni](docs/images/versions.png) | **Torna indietro nel tempo.** Scegli una data, un clic, e il backup di quel giorno si apre in Esplora file. Ricopia quello che ti serve. |

## Parti in due minuti

1. Scarica l'ultima versione dalla pagina **[Releases](../../releases)**:
   - **`…-selfcontained.zip`** — estrai e avvia, **niente da installare** (include .NET, download più grande);
   - **`…-framework-dependent.zip`** — molto più piccolo, richiede il
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) gratuito, da installare una volta.
2. Estrai lo zip in una **cartella scrivibile** (es. `D:\Programmi\RoboKeep` — evita
   `C:\Program Files`).
3. Avvia **`RoboKeep.exe`**, clicca **Nuovo**, rispondi alle domande della creazione guidata,
   poi **Avvia tutti**. Fatto.

Vuoi che giri da solo? **Impostazioni → Pianificazione** crea un'attività pianificata di Windows
che esegue i tuoi job all'ora che scegli — anche ad app chiusa.

## Come si comporta un backup

Per ogni job (coppia cartella sorgente → destinazione, sottocartelle incluse), RoboKeep:

- **salta** i file non cambiati (per questo la seconda esecuzione dura pochi secondi);
- **aggiorna** i file più recenti nella sorgente;
- in modalità **mirror** (predefinita) **rimuove** dalla destinazione ciò che hai cancellato
  dalla sorgente — la destinazione resta una copia esatta;
- con mirror disattivato, aggiunge e aggiorna soltanto, **senza mai cancellare**.

E se hai attivato le versioni, ogni esecuzione salva prima lo stato precedente come snapshot datato.

## Tutte le funzioni

**Backup**
modalità mirror o accumulo · versioni datate con hard-link e ritenzione configurabile · copia dei
file aperti/bloccati via VSS · copia multi-thread · esclusioni di file e cartelle per job ·
"forza copia" per i file con data/dimensione che non cambiano mai (container cifrati, alcuni
database), con modalità opzionale a confronto di contenuto · modalità riavviabile per i file
enormi · anteprima/dry-run

**Ti tiene informato**
icone di salute per job con spiegazioni in linguaggio semplice · controlli pre-avvio
(destinazione raggiungibile, spazio disco, idoneità VSS e versioning) · log in tempo reale ·
archivio log zippati per job con pulizia automatica · notifiche toast e area di notifica ·
report email (SMTP), anche solo in caso di errori

**Si adatta a te**
creazione guidata o editor manuale completo · anteprima esatta del comando · share di rete con
credenziali cifrate (DPAPI di Windows) · pianificazione con l'Utilità di pianificazione di
Windows · riga di comando per l'automazione · ordinamento dei job con trascinamento · 5 lingue ·
modalità portatile

## Da sapere

- **Solo Windows 10/11** — RoboKeep si appoggia a robocopy e ad altre funzioni native di Windows.
- **I file cambiati vengono ricopiati per intero** (niente copia a blocchi/delta): perfetto per
  documenti e foto, costoso per singoli file enormi che cambiano ogni giorno.
- **Le versioni richiedono una destinazione NTFS locale** (gli hard-link non esistono su exFAT o
  share di rete).
- **La copia dei file aperti richiede una sorgente NTFS locale** e una conferma amministratore
  (UAC) per esecuzione.
- Impostazioni, esiti e log vivono in `%APPDATA%\RoboKeep`: sopravvivono agli aggiornamenti
  dell'app. Le password sono cifrate con DPAPI di Windows, mai salvate in chiaro.

## Per utenti esperti

```text
RoboKeep.exe --run-all              esegue tutti i job abilitati (exit code 0 = tutto ok)
RoboKeep.exe --job "Documenti"      esegue un singolo job
RoboKeep.exe --run-all --dry-run    solo anteprima, nessuna modifica
RoboKeep.exe --job "Foto" --config "D:\percorso\config.json"
```

Compilazione dai sorgenti: `dotnet build src/RoboKeep.sln -c Release` (richiede il .NET 10 SDK) —
i test si eseguono con `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`.
Modalità portatile: crea un file vuoto `portable.flag` accanto all'exe e tutto (config, log,
esiti) resta nella cartella dell'app. Config di esempio:
[config/config.example.json](config/config.example.json). Retroscena e decisioni tecniche del
progetto: [ANALISI.md](ANALISI.md).

## Licenza

Distribuito con **licenza MIT** — vedi [LICENSE](LICENSE). © 2026 Roberto Serafini.
