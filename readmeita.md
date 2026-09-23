# RoboKeep

*Leggi in: [English](README.md) · Italiano · [Español](readmees.md) · [Français](readmefr.md) · [Deutsch](readmede.md)*

**Lo configuri una volta. Ogni file, ogni versione, al sicuro sul tuo disco.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Lingue](https://img.shields.io/badge/lingue-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Download](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep è un'app Windows di backup basata su `robocopy`, il motore di copia già presente in ogni PC.
Rispondi a qualche domanda e RoboKeep tiene le tue cartelle specchiate su un disco esterno, con
versioni datate a cui tornare quando serve. **Niente cloud, niente account, nessun abbonamento.**

![Finestra principale di RoboKeep: job con esiti a colpo d'occhio e log di esecuzione in tempo reale](docs/images/main-window.png)

## Perché RoboKeep

| | |
|---|---|
| 🧙 **Configurazione semplice** | La creazione guidata chiede dei tuoi dati in linguaggio semplice e sceglie le impostazioni giuste. Chi è esperto può comunque regolare tutto a mano. |
| 🕰️ **Torna indietro nel tempo** | Ogni esecuzione può salvare una versione datata. I file invariati sono condivisi tra le versioni, quindi dieci versioni non costano dieci volte lo spazio. |
| 💿 **Rispetta i tuoi dischi** | Si ferma al primo errore hardware invece di insistere per ore, tiene il PC sveglio durante il backup, va piano sui dischi meccanici. |
| 🩺 **Salute dischi a colpo d'occhio** | Un clic legge lo SMART di ogni disco e dà un verdetto semplice: Buono, Attenzione, Pericolo — con ogni valore spiegato. *Novità della 1.8.* |
| 🛡️ **Mai il disco sbagliato** | Alterni due dischi esterni che Windows chiama entrambi `E:`? Ogni job riconosce il proprio disco dall'identità e semplicemente lo aspetta. |
| ✅ **La prova che ha funzionato** | Verifica rilegge entrambi i lati e confronta le impronte SHA-256. La corruzione silenziosa viene scovata. |
| 🔓 **Anche i file aperti** | Archivi di Outlook, database, qualsiasi file bloccato: una casella copia da una copia shadow di Windows. |
| ⏰ **Gira da solo** | Ogni job ha il suo orario (giornaliero, settimanale, mensile, anche l'ultimo giorno del mese) e parte anche ad app chiusa. |
| 🚨 **Si fa sentire solo quando conta** | Icone colorate e tooltip semplici per i job falliti, in ritardo o interrotti. Un disco scollegato è una clessidra, non un allarme. |
| 🏠 **Davvero tuo** | Gratuito e open source (MIT), tutto in locale, zero telemetria. Cinque lingue. Portatile, se lo vuoi. |

## Parti in due minuti

1. Scarica l'ultima versione dalla pagina **[Releases](../../releases)**:
   - **`…-selfcontained.zip`**: estrai e avvia, niente da installare (download più grande);
   - **`…-framework-dependent.zip`**: molto più piccolo, richiede il
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) gratuito.
2. Estrai lo zip in una **cartella scrivibile**, ad es. `D:\Programmi\RoboKeep` (non `C:\Program Files`).
3. Avvia **`RoboKeep.exe`** → **Nuovo** → rispondi alle domande della creazione guidata →
   **Avvia tutti**. Fatto.

> **Windows SmartScreen ti avvisa?** RoboKeep non è ancora firmato digitalmente. Clicca su *Ulteriori
> informazioni* → *Esegui comunque*, oppure fai clic destro sullo zip → Proprietà → **Sblocca**
> prima di estrarlo.

## Guardalo in azione

| | |
|---|---|
| ![Creazione guidata](docs/images/wizard.png) | **Creazione guidata.** Poche domande, inclusa quella sui file che restano aperti mentre lavori, e il job è configurato per te. |
| ![Editor del job](docs/images/editor.png) | **Tutto sotto controllo.** Mirror o accumulo, copia dei file aperti, opzioni per i file grandi, ognuna spiegata in una riga. |
| ![Anteprima comando e versioning](docs/images/editor-preview.png) | **Niente di nascosto.** Versioni datate, esclusioni per job e il comando robocopy esatto sempre in vista. |
| ![Cronologia esecuzioni](docs/images/history.png) | **Ogni esecuzione a registro.** Backup e verifiche fianco a fianco; doppio clic apre il log completo. |
| ![Pianificazione per job](docs/images/editor-schedule.png) | **Imposta e dimentica.** Giornaliera, settimanale o mensile, più le verifiche d'integrità periodiche. |
| ![Sfoglia le versioni](docs/images/versions.png) | **Scegli una data.** Il backup di quel giorno si apre in Esplora file; ricopia quello che ti serve. |

## Come si comporta un backup

Ogni job è una coppia cartella sorgente → cartella destinazione. Ad ogni esecuzione RoboKeep:

- **salta** i file non cambiati (la seconda esecuzione dura pochi secondi);
- **aggiorna** i file più recenti nella sorgente;
- in modalità **mirror** (predefinita) **rimuove** dalla destinazione anche ciò che hai cancellato;
- con mirror disattivato, aggiunge e aggiorna soltanto, **senza mai cancellare**.

Con le versioni attive, lo stato precedente viene salvato prima come snapshot datato.

## Novità della 1.8

- **Finestra Salute dischi**: SMART di ogni disco, spiegato in parole semplici. NVMe senza
  richieste; SATA e USB con una conferma da amministratore, l'unico modo per attraversare i box USB.
- **Controllo aggiornamenti facoltativo**: RoboKeep chiede una volta se può cercare nuove versioni
  su GitHub. Quando ce n'è una, un banner propone *Novità*, *Scarica*, *Ignora*. Sostituisci i
  file tu.
- **Creazione guidata più semplice**: chiede solo ciò che conta, salva il job appena finisci,
  gestisce da sola le credenziali di rete.
- Attivare le versioni su un job esistente **non ricopia più tutto**; un job annullato lascia
  comunque un log; le pianificazioni mensili possono girare **l'ultimo giorno del mese**.

Cronologia completa nel [CHANGELOG](CHANGELOG.md).

<details>
<summary><b>Tutte le funzioni</b></summary>

**Backup**: mirror o accumulo · protezione dalla rotazione dei dischi (job legato al suo disco
tramite l'identità del volume) · versioni datate con hard-link e ritenzione configurabile, nessuna
versione doppia se non è cambiato nulla · copia dei file aperti/bloccati via VSS · verifica
d'integrità SHA-256, a richiesta o periodica · stop su errore hardware che mette a riposo il
disco · tetto automatico ai thread sui dischi meccanici · PC tenuto sveglio · copia multi-thread ·
esclusioni per job · "forza copia" per i file con data/dimensione che non cambiano mai, con
modalità opzionale a confronto di contenuto · modalità riavviabile per i file enormi ·
anteprima/dry-run.

**Ti tiene informato**: cronologia esecuzioni con un log per ogni voce, aperto nel Blocco note ·
pulsante Cartella log · icone di salute per job con tooltip in linguaggio semplice · icone che si
aggiornano nell'istante in cui colleghi o scolleghi un disco · controlli pre-avvio (destinazione
raggiungibile, spazio disco, idoneità VSS e versioning, errori disco recenti dal registro eventi
di Windows) · finestra Salute dischi (SMART) · log in tempo reale · archivio log zippati con
pulizia automatica · notifiche toast e area di notifica · report via email (SMTP), anche solo in
caso di errori.

**Si adatta a te**: creazione guidata o editor manuale completo · anteprima esatta del comando ·
pianificazione per job (giornaliera / settimanale / mensile / ultimo giorno del mese) · share di
rete con credenziali cifrate DPAPI · rallentamento della copia per i backup su rete ·
esporta/importa configurazione · riga di comando per l'automazione · ordinamento dei job con
trascinamento · controllo aggiornamenti facoltativo · 5 lingue · modalità portatile.

</details>

<details>
<summary><b>Da sapere</b></summary>

- **Solo Windows 10/11.** RoboKeep si appoggia a robocopy e ad altre funzioni native di Windows.
- **I file cambiati vengono ricopiati per intero** (niente copia a blocchi/delta): perfetto per
  documenti e foto, costoso per singoli file enormi che cambiano ogni giorno.
- **Le versioni richiedono una destinazione NTFS locale** (gli hard-link non esistono su exFAT o
  share di rete).
- **La copia dei file aperti richiede una sorgente NTFS locale** e una conferma amministratore
  (UAC) per ogni esecuzione.
- **Le verifiche d'integrità rileggono ogni file da entrambi i lati**, quindi durano all'incirca
  quanto un primo backup. Per questo la verifica automatica gira ogni 7 giorni per default (0 =
  dopo ogni esecuzione).
- **Un errore hardware ferma il job di proposito.** Prima di rilanciare, controlla cavo, box USB e
  alimentazione (sui dischi esterni causano esattamente gli stessi errori di un disco che cede),
  poi apri **Salute dischi**.
- Impostazioni, esiti e log vivono in `%APPDATA%\RoboKeep` e sopravvivono agli aggiornamenti. Le
  password sono cifrate con DPAPI di Windows; l'ambito predefinito è a livello macchina, così
  anche le attività pianificate possono leggerle. Su un PC condiviso, passa all'ambito per-utente.

</details>

<details>
<summary><b>Per utenti esperti</b></summary>

```text
RoboKeep.exe --run-all              esegue tutti i job abilitati (exit code 0 = tutto ok)
RoboKeep.exe --job "Documents"      esegue un singolo job
RoboKeep.exe --run-all --dry-run    solo anteprima, nessuna modifica
RoboKeep.exe --job "Photos" --config "D:\path\config.json"
```

Compilazione dai sorgenti: `dotnet build src/RoboKeep.sln -c Release` (.NET 10 SDK); test:
`dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`. Modalità portatile: un file vuoto
`portable.flag` accanto all'exe tiene tutto nella cartella dell'app. Config di esempio:
[config/config.example.json](config/config.example.json). Retroscena e decisioni tecniche:
[ANALISI.md](ANALISI.md).

</details>

## Privacy

RoboKeep non raccoglie **niente**. Zero telemetria, zero analitica, nessun account. L'unico
traffico di rete è quello che attivi tu: il controllo aggiornamenti facoltativo (una richiesta a
GitHub) e i report via email (il tuo server SMTP, TLS di default). Tutto ciò che sa vive in file
JSON leggibili sul tuo PC.

## Contribuisci

Le traduzioni sono mantenute dalla community: hai notato un termine che un madrelingua direbbe
diversamente? [Le pull request sono benvenute](../../pulls), anche una correzione di una riga.

## Licenza

**Licenza MIT**, vedi [LICENSE](LICENSE). Componenti di terze parti in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). © 2026 Roberto Serafini.
