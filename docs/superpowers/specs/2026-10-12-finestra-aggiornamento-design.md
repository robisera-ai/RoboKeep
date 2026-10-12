# Finestra dell'aggiornamento — design

Data: 2026-10-12. Stato: approvato a voce, in attesa di revisione del testo.

Sostituisce la parte «Avviso» di
[2026-09-23-controllo-aggiornamenti-design.md](2026-09-23-controllo-aggiornamenti-design.md);
tutto il resto di quella specifica (consenso, richiesta a GitHub, scelta del pacchetto,
indirizzi attendibili) resta valido.

## Perché

Il banner nella pagina principale si vede solo se guardi la pagina principale. Chi apre Info e
preme «Controlla ora» legge «è disponibile la 1.10.0: chiudi le Impostazioni, l'avviso è nella
finestra principale» e deve andare a cercarselo: due passaggi per una notizia di una riga.

Si aggiungono due casi visti leggendo il codice, che tengono l'utente su una versione vecchia
senza che nessuno glielo dica:

1. **Un tentativo fallito consuma le 24 ore.** La data dell'ultimo controllo si salva anche
   quando la rete manca o GitHub va in timeout (5 secondi). Un avvio senza rete blocca ogni
   nuovo tentativo per un giorno intero.
2. **Il controllo parte solo all'apertura della finestra.** RoboKeep lasciato aperto per giorni
   non ricontrolla mai.

Non è un difetto, invece, il caso che ha fatto nascere questo lavoro: la release 1.10.0 è
diventata pubblica il 12/10 alle 09:08, l'app era già stata aperta (e aveva già controllato) il
giorno prima, quindi il controllo all'avvio è stato saltato dalla cadenza di 24 ore. Il
comportamento era quello scritto.

## Cosa fa

### La finestra

Nuova finestra `UpdateWindow` (`src/RoboKeep/UpdateWindow.xaml` + `.xaml.cs`), sullo stampo di
`PreflightWindow`: `ui:FluentWindow`, `Width="520"`, `SizeToContent="Height"`,
`ResizeMode="NoResize"`, `WindowStartupLocation="CenterOwner"`, `WindowBackdropType="Mica"`,
`ExtendsContentIntoTitleBar="True"`, `ui:TitleBar` con icona `ArrowDownload24` (icona già usata
nel banner attuale: le `SymbolRegular` non le controlla il build, quindi se ne usano solo di
già viste funzionare).

Quattro stati, nello stesso spazio:

| Stato | Testo | Pulsanti |
|---|---|---|
| Disponibile | «È disponibile RoboKeep 1.10.0», «Versione in uso: 1.9.0 — pacchetto 65,5 MB», come si installa | Novità · **Scarica** (primario) · Più tardi (`IsCancel`) |
| Scaricamento | «Scarico… 43%» e barra di avanzamento | nessuno |
| Scaricato | «Scaricato in `<cartella>`» e come si installa | Chiudi |
| Non riuscito | «Scaricamento non riuscito…» | Scarica · Chiudi |

Durante il download la finestra non si chiude e non si annulla: `UpdateChecker.DownloadAsync`
non ha annullamento oggi e questa modifica non lo aggiunge. La finestra è modale, quindi per il
tempo del download non si usa il resto dell'interfaccia; i backup in corso o pianificati non
ne sono toccati, perché girano fuori dal thread dell'interfaccia.

A download riuscito la finestra apre la cartella Download in Esplora file, come fa oggi il
banner.

**Niente «Ignora questa versione».** Su uno strumento di backup una versione con correzioni non
deve poter essere messa a tacere per sempre. «Più tardi» chiude la finestra; la cadenza di 24
ore fa il resto, cioè lo ripropone il giorno dopo.

### Chi la apre

Un solo metodo, `UpdateWindow.ShowFor(Window owner, MainViewModel vm)`, chiamato da tre punti:

- **All'avvio.** `MainWindow.OnLoadedCheckUpdates`: se l'esito è `Available`, apre la finestra
  con la finestra principale come proprietaria. Se RoboKeep non è davanti all'utente — partito
  nell'area di notifica (`StartMinimized` + `MinimizeToTray`) **oppure ridotto a icona**
  (`StartMinimized` da solo) — la finestra non si apre nel vuoto: il risultato resta nel view
  model e la finestra si apre la prima volta che quella principale torna a video.
  Servono due agganci, perché le due strade sono diverse: dall'area di notifica si torna con
  `Show()` (cambia `IsVisible`), dalla barra delle applicazioni con un ripristino (cambia
  `WindowState`). **`IsVisible` da solo non basta: resta `true` anche a finestra ridotta a
  icona** — verificato alla prova, dove l'avviso compariva sopra un'app minimizzata.
- **Da Info.** `SettingsWindow.OnCheckUpdatesNow`: se l'esito è `Available`, apre la finestra
  con **la finestra Impostazioni** come proprietaria. Deve essere modale su di essa: le
  Impostazioni sono aperte con `ShowDialog()`, e una finestra non modale aperta in quel momento
  verrebbe disabilitata da Windows. Il testo di esito in Info diventa «È disponibile la 1.10.0.»
  senza più il rimando alla pagina principale.
- **A finestra aperta.** Un `DispatcherTimer` di un'ora in `MainWindow` chiama
  `CheckForUpdatesAsync(force: false)`, che rispetta sempre la cadenza (quindi nella quasi
  totalità dei giri non fa nessuna richiesta di rete). Se l'esito è `Available`, la finestra si
  apre **solo** se non c'è già una finestra modale aperta e nessuna operazione è in corso
  (`IsBusy`): non deve saltare addosso durante un backup o mentre si modifica un job. Altrimenti
  il risultato resta e si riprova al giro dopo.

