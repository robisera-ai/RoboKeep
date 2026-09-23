# Controllo aggiornamenti — design

Data: 2026-09-23. Stato: approvato a voce, in attesa di revisione del testo.

## Scopo

Chi usa RoboKeep deve sapere quando esiste una versione nuova, perché le protezioni introdotte
dopo la 1.7.0 non servono a niente se le copie installate restano vecchie. Oggi l'app non fa
nessuna connessione di rete e la guida lo promette: la funzione deve rispettare quella promessa,
cioè non fare nulla senza consenso esplicito e dichiarare esattamente cosa fa.

## Cosa fa

- **Consenso una volta.** Al primo avvio della finestra dopo l'aggiornamento (impostazione
  `UpdateCheck` ancora indecisa), una finestra chiede: «Vuoi che RoboKeep controlli se ci sono
  aggiornamenti? Fa una sola richiesta a GitHub, senza inviare dati tuoi.» Sì/No, salvato nelle
  impostazioni. La domanda non si ripete.
- **Impostazioni → Generale:** casella «Cerca aggiornamenti automaticamente» e, accanto alla
  versione corrente, il pulsante «Controlla ora» con l'esito in linea («Sei aggiornato», «È
  disponibile la X», «Controllo non riuscito»).
- **Controllo automatico:** solo nell'app con finestra, mai in modalità riga di comando né
  dall'attività pianificata. All'avvio, in background, al massimo una volta ogni 24 ore
  (`LastUpdateCheck` nelle impostazioni), timeout 5 secondi. Qualunque errore (rete assente,
  GitHub non raggiungibile, risposta inattesa) è silenzioso: nessun messaggio a video.
- **Avviso:** un banner nella finestra principale, sotto la barra dei pulsanti: «È disponibile
  RoboKeep 1.8.0 (hai la 1.7.0)». Pulsanti: **Novità** (apre la pagina della release nel
  browser), **Scarica**, **Ignora questa versione** (salva `IgnoredUpdateVersion`; la versione
  successiva viene proposta di nuovo).
- **Scarica:** RoboKeep riconosce da solo se gira come *self-contained* o *framework-dependent*
  e scarica il pacchetto corrispondente nella cartella Download di Windows, con avanzamento nel
  banner. A fine scaricamento confronta la dimensione con quella dichiarata da GitHub: se non
  coincide cancella il file e invita a riprovare; se coincide apre Esplora risorse con il file
  selezionato. La sostituzione dei file la fa l'utente: chiude RoboKeep, estrae lo zip sopra la
  cartella attuale, riapre. Il banner lo dice in una riga.
- **Niente aggiornamento automatico**, mai: un programma di backup non si sostituisce da solo.

## Come è fatto

- `RoboKeep.Core/Services/UpdateChecker.cs` — parte pura + rete.
  - `record UpdateInfo(Version Latest, string ReleaseUrl, string SelfContainedUrl, long
    SelfContainedSize, string FrameworkDependentUrl, long FrameworkDependentSize)`.
  - `static UpdateInfo? Parse(string json)` — legge la risposta di
    `GET https://api.github.com/repos/robisera-ai/RoboKeep/releases/latest`: `tag_name`
    (`vX.Y.Z`), `html_url`, e in `assets[]` i due zip riconosciuti dal nome
    (`RoboKeep-<ver>-win-x64-selfcontained.zip`, `…-framework-dependent.zip`) con
    `browser_download_url` e `size`. Funzione pura, testata con un JSON di esempio.
  - `static bool IsNewer(Version latest, Version current, string? ignored)` — pura.
  - `static bool IsDue(DateTime? lastCheck, DateTime now)` — pura, 24 ore.
  - `Task<UpdateInfo?> FetchAsync(CancellationToken)` — `HttpClient` con `User-Agent:
    RoboKeep/<versione>` (GitHub lo esige), timeout 5 s, restituisce null su qualunque errore.
  - `Task DownloadAsync(string url, string targetPath, long expectedSize, IProgress<double>,
    CancellationToken)` — scarica in un file `.part`, verifica la dimensione, rinomina.
- `RoboKeep.Core/Services/InstallKind.cs` — `static bool IsSelfContained(string appDir)`:
  vero se accanto all'eseguibile c'è `coreclr.dll` (presente solo nella pubblicazione
  self-contained). Pura, testata su cartelle temporanee.
- `AppSettings` — `bool? UpdateCheck` (null = mai chiesto), `DateTime? LastUpdateCheck`,
  `string? IgnoredUpdateVersion`.
- `MainViewModel` — `UpdateBanner` (visibile, testo, avanzamento) e i comandi Novità / Scarica /
  Ignora; `CheckForUpdatesAsync(force)` chiamato all'avvio della finestra e da «Controlla ora».
- `MainWindow.xaml` — banner (`ui:InfoBar`, informativo, chiudibile) sotto la barra dei
  pulsanti; al primo avvio la domanda di consenso è un `MessageBox` Sì/No.
- `SettingsWindow` — casella e pulsante «Controlla ora» nella scheda Generale, accanto alla
  versione.
- Nessuna nuova dipendenza.

## Errori e casi limite

- Versione locale ≥ ultima release: nessun banner; «Controlla ora» dice «Sei aggiornato».
- Release senza i due asset (bozza, nomi diversi): trattata come «nessun aggiornamento».
- Download interrotto o dimensione sbagliata: file `.part` cancellato, banner con «Scaricamento
  non riuscito, riprova»; nessuna eccezione a video.
- Cartella Download non trovata: si ripiega su `%TEMP%` e lo si dice nel banner.
- Modalità portatile: identica (la scelta del pacchetto dipende solo da `coreclr.dll`).
- Risposta di GitHub con `tag_name` non parsabile: ignorata.

## Privacy e documentazione

- Una sola richiesta HTTPS a `api.github.com`; GitHub riceve l'indirizzo IP e lo User-Agent
  `RoboKeep/<versione>`, nient'altro. Nessun identificativo dell'installazione, nessun dato dei
  job. Lo scaricamento parte solo su richiesta.
- Guida: cap. 17 (privacy) riscrive la frase «nessun controllo aggiornamenti, nessun traffico di
  rete» spiegando esattamente cosa viene chiesto e a chi, e che la funzione è spenta finché non si
  dice sì; cap. 02 (installazione) descrive l'avviso e la sostituzione manuale dei file. README
  (5 lingue): una riga nella sezione privacy.
- Changelog: voce in Unreleased.

## Test

- `UpdateCheckerTests`: `Parse` su un JSON reale della release 1.7.0 (asset riconosciuti, tag →
  Version), `Parse` su JSON senza asset o con tag strano → null; `IsNewer` (uguale, maggiore,
  minore, ignorata, ignorata ma superata); `IsDue` (mai, 23 ore, 25 ore).
- `InstallKindTests`: cartella con e senza `coreclr.dll`.
- `DownloadAsync` testato con un server HTTP locale minimo (`HttpListener`) che serve un file
  di dimensione nota: dimensione giusta → file finale presente, `.part` assente; dimensione
  sbagliata → nessun file.
- La UI (banner, consenso, «Controlla ora») si prova a mano.

## Fuori scopo

Aggiornamento automatico; controllo dalla riga di comando; notifica per email; firma dei
pacchetti (resta il report VirusTotal nella pagina della release).
