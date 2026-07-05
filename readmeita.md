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
punta-e-clicca, **versioni datate** dei tuoi file, copia dei **file che stai ancora usando**,
ogni job col **suo orario**, una **cronologia** completa e la **prova matematica** che le copie
sono integre. **Niente cloud, niente account, niente abbonamenti** — i tuoi file non lasciano
mai i tuoi dischi.

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
- ✅ **La prova matematica che il backup è integro** *(novità della 1.4)*. Il pulsante
  **Verifica** rilegge ogni file da entrambi i lati e confronta le impronte digitali (SHA-256):
  la corruzione silenziosa del disco — invisibile a qualunque controllo su data e dimensione —
  viene scovata. E con intelligenza: un file che hai modificato *dopo* il backup viene segnalato
  come tale, mai come falso allarme. A richiesta, o automatica dopo ogni backup per i job critici.
- ⏰ **Ogni job col suo orario** *(novità della 1.4)*. Documenti ogni sera, foto la domenica,
  archivi una volta al mese: ogni job ha la sua attività pianificata di Windows e parte anche ad
  app chiusa.
- 📜 **La memoria di ogni esecuzione** *(novità della 1.4)*. La finestra **Cronologia** elenca
  ogni backup e ogni verifica con esito, conteggi e durata — e col doppio clic il log completo si
  apre direttamente nell'app, senza frugare tra gli zip.
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
| ![Cronologia esecuzioni](docs/images/history.png) | **Ogni esecuzione a registro.** Backup e verifiche fianco a fianco, filtrabili per job; doppio clic su una voce e leggi il log completo senza toccare uno zip. |
| ![Pianificazione per job](docs/images/editor-schedule.png) | **Imposta e dimentica.** Ogni job può avere il suo orario — giornaliero, settimanale o mensile — più la verifica d'integrità automatica dopo ogni esecuzione. |
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

Vuoi che giri da solo? Dai a ogni job il suo orario direttamente nell'editor (giornaliero,
settimanale o mensile), oppure usa **Impostazioni → Pianificazione** per un'unica attività
"avvia tutto". In entrambi i casi parte anche ad app chiusa.

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
file aperti/bloccati via VSS · **verifica integrità (SHA-256), a richiesta o dopo ogni
esecuzione** · copia multi-thread · esclusioni di file e cartelle per job · "forza copia" per i
file con data/dimensione che non cambiano mai (container cifrati, alcuni database), con modalità
opzionale a confronto di contenuto · modalità riavviabile per i file enormi · anteprima/dry-run

**Ti tiene informato**
**cronologia esecuzioni con visualizzatore log in-app** · icone di salute per job con spiegazioni
in linguaggio semplice · controlli pre-avvio (destinazione raggiungibile, spazio disco, idoneità
VSS e versioning) · log in tempo reale · archivio log zippati per job con pulizia automatica ·
notifiche toast e area di notifica · report email (SMTP), anche solo in caso di errori

**Si adatta a te**
creazione guidata o editor manuale completo · anteprima esatta del comando · **pianificazione per
job (giornaliera / settimanale / mensile)** · share di rete con credenziali cifrate (DPAPI di
Windows) · **rallentamento della copia per i backup su rete** · **esporta/importa
configurazione** · riga di comando per l'automazione · ordinamento dei job con trascinamento ·
5 lingue · modalità portatile

## Da sapere

- **Solo Windows 10/11** — RoboKeep si appoggia a robocopy e ad altre funzioni native di Windows.
- **I file cambiati vengono ricopiati per intero** (niente copia a blocchi/delta): perfetto per
  documenti e foto, costoso per singoli file enormi che cambiano ogni giorno.
- **Le versioni richiedono una destinazione NTFS locale** (gli hard-link non esistono su exFAT o
  share di rete).
- **La copia dei file aperti richiede una sorgente NTFS locale** e una conferma amministratore
  (UAC) per esecuzione.
- **La verifica integrità rilegge ogni file da entrambi i lati**: è accurata per costruzione,
  quindi dura all'incirca quanto un primo backup. Attiva "verifica dopo ogni backup" solo dove
  conta davvero.
- Impostazioni, esiti e log vivono in `%APPDATA%\RoboKeep`: sopravvivono agli aggiornamenti
  dell'app. Le password sono cifrate con DPAPI di Windows, mai salvate in chiaro. Nota:
  l'ambito di cifratura predefinito è **a livello macchina** (così anche le attività
  pianificate possono decifrarle) — su un PC condiviso, passa all'ambito per-utente se gli
  altri account non devono poterle leggere.

## Privacy

RoboKeep non raccoglie **niente**. Zero telemetria, zero statistiche, zero controlli
aggiornamenti, zero account, zero traffico di rete — a meno che non configuri *tu* i report
email (verso il server SMTP che scegli, con TLS attivo di default). Tutto ciò che l'app sa —
impostazioni dei job, esiti, cronologia, log — vive in file locali sul tuo PC, JSON leggibili
che puoi ispezionare quando vuoi. I log contengono i percorsi dei file copiati e vengono
ripuliti automaticamente dopo 30 giorni (configurabile).

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

Distribuito con **licenza MIT** — vedi [LICENSE](LICENSE). I componenti di terze parti sono
elencati in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). © 2026 Roberto Serafini.