### Cosa sparisce dalla pagina principale

- `MainWindow.xaml`: l'`InfoBar` dell'aggiornamento e la riga di pulsanti sotto di essa
  («Novità», «Scarica», «Ignora»). Il banner della salute dei job resta dov'è.
- `MainWindow.xaml.cs`: i gestori `OnUpdateWhatsNew`, `OnUpdateDownload`, `OnUpdateIgnore`.
- `MainViewModel`: `UpdateBannerText`, `UpdateBannerVisible`, `UpdateActionsEnabled`,
  `_updateBusy`, `SetUpdateBusy`, `IgnoreUpdate`.

`CheckForUpdatesAsync`, `OpenUpdatePage`, `LatestUpdateVersion`, la scelta del pacchetto e la
cartella Download restano come sono. Cambia la firma del download, che non scrive più testi:

```csharp
// prima: Task DownloadUpdateAsync()            — scriveva in UpdateBannerText
// dopo:  Task<(bool Ok, string Folder)> DownloadUpdateAsync(IProgress<double>? progress)
```

I testi («Scarico… 43%», «Scaricato in…», «Scaricamento non riuscito») li compone la finestra.
Il view model continua a non lanciare eccezioni al chiamante.

Serve inoltre nel view model il dato per la riga «pacchetto 65,5 MB»: una proprietà
`UpdatePackageSize` con la dimensione del pacchetto che verrebbe scaricato (quello
self-contained o quello framework-dependent, secondo `InstallKind.IsSelfContained`), già
formattata in MB.

### Lo stato salvato

`UpdateState` diventa `(DateTime? LastCheck, bool LastCheckFailed)`:

- `UpdateChecker.IsDue(DateTime? lastCheck, bool lastFailed, DateTime now)`: vero se non c'è
  data, se l'ultimo tentativo è **fallito** e sono passate **2 ore**, o se è **riuscito** e sono
  passate **24 ore**. Due costanti: `CheckInterval` (24 ore, già esistente) e `RetryInterval`
  (2 ore).
- `CheckForUpdatesAsync` salva `LastCheckFailed = true` quando `FetchWithStatusAsync` non
  restituisce informazioni (qualunque sia il motivo), `false` quando le restituisce.
- `UpdateChecker.IsNewer(Version latest, Version current)` perde il terzo parametro.
- Il campo `ignoredVersion` sparisce dal record. Un `update-state.json` scritto da una versione
  precedente si legge senza errori e quel campo viene ignorato: una versione messa a tacere
  prima torna a essere proposta, che è l'intenzione.

### Testi

Nuove chiavi in tutte e cinque le lingue (it, en, es, fr, de):

| Chiave | Italiano |
|---|---|
| `Upd_WindowTitle` | Aggiornamento disponibile |
| `Upd_Headline` | È disponibile RoboKeep {0} |
| `Upd_CurrentAndSize` | Versione in uso: {0} — pacchetto {1} |
| `Upd_HowTo` | Scarica lo zip, chiudi RoboKeep, estrailo sopra la cartella attuale e riapri. |
| `Upd_Later` | Più tardi |
| `Upd_Close` | Chiudi |

Rimosse: `Upd_BannerTitle`, `Upd_Ignore`, `Upd_Available` (sostituita da `Upd_Headline` +
`Upd_CurrentAndSize` + `Upd_HowTo`). Riscritta `Set_UpdateAvailable`: «È disponibile la {0}.»
Restano `Upd_Downloading`, `Upd_Downloaded`, `Upd_DownloadFailed`, `Upd_WhatsNew`,
`Upd_Download`, `Upd_Consent`, `Upd_ConsentTitle`.

## Errori

- **Rete assente o GitHub non raggiungibile all'avvio:** silenzio, come oggi; nessuna finestra.
  Lo stato segna il fallimento e si riprova dopo 2 ore.
- **Download non riuscito** (rete caduta, disco pieno, dimensione sbagliata): la finestra passa
  allo stato «Non riuscito» e offre di riprovare o di chiudere. `DownloadAsync` già non lascia
  file a metà.
- **Cartella Download assente:** si usa `%TEMP%`, e la finestra mostra il percorso effettivo
  (comportamento di oggi).
- **Nessuna finestra proprietaria valida** (chiusa nel frattempo): la finestra non si apre e il
  risultato resta per il giro dopo.

## Test

Automatici (`src/RoboKeep.Tests`):

- `IsDue`: senza data, dopo 1 ora da un fallimento (no), dopo 2 ore da un fallimento (sì), dopo
  3 ore da un successo (no), dopo 24 ore da un successo (sì).
- `IsNewer`: più recente sì, uguale no, più vecchia no — senza versione ignorata.
- `UpdateStateStore`: round-trip del nuovo record; lettura di un file che contiene
  `ignoredVersion` (non deve fallire né alterare gli altri campi).

A mano (la finestra WPF non si prova a unità), abbassando temporaneamente `Version` in
`src/Directory.Build.props` per far apparire la 1.10.0 come nuova:

1. Avvio normale: la finestra compare sopra la principale, «Più tardi» la chiude e non ricompare
   riaprendo l'app lo stesso giorno.
2. Info → «Controlla ora»: la finestra compare sopra le Impostazioni, non dietro, ed è usabile.
3. Avvio minimizzato nel tray: niente finestra; si apre aprendo la finestra principale.
4. «Scarica»: la barra avanza, la cartella Download si apre, lo zip c'è e la dimensione è
   quella attesa.
5. Pagina principale: nessuna traccia del vecchio banner.

## Fuori ambito

- Annullare un download in corso.
- Installare l'aggiornamento da solo (resta: estrai lo zip sopra la cartella).
- Mostrare le note di rilascio dentro la finestra: si leggono con «Novità», nel browser.
