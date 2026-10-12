# Finestra dell'aggiornamento — piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** sostituire il banner dell'aggiornamento nella pagina principale con una finestra dedicata che compare davanti a chi l'ha chiesta (avvio, Info, app lasciata aperta), togliere la versione ignorata e far riprovare il controllo dopo 2 ore quando fallisce.

**Architecture:** la parte pura (`UpdateChecker`, `UpdateState`) resta in `RoboKeep.Core` e si prova a unità. `MainViewModel` perde le proprietà del banner e restituisce dati grezzi (versione, dimensione, esito del download); i testi li compone la nuova finestra `UpdateWindow`, aperta da un unico metodo statico `ShowFor(owner, vm)` chiamato da tre punti di `MainWindow`/`SettingsWindow`.

**Tech Stack:** .NET 10, WPF, WPF-UI (`Wpf.Ui.Controls.FluentWindow`), xUnit. Specifica di riferimento: [docs/superpowers/specs/2026-10-12-finestra-aggiornamento-design.md](../specs/2026-10-12-finestra-aggiornamento-design.md).

**Nota sul build:** se RoboKeep è aperto, il build fallisce perché la DLL è bloccata. Non terminare il processo: chiedere all'utente di chiudere l'app.

**Nota sui commit:** in questo progetto non si committa finché l'utente non ha confermato che la modifica funziona. I passi «Commit» di ogni task vanno quindi preparati ma **eseguiti solo dopo** la verifica a mano del Task 6, in ordine. I messaggi usano un heredoc di bash (`git commit -F - <<'EOF'`) e non `-m "…"`: le virgolette doppie passate da PowerShell rompono il comando.

Comandi usati in tutto il piano:

```bash
dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj --nologo
dotnet build src/RoboKeep/RoboKeep.csproj --nologo
```

---

## Struttura dei file

| File | Responsabilità | Azione |
|---|---|---|
| `src/RoboKeep.Core/Services/UpdateStateStore.cs` | stato salvato: data e se l'ultimo tentativo è fallito | modifica |
| `src/RoboKeep.Core/Services/UpdateChecker.cs` | cadenza (24 h / 2 h) e confronto di versione | modifica |
| `src/RoboKeep/ViewModels/MainViewModel.cs` | controllo, dati grezzi dell'aggiornamento, download | modifica |
| `src/RoboKeep/UpdateWindow.xaml` + `.xaml.cs` | **la finestra**: testi, quattro stati, avanzamento | **nuovo** |
| `src/RoboKeep/MainWindow.xaml` | via l'`InfoBar` e la riga di pulsanti | modifica |
| `src/RoboKeep/MainWindow.xaml.cs` | apertura all'avvio, rinvio dal tray, ricontrollo orario | modifica |
| `src/RoboKeep/SettingsWindow.xaml.cs` | apertura da «Controlla ora» | modifica |
| `src/RoboKeep/Localization/Loc.cs` | chiavi nuove e vecchie, 5 lingue | modifica |
| `src/RoboKeep.Tests/UpdateCheckerTests.cs` | `IsDue`, `IsNewer` | modifica |
| `src/RoboKeep.Tests/UpdateStateStoreTests.cs` | round-trip, file di una versione precedente | modifica |
| `docs/guide/it/02-installazione.md`, `docs/guide/it/14-impostazioni.md`, `docs/guide/en/02-installation.md`, `docs/guide/en/14-settings.md` | la guida non parla più di banner né di «Ignora» | modifica |
| `CHANGELOG.md` | voce in `Unreleased` | modifica |

---

### Task 1: Lo stato — fallimento al posto della versione ignorata

**Files:**
- Modify: `src/RoboKeep.Core/Services/UpdateStateStore.cs`
- Modify: `src/RoboKeep.Core/Services/UpdateChecker.cs`
- Modify: `src/RoboKeep/ViewModels/MainViewModel.cs`
- Modify: `src/RoboKeep/MainWindow.xaml` e `src/RoboKeep/MainWindow.xaml.cs` (solo il pulsante «Ignora»)
- Test: `src/RoboKeep.Tests/UpdateCheckerTests.cs`, `src/RoboKeep.Tests/UpdateStateStoreTests.cs`

- [ ] **Step 1: Scrivere i test che devono fallire — `UpdateCheckerTests.cs`**

In `src/RoboKeep.Tests/UpdateCheckerTests.cs` sostituire il blocco `[Theory]`/`IsNewer_ComparesAndHonoursIgnored` (righe 90-98) e il `[Fact] IsDue_Every24Hours` (righe 100-107) con:

```csharp
    [Theory]
    [InlineData("1.8.0", "1.7.0", true)]
    [InlineData("1.7.0", "1.7.0", false)]
    [InlineData("1.6.9", "1.7.0", false)]
    [InlineData("1.8.1", "1.8.0", true)]
    public void IsNewer_ComparesVersions(string latest, string current, bool expected)
        => Assert.Equal(expected, UpdateChecker.IsNewer(Version.Parse(latest), Version.Parse(current)));

    [Fact]
    public void IsDue_Every24Hours_WhenTheLastCheckSucceeded()
    {
        var now = new DateTime(2026, 10, 12, 12, 0, 0);
        Assert.True(UpdateChecker.IsDue(null, false, now));
        Assert.False(UpdateChecker.IsDue(now.AddHours(-23), false, now));
        Assert.True(UpdateChecker.IsDue(now.AddHours(-25), false, now));
    }

    [Fact]
    public void IsDue_After2Hours_WhenTheLastCheckFailed()
    {
        // Un avvio senza rete non deve togliere il controllo per un giorno intero.
        var now = new DateTime(2026, 10, 12, 12, 0, 0);
        Assert.False(UpdateChecker.IsDue(now.AddHours(-1), true, now));
        Assert.True(UpdateChecker.IsDue(now.AddHours(-2), true, now));
        Assert.True(UpdateChecker.IsDue(now.AddHours(-3), true, now));
    }

    [Fact]
    public void IsDue_WhenTheClockWentBack()
    {
        // Orologio spostato indietro (o fuso cambiato): senza questo si resterebbe fermi
        // fino a quando l'ora non ha recuperato il ritardo.
        var now = new DateTime(2026, 10, 12, 12, 0, 0);
        Assert.True(UpdateChecker.IsDue(now.AddHours(5), false, now));
    }
```

- [ ] **Step 2: Scrivere i test che devono fallire — `UpdateStateStoreTests.cs`**

Sostituire l'intero contenuto dei quattro `[Fact]` di `src/RoboKeep.Tests/UpdateStateStoreTests.cs` con:

```csharp
    [Fact]
    public void SaveThenLoad_RoundTripsBothFields()
    {
        var path = TempFile();
        try
        {
            var store = new UpdateStateStore(path);
            var when = new DateTime(2026, 10, 12, 12, 30, 0);
            store.Save(new UpdateState(when, true));

            var back = new UpdateStateStore(path).Load();
            Assert.Equal(when, back.LastCheck);
            Assert.True(back.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenTheFileIsMissing()
    {
        var state = new UpdateStateStore(TempFile()).Load();
        Assert.Null(state.LastCheck);
        Assert.False(state.LastCheckFailed);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenTheFileIsCorrupt()
    {
        var path = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ questo non e' json");
            var state = new UpdateStateStore(path).Load();
            Assert.Null(state.LastCheck);
            Assert.False(state.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Load_IgnoresIgnoredVersion_FromAnOlderInstall()
    {
        // File scritto da una versione precedente: il campo non esiste piu' e va ignorato,
        // senza perdere la data e senza far fallire la lettura.
        var path = TempFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Senza fuso orario nella data: con l'offset il valore letto dipenderebbe dal fuso
            // della macchina che esegue i test.
            File.WriteAllText(path, """
            {
              "lastCheck": "2026-10-11T16:26:00",
              "ignoredVersion": "1.10.0"
            }
            """);
            var state = new UpdateStateStore(path).Load();
            Assert.Equal(new DateTime(2026, 10, 11, 16, 26, 0), state.LastCheck);
            Assert.False(state.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Save_CreatesTheFolder_AndOverwritesThePreviousState()
    {
        var path = TempFile();
        try
        {
            var store = new UpdateStateStore(path);
            store.Save(new UpdateState(new DateTime(2026, 1, 1), true));
            store.Save(new UpdateState(new DateTime(2026, 2, 2), false));

            var back = store.Load();
            Assert.Equal(new DateTime(2026, 2, 2), back.LastCheck);
            Assert.False(back.LastCheckFailed);
        }
        finally { Cleanup(path); }
    }
```

- [ ] **Step 3: Verificare che i test non compilino/falliscano**

Run: `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj --nologo --filter "FullyQualifiedName~Update"`
Expected: errore di compilazione — `UpdateState` non ha un costruttore `(DateTime, bool)`, `IsDue` non accetta tre argomenti, `IsNewer` non accetta due.

- [ ] **Step 4: `UpdateState` — il campo nuovo**

In `src/RoboKeep.Core/Services/UpdateStateStore.cs` sostituire il record e il campo `Empty`:

```csharp
/// <summary>Stato del controllo aggiornamenti.</summary>
/// <param name="LastCheck">Ultimo tentativo eseguito.</param>
/// <param name="LastCheckFailed">true se l'ultimo tentativo non ha portato informazioni (rete
/// assente, timeout, 404): si riprova dopo <see cref="UpdateChecker.RetryInterval"/> invece di
/// aspettare le 24 ore piene, altrimenti un avvio senza rete toglierebbe il controllo per un
/// giorno. Un file scritto da una versione precedente contiene <c>ignoredVersion</c> al posto
/// di questo campo: viene ignorato, e la versione messa a tacere torna a essere proposta.</param>
public sealed record UpdateState(DateTime? LastCheck, bool LastCheckFailed);
```

e, dentro la classe `UpdateStateStore`:

```csharp
    private static readonly UpdateState Empty = new(null, false);
```

- [ ] **Step 5: `UpdateChecker` — cadenza e confronto**

In `src/RoboKeep.Core/Services/UpdateChecker.cs`, accanto a `CheckInterval`, aggiungere:

```csharp
    /// <summary>Attesa dopo un tentativo che non ha portato informazioni. Molto piu' breve delle
    /// 24 ore: un avvio senza rete non deve impedire ogni controllo per un giorno intero.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromHours(2);
```

Sostituire `IsNewer` e `IsDue`:

```csharp
    /// <summary>true se la release e' piu' recente di quella in uso. Non esiste piu' una versione
    /// "ignorata": su uno strumento di backup una versione con correzioni non deve poter essere
    /// messa a tacere per sempre; «Più tardi» chiude l'avviso e la cadenza lo ripropone.</summary>
    public static bool IsNewer(Version latest, Version current) => latest > current;

    /// <summary>Se e' ora di ricontrollare: 24 ore dopo un tentativo riuscito, 2 dopo uno
    /// fallito.</summary>
    public static bool IsDue(DateTime? lastCheck, bool lastCheckFailed, DateTime now)
    {
        if (lastCheck is null) return true;
        // Orologio spostato indietro o fuso cambiato: senza questo si resterebbe fermi finche'
        // l'ora non ha recuperato il ritardo.
        if (now < lastCheck.Value) return true;
        return now - lastCheck.Value >= (lastCheckFailed ? RetryInterval : CheckInterval);
    }
```

- [ ] **Step 6: Allineare `MainViewModel` (solo quanto serve a compilare)**

In `src/RoboKeep/ViewModels/MainViewModel.cs`, dentro `CheckForUpdatesAsync`, sostituire le prime righe e il salvataggio:

```csharp
        var state = _host.UpdateState.Load();
        if (!force && (_host.Config.Settings.UpdateCheck != true
                       || !UpdateChecker.IsDue(state.LastCheck, state.LastCheckFailed, DateTime.Now)))
            return UpdateCheckOutcome.Skipped;

        var (info, status) = await UpdateChecker.FetchWithStatusAsync();
        // La data si aggiorna anche quando il controllo non riesce, ma segnata come fallita: si
        // riprova fra RetryInterval invece di restare fermi 24 ore.
        _host.UpdateState.Save(new UpdateState(DateTime.Now, info is null));
```

e la riga del confronto:

```csharp
        if (!UpdateChecker.IsNewer(info.Latest, UpdateChecker.Current))
```

Cancellare per intero il metodo `IgnoreUpdate()`.

- [ ] **Step 7: Togliere il pulsante «Ignora»**

In `src/RoboKeep/MainWindow.xaml` cancellare la riga:

```xml
                    <ui:Button Content="{l:Tr Upd_Ignore}" Click="OnUpdateIgnore"/>
```

In `src/RoboKeep/MainWindow.xaml.cs` cancellare la riga:

```csharp
    private void OnUpdateIgnore(object sender, RoutedEventArgs e) => _vm.IgnoreUpdate();
```

- [ ] **Step 8: Eseguire i test**

Run: `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj --nologo`
Expected: PASS, tutti. Se `LocParityTests` fallisce, `Upd_Ignore` è ancora in uso da qualche parte: cercarlo con `grep -rn "Upd_Ignore" src/`.

- [ ] **Step 9: Commit**

```bash
git add src/RoboKeep.Core/Services/UpdateStateStore.cs src/RoboKeep.Core/Services/UpdateChecker.cs src/RoboKeep/ViewModels/MainViewModel.cs src/RoboKeep/MainWindow.xaml src/RoboKeep/MainWindow.xaml.cs src/RoboKeep.Tests/UpdateCheckerTests.cs src/RoboKeep.Tests/UpdateStateStoreTests.cs
git commit -F - <<'EOF'
fix: un controllo aggiornamenti fallito si riprova dopo 2 ore

La data dell'ultimo controllo si salvava anche quando la rete mancava o GitHub
andava in timeout, e bloccava ogni nuovo tentativo per 24 ore piene. Ora lo
stato ricorda se il tentativo è fallito e in quel caso si riprova dopo 2 ore.

Via anche la versione ignorata: su uno strumento di backup una versione con
correzioni non deve poter essere messa a tacere per sempre.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
```

---

### Task 2: Il view model senza banner

**Files:**
- Modify: `src/RoboKeep/ViewModels/MainViewModel.cs:126-141` e il blocco del download
- Modify: `src/RoboKeep/MainWindow.xaml:113-124`
- Modify: `src/RoboKeep/MainWindow.xaml.cs:523-527`

Nessun test automatico: `MainViewModel` dipende da `AppHost` (file su disco) e il download fa rete, quindi la verifica è il build più la prova a mano del Task 6.

- [ ] **Step 1: Le proprietà del banner diventano dati grezzi**

In `src/RoboKeep/ViewModels/MainViewModel.cs` sostituire il blocco che va da `private UpdateInfo? _update;` fino alla riga di `LatestUpdateVersion` compresa con:

```csharp
    private UpdateInfo? _update;

    /// <summary>Versione nuova trovata dall'ultimo controllo (vuota se nessuna): la mostrano le
    /// Impostazioni e la finestra dell'aggiornamento.</summary>
    public string LatestUpdateVersion => _update?.Latest.ToString() ?? "";

    /// <summary>Dimensione del pacchetto che verrebbe scaricato su questa installazione, già
    /// scritta per essere letta ("65,5 MB"); vuota se non c'e' nessuna versione nuova. Si mostra
    /// una dimensione sola: quella dell'altro pacchetto non riguarda chi legge.</summary>
    public string UpdatePackageSize
    {
        get
        {
            if (_update is null) return "";
            var size = InstallKind.IsSelfContained(AppContext.BaseDirectory)
                ? _update.SelfContainedSize
                : _update.FrameworkDependentSize;
            return $"{size / 1024d / 1024d:0.#} MB";
        }
    }
```

- [ ] **Step 2: `CheckForUpdatesAsync` non scrive più testi**

Sostituire la parte finale del metodo (da `if (!UpdateChecker.IsNewer(...)` alla fine) con:

```csharp
        if (!UpdateChecker.IsNewer(info.Latest, UpdateChecker.Current))
        {
            _update = null;
            RaiseUpdateChanged();
            return UpdateCheckOutcome.UpToDate;
        }
        _update = info;
        RaiseUpdateChanged();
        return UpdateCheckOutcome.Available;
    }

    private void RaiseUpdateChanged()
    {
        OnPropertyChanged(nameof(LatestUpdateVersion));
        OnPropertyChanged(nameof(UpdatePackageSize));
    }
```

Aggiornare anche il commento XML del metodo, che parla di "versione ignorata":

```csharp
    /// <summary>Controlla se esiste una versione nuova. <paramref name="force"/> ignora la
    /// cadenza (pulsante "Controlla ora"). La data e l'esito dell'ultimo tentativo stanno in
    /// update-state.json: qui la configurazione non si salva mai, perche' le Impostazioni
    /// possono essere aperte con modifiche non confermate.</summary>
```

- [ ] **Step 3: Il download restituisce l'esito invece di scriverlo**

Sostituire `DownloadUpdateAsync` e cancellare `SetUpdateBusy`:

```csharp
    /// <summary>Scarica il pacchetto giusto per questa installazione nella cartella Download e
    /// la apre in Esplora file. Restituisce l'esito e la cartella: i testi li compone la
    /// finestra dell'aggiornamento. Non lancia eccezioni.</summary>
    public async Task<(bool Ok, string Folder)> DownloadUpdateAsync(IProgress<double>? progress)
    {
        if (_update is null) return (false, "");
        var selfContained = InstallKind.IsSelfContained(AppContext.BaseDirectory);
        var url = selfContained ? _update.SelfContainedUrl : _update.FrameworkDependentUrl;
        var size = selfContained ? _update.SelfContainedSize : _update.FrameworkDependentSize;
        var folder = DownloadsFolder();
        var target = Path.Combine(folder, InstallKind.AssetName(_update.Latest, selfContained));

        // Nessun annullamento: la finestra non ha un pulsante per fermarlo e DownloadAsync
        // gestisce da se' gli errori (niente file a meta').
        var ok = await UpdateChecker.DownloadAsync(url, target, size, progress, CancellationToken.None);
        if (ok) RevealInExplorer(target);
        return (ok, folder);
    }
```

- [ ] **Step 4: Via l'avviso dalla pagina principale**

In `src/RoboKeep/MainWindow.xaml` cancellare il commento, l'`ui:InfoBar` dell'aggiornamento e lo `StackPanel` dei pulsanti che lo segue — cioè tutto da `<!-- Aggiornamento disponibile:` fino a `</StackPanel>` incluso, lasciando intatto l'`InfoBar` della salute dei job e lo `StackPanel` esterno.

In `src/RoboKeep/MainWindow.xaml.cs` cancellare:

```csharp
    private void OnUpdateWhatsNew(object sender, RoutedEventArgs e) => _vm.OpenUpdatePage();
    private async void OnUpdateDownload(object sender, RoutedEventArgs e)
    {
        try { await _vm.DownloadUpdateAsync(); } catch { /* il view model gia' gestisce gli errori */ }
    }
```

- [ ] **Step 5: Build e test**

Run: `dotnet build src/RoboKeep/RoboKeep.csproj --nologo`
Expected: build riuscito. Se compare `CS0103` su `UpdateBannerText`/`UpdateActionsEnabled`, è rimasto un riferimento nel XAML: cercarlo con `grep -rn "UpdateBanner\|UpdateActionsEnabled" src/RoboKeep/`.

Run: `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj --nologo`
Expected: PASS, tutti.

In questo momento l'app non avvisa più di niente: la finestra arriva col Task 3. È uno stato intermedio, non una versione da pubblicare.

- [ ] **Step 6: Commit**

```bash
git add src/RoboKeep/ViewModels/MainViewModel.cs src/RoboKeep/MainWindow.xaml src/RoboKeep/MainWindow.xaml.cs
git commit -F - <<'EOF'
refactor: il view model degli aggiornamenti non compone più i testi

Prepara la finestra dedicata: via le proprietà del banner, il download
restituisce esito e cartella, e una proprietà nuova dice la dimensione del
solo pacchetto che verrebbe scaricato su questa installazione.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
```

---

### Task 3: I testi in cinque lingue

**Files:**
- Modify: `src/RoboKeep/Localization/Loc.cs`
- Test: `src/RoboKeep.Tests/LocParityTests.cs` (esistente, non va modificato: verifica da solo che le cinque lingue abbiano le stesse chiavi)

- [ ] **Step 1: Italiano**

In `src/RoboKeep/Localization/Loc.cs`, nel blocco `it`, sostituire le righe `Upd_BannerTitle`, `Upd_Available` e `Upd_Ignore` con:

```csharp
        ["Upd_WindowTitle"] = "Aggiornamento disponibile",
        ["Upd_Headline"] = "È disponibile RoboKeep {0}",
        ["Upd_CurrentAndSize"] = "Versione in uso: {0} — pacchetto {1}",
        ["Upd_HowTo"] = "Scarica lo zip, chiudi RoboKeep, estrailo sopra la cartella attuale e riapri.",
        ["Upd_Later"] = "Più tardi",
        ["Upd_Close"] = "Chiudi",
```

e `Set_UpdateAvailable` con:

```csharp
        ["Set_UpdateAvailable"] = "È disponibile la {0}.",
```

- [ ] **Step 2: English**

Nel blocco `en`, stesse sostituzioni:

```csharp
        ["Upd_WindowTitle"] = "Update available",
        ["Upd_Headline"] = "RoboKeep {0} is available",
        ["Upd_CurrentAndSize"] = "Version in use: {0} — package {1}",
        ["Upd_HowTo"] = "Download the zip, close RoboKeep, extract it over the current folder and reopen.",
        ["Upd_Later"] = "Later",
        ["Upd_Close"] = "Close",
```

```csharp
        ["Set_UpdateAvailable"] = "Version {0} is available.",
```

- [ ] **Step 3: Español**

```csharp
        ["Upd_WindowTitle"] = "Actualización disponible",
        ["Upd_Headline"] = "RoboKeep {0} está disponible",
        ["Upd_CurrentAndSize"] = "Versión en uso: {0} — paquete {1}",
        ["Upd_HowTo"] = "Descarga el zip, cierra RoboKeep, extráelo sobre la carpeta actual y vuelve a abrir.",
        ["Upd_Later"] = "Más tarde",
        ["Upd_Close"] = "Cerrar",
```

```csharp
        ["Set_UpdateAvailable"] = "Está disponible la {0}.",
```

- [ ] **Step 4: Français**

```csharp
        ["Upd_WindowTitle"] = "Mise à jour disponible",
        ["Upd_Headline"] = "RoboKeep {0} est disponible",
        ["Upd_CurrentAndSize"] = "Version utilisée : {0} — paquet {1}",
        ["Upd_HowTo"] = "Téléchargez le zip, fermez RoboKeep, extrayez-le sur le dossier actuel et rouvrez.",
        ["Upd_Later"] = "Plus tard",
        ["Upd_Close"] = "Fermer",
```

```csharp
        ["Set_UpdateAvailable"] = "La {0} est disponible.",
```

- [ ] **Step 5: Deutsch**

```csharp
        ["Upd_WindowTitle"] = "Update verfügbar",
        ["Upd_Headline"] = "RoboKeep {0} ist verfügbar",
        ["Upd_CurrentAndSize"] = "Verwendete Version: {0} — Paket {1}",
        ["Upd_HowTo"] = "Laden Sie die Zip-Datei herunter, schließen Sie RoboKeep, entpacken Sie sie über den aktuellen Ordner und öffnen Sie es erneut.",
        ["Upd_Later"] = "Später",
        ["Upd_Close"] = "Schließen",
```

```csharp
        ["Set_UpdateAvailable"] = "Version {0} ist verfügbar.",
```

- [ ] **Step 6: Eseguire i test di parità**

Run: `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj --nologo --filter "FullyQualifiedName~LocParityTests"`
Expected: PASS. Un fallimento elenca le chiavi mancanti o in eccesso per lingua: aggiungere o togliere quelle indicate.

- [ ] **Step 7: Commit**

```bash
git add src/RoboKeep/Localization/Loc.cs
git commit -F - <<'EOF'
i18n: testi della finestra dell'aggiornamento in cinque lingue

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
```

---

### Task 4: La finestra `UpdateWindow`

**Files:**
- Create: `src/RoboKeep/UpdateWindow.xaml`
- Create: `src/RoboKeep/UpdateWindow.xaml.cs`

Due trappole da non ripetere:

- Le icone WPF-UI non le controlla il build: una `SymbolRegular` inesistente fa cadere la finestra a runtime. `ArrowDownload24` e `Open24` sono quelle del vecchio banner, quindi già viste funzionare.
- La barra si chiama `DownloadProgress`, non `Progress`: un campo di nome `Progress` nasconderebbe il tipo `Progress<double>` usato nello stesso metodo e il compilatore darebbe `CS0118`.

- [ ] **Step 1: Creare `src/RoboKeep/UpdateWindow.xaml`**

```xml
<ui:FluentWindow x:Class="RoboKeep.UpdateWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        xmlns:l="clr-namespace:RoboKeep.Localization"
        Title="{l:Tr Upd_WindowTitle}" Width="520" SizeToContent="Height"
        ResizeMode="NoResize" ExtendsContentIntoTitleBar="True" WindowBackdropType="Mica"
        WindowCornerPreference="Round" WindowStartupLocation="CenterOwner">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <ui:TitleBar Grid.Row="0" Title="{l:Tr Upd_WindowTitle}">
            <ui:TitleBar.Icon><ui:SymbolIcon Symbol="ArrowDownload24"/></ui:TitleBar.Icon>
        </ui:TitleBar>

        <StackPanel Grid.Row="1" Margin="20,8,20,0">
            <TextBlock x:Name="Headline" FontSize="18" FontWeight="SemiBold" TextWrapping="Wrap"/>
            <TextBlock x:Name="Detail" Margin="0,4,0,0" Opacity="0.75" TextWrapping="Wrap"/>
            <TextBlock x:Name="Body" Margin="0,12,0,0" TextWrapping="Wrap"/>
            <ProgressBar x:Name="DownloadProgress" Height="6" Margin="0,12,0,0"
                         Minimum="0" Maximum="100" Visibility="Collapsed"/>
        </StackPanel>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right"
                    Margin="20,16,20,20">
            <ui:Button x:Name="WhatsNewButton" Content="{l:Tr Upd_WhatsNew}"
                       Icon="{ui:SymbolIcon Open24}" Margin="0,0,8,0" Click="OnWhatsNew"/>
            <ui:Button x:Name="DownloadButton" Content="{l:Tr Upd_Download}"
                       Icon="{ui:SymbolIcon ArrowDownload24}" Appearance="Primary"
                       Margin="0,0,8,0" Click="OnDownload"/>
            <ui:Button x:Name="CloseButton" Content="{l:Tr Upd_Later}" IsCancel="True"
                       Click="OnClose"/>
        </StackPanel>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 2: Creare `src/RoboKeep/UpdateWindow.xaml.cs`**

```csharp
using System.ComponentModel;
using System.Windows;
using RoboKeep.Core.Services;
using RoboKeep.Localization;
using RoboKeep.ViewModels;

namespace RoboKeep;

/// <summary>
/// L'avviso di una versione nuova. Prima era un banner nella pagina principale: chi premeva
/// «Controlla ora» nella scheda Info doveva chiudere le Impostazioni per andare a leggerlo.
/// Qui l'avviso arriva davanti a chi l'ha chiesto, compreso l'avvio.
///
/// Modale per necessita': le Impostazioni sono aperte con ShowDialog(), e una finestra non
/// modale aperta da li' verrebbe disabilitata da Windows. Un backup in corso non ne soffre,
/// perche' gira fuori dal thread dell'interfaccia.
/// </summary>
public partial class UpdateWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _vm;
    private bool _downloading;

    private UpdateWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Headline.Text = string.Format(Loc.Instance["Upd_Headline"], vm.LatestUpdateVersion);
        Detail.Text = string.Format(Loc.Instance["Upd_CurrentAndSize"],
            UpdateChecker.Current, vm.UpdatePackageSize);
        Body.Text = Loc.Instance["Upd_HowTo"];
    }

    /// <summary>Apre l'avviso davanti a <paramref name="owner"/>. Non fa niente se non c'e'
    /// nessuna versione nuova, o se la finestra proprietaria non e' a video: l'avviso si
    /// centrerebbe sul nulla (app partita nell'area di notifica).</summary>
    public static void ShowFor(Window owner, MainViewModel vm)
    {
        if (string.IsNullOrEmpty(vm.LatestUpdateVersion)) return;
        if (!owner.IsVisible) return;
        new UpdateWindow(vm) { Owner = owner }.ShowDialog();
    }

    private void OnWhatsNew(object sender, RoutedEventArgs e) => _vm.OpenUpdatePage();

    private async void OnDownload(object sender, RoutedEventArgs e)
    {
        if (_downloading) return;
        _downloading = true;
        // Mentre scarica non c'e' niente da premere: non si annulla (DownloadAsync non lo
        // prevede) e la finestra non si chiude, altrimenti resterebbe un .part senza nessuno
        // che lo finisca.
        WhatsNewButton.Visibility = Visibility.Collapsed;
        DownloadButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Collapsed;
        DownloadProgress.Visibility = Visibility.Visible;
        Body.Text = string.Format(Loc.Instance["Upd_Downloading"], 0);

        var progress = new Progress<double>(p =>
        {
            DownloadProgress.Value = p * 100;
            Body.Text = string.Format(Loc.Instance["Upd_Downloading"], (int)(p * 100));
        });
        // Il view model gestisce da se' gli errori: qui arriva solo l'esito.
        var (ok, folder) = await _vm.DownloadUpdateAsync(progress);

        _downloading = false;
        DownloadProgress.Visibility = Visibility.Collapsed;
        Body.Text = ok
            ? string.Format(Loc.Instance["Upd_Downloaded"], folder)
            : Loc.Instance["Upd_DownloadFailed"];
        // Non riuscito: si puo' riprovare. Riuscito: non c'e' altro da fare qui.
        DownloadButton.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        CloseButton.Content = Loc.Instance["Upd_Close"];
        CloseButton.Visibility = Visibility.Visible;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // La X della barra del titolo e Esc non devono interrompere uno scaricamento in corso.
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_downloading) e.Cancel = true;
        base.OnClosing(e);
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/RoboKeep/RoboKeep.csproj --nologo`
Expected: build riuscito. La finestra non è ancora aperta da nessuno: il Task 5 la collega.

- [ ] **Step 4: Commit**

```bash
git add src/RoboKeep/UpdateWindow.xaml src/RoboKeep/UpdateWindow.xaml.cs
git commit -F - <<'EOF'
feat: finestra dedicata per l'avviso di una versione nuova

Quattro stati nello stesso spazio: disponibile, scaricamento con barra,
scaricato, non riuscito con la possibilità di riprovare. Modale perché le
Impostazioni sono aperte con ShowDialog e una finestra non modale aperta da
lì verrebbe disabilitata da Windows.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
```

---

### Task 5: I tre punti di apertura

**Files:**
- Modify: `src/RoboKeep/MainWindow.xaml.cs` (campi, `OnLoadedCheckUpdates`, metodi nuovi)
- Modify: `src/RoboKeep/SettingsWindow.xaml.cs:47-64`

- [ ] **Step 1: I campi nuovi in `MainWindow`**

In `src/RoboKeep/MainWindow.xaml.cs`, accanto a `private System.Windows.Threading.DispatcherTimer? _deviceRefresh;`, aggiungere:

```csharp
    // Avviso di aggiornamento rimandato: l'app puo' essere partita nell'area di notifica, e
    // la finestra si centrerebbe su una finestra che non si vede.
    private bool _updatePostponed;
    // Ricontrollo a finestra aperta: senza di questo RoboKeep lasciato aperto per giorni non
    // ricontrollerebbe mai (il controllo parte solo all'apertura della finestra).
    private System.Windows.Threading.DispatcherTimer? _updateRecheck;
```

- [ ] **Step 2: Apertura all'avvio e avvio del ricontrollo periodico**

Sostituire la parte finale di `OnLoadedCheckUpdates` (il `try`/`catch`) con:

```csharp
        try
        {
            if (await _vm.CheckForUpdatesAsync(force: false) == MainViewModel.UpdateCheckOutcome.Available)
                ShowOrPostponeUpdateWindow();
        }
        catch { /* best-effort: nessun aggiornamento noto */ }

        // La cadenza la decide CheckForUpdatesAsync, quindi quasi tutti i giri non fanno rete.
        _updateRecheck = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromHours(1),
        };
        _updateRecheck.Tick += OnUpdateRecheckTick;
        _updateRecheck.Start();
```

- [ ] **Step 3: I metodi nuovi di `MainWindow`**

Subito dopo `OnLoadedCheckUpdates`, aggiungere:

```csharp
    // L'avviso va mostrato a finestra visibile. Partiti nell'area di notifica si aspetta la
    // prima apertura: un avviso aperto su una finestra nascosta comparirebbe senza contesto.
    private void ShowOrPostponeUpdateWindow()
    {
        if (IsVisible)
        {
            UpdateWindow.ShowFor(this, _vm);
            return;
        }
        if (_updatePostponed) return;
        _updatePostponed = true;
        IsVisibleChanged += OnVisibleShowUpdate;
    }

    private void OnVisibleShowUpdate(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible) return;
        IsVisibleChanged -= OnVisibleShowUpdate;
        _updatePostponed = false;
        // La finestra appena mostrata non ha ancora finito di disporsi: l'avviso si apre al
        // giro successivo del dispatcher, altrimenti si centra su misure non ancora valide.
        Dispatcher.BeginInvoke(new System.Action(() => UpdateWindow.ShowFor(this, _vm)));
    }

    // Ricontrollo ogni ora a finestra aperta. L'avviso non salta addosso durante un'operazione
    // o sopra un'altra finestra modale: in quel caso si riprova al giro dopo.
    private async void OnUpdateRecheckTick(object? sender, System.EventArgs e)
    {
        if (_vm.IsBusy || System.Windows.Interop.ComponentDispatcher.IsThreadModal) return;
        try
        {
            if (await _vm.CheckForUpdatesAsync(force: false) == MainViewModel.UpdateCheckOutcome.Available
                && !_vm.IsBusy && !System.Windows.Interop.ComponentDispatcher.IsThreadModal)
                ShowOrPostponeUpdateWindow();
        }
        catch { /* best-effort: nessun aggiornamento noto */ }
    }
```

- [ ] **Step 4: Apertura da «Controlla ora»**

In `src/RoboKeep/SettingsWindow.xaml.cs` sostituire `OnCheckUpdatesNow` con:

```csharp
    // "Controlla ora": controllo immediato, saltando la cadenza. L'avviso si apre sopra le
    // Impostazioni, non dietro: prima bisognava chiuderle per leggere il banner nella pagina
    // principale.
    private async void OnCheckUpdatesNow(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        var outcome = MainViewModel.UpdateCheckOutcome.Skipped;
        try
        {
            outcome = await _main.CheckForUpdatesAsync(force: true);
            UpdateStatusText.Text = outcome switch
            {
                MainViewModel.UpdateCheckOutcome.Available => string.Format(Loc.Instance["Set_UpdateAvailable"], _main.LatestUpdateVersion),
                MainViewModel.UpdateCheckOutcome.UpToDate => Loc.Instance["Set_UpdateUpToDate"],
                MainViewModel.UpdateCheckOutcome.NotPublic => Loc.Instance["Set_UpdateNotPublic"],
                _ => Loc.Instance["Set_UpdateFailed"],
            };
        }
        // async void: un'eccezione non gestita qui farebbe cadere l'applicazione.
        catch { UpdateStatusText.Text = Loc.Instance["Set_UpdateFailed"]; }
        // Il pulsante si riattiva prima di aprire l'avviso: la finestra e' modale e lo
        // lascerebbe spento per tutto il tempo che resta aperta.
        finally { CheckUpdatesButton.IsEnabled = true; }

        if (outcome == MainViewModel.UpdateCheckOutcome.Available)
            UpdateWindow.ShowFor(this, _main);
    }
```

- [ ] **Step 5: Build e test**

Run: `dotnet build src/RoboKeep/RoboKeep.csproj --nologo`
Expected: build riuscito.

Run: `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj --nologo`
Expected: PASS, tutti.

- [ ] **Step 6: Commit**

```bash
git add src/RoboKeep/MainWindow.xaml.cs src/RoboKeep/SettingsWindow.xaml.cs
git commit -F - <<'EOF'
feat: l'avviso di aggiornamento arriva davanti a chi l'ha chiesto

Tre punti di apertura: l'avvio, il pulsante «Controlla ora» nella scheda Info
(dove l'avviso ora compare sopra le Impostazioni invece di restare dietro) e
un ricontrollo ogni ora a finestra aperta, che prima non esisteva: il
controllo partiva solo all'apertura della finestra.

Partendo nell'area di notifica l'avviso si rimanda alla prima apertura della
finestra, e non interrompe un'operazione in corso.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
```

---

### Task 6: Verifica a mano

**Files:** nessuna modifica permanente. `src/Directory.Build.props:6` si tocca e si rimette.

- [ ] **Step 1: Far apparire la 1.10.0 come versione nuova**

In `src/Directory.Build.props` cambiare `<Version>1.10.0</Version>` in `<Version>1.9.0</Version>`.

Cancellare `%APPDATA%\RoboKeep\update-state.json` per far partire il controllo subito:

```bash
rm -f "$APPDATA/RoboKeep/update-state.json"
```

Run: `dotnet build src/RoboKeep/RoboKeep.csproj --nologo`
Expected: build riuscito. Se fallisce con un errore di file bloccato, RoboKeep è aperto: chiedere all'utente di chiuderlo.

- [ ] **Step 2: Avvio normale**

Avviare l'app. Expected: la finestra «Aggiornamento disponibile» compare centrata sulla finestra principale, con «È disponibile RoboKeep 1.10.0», «Versione in uso: 1.9.0 — pacchetto … MB» e i tre pulsanti. Nella pagina principale non c'è nessun banner di aggiornamento (quello della salute dei job, se c'è, resta).

Premere «Più tardi», chiudere e riaprire l'app. Expected: la finestra non ricompare (cadenza di 24 ore).

- [ ] **Step 3: «Controlla ora» da Info**

Impostazioni → Info → «Controlla ora». Expected: la finestra compare **sopra** le Impostazioni ed è usabile; sotto il pulsante c'è «È disponibile la 1.10.0.» senza rimandi alla pagina principale. Chiudendola si torna alle Impostazioni, col pulsante attivo.

- [ ] **Step 4: Avvio nell'area di notifica**

In Impostazioni attivare «Avvia minimizzato» e «Riduci a icona nell'area di notifica», chiudere l'app, cancellare `update-state.json`, riaprirla. Expected: nessuna finestra compare, nessun lampo sullo schermo. Aprendo la finestra principale dall'icona nell'area di notifica, l'avviso si apre subito dopo. Rimettere poi le due impostazioni come stavano.

- [ ] **Step 5: Download**

Cancellare `update-state.json`, riavviare, premere «Scarica». Expected: i pulsanti spariscono, la barra avanza con la percentuale, la X della barra del titolo e Esc non chiudono la finestra. A fine download: «Scaricato in C:\Users\…\Downloads», la cartella si apre in Esplora file, lo zip c'è con la dimensione mostrata nella finestra, e resta solo «Chiudi».

- [ ] **Step 6: Rimettere la versione**

In `src/Directory.Build.props` rimettere `<Version>1.10.0</Version>`.

Run: `git diff --stat src/Directory.Build.props`
Expected: nessuna differenza.

---

### Task 7: Guida e changelog

**Files:**
- Modify: `docs/guide/it/02-installazione.md:52-57`
- Modify: `docs/guide/it/14-impostazioni.md:57,59`
- Modify: `docs/guide/en/02-installation.md:51-56`
- Modify: `docs/guide/en/14-settings.md:57,59`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Guida italiana — capitolo Aggiornare**

In `docs/guide/it/02-installazione.md` sostituire il paragrafo che inizia con «Quando esiste una versione nuova» con:

```markdown
Quando esiste una versione nuova (se hai attivato il controllo), RoboKeep apre una finestra che
lo dice, con **Novità**, **Scarica** e **Più tardi**. Scarica prende il pacchetto giusto
per la tua installazione e lo mette in Download, mostrando l'avanzamento nella finestra stessa.
Poi fai tu: chiudi RoboKeep, estrai lo zip **sopra** la cartella attuale (sovrascrivendo),
riapri. Impostazioni, job, cronologia e log stanno in `%APPDATA%\RoboKeep` e non vengono
toccati. Se un'attività pianificata punta a quella cartella, dopo la sostituzione usa la
versione nuova da sola.

Con **Più tardi** la finestra si chiude e la versione ti viene riproposta il giorno dopo: non
c'è un modo per mettere a tacere una versione per sempre, perché gli aggiornamenti di un
programma di backup contengono correzioni che è meglio non perdere.
```

- [ ] **Step 2: Guida italiana — capitolo Impostazioni**

In `docs/guide/it/14-impostazioni.md` sostituire la riga 57 con:

```markdown
La casella **«Cerca aggiornamenti automaticamente»** decide se RoboKeep controlla da solo l'esistenza di una versione nuova: una richiesta a `api.github.com` all'avvio e poi, se lasci l'app aperta, al massimo una volta al giorno. Se un tentativo non riesce (rete assente, GitHub non raggiungibile) riprova dopo due ore invece di aspettare il giorno dopo. Se trova una versione più recente, si apre una finestra con **Novità**, **Scarica** e **Più tardi**. Spenta, RoboKeep non contatta mai GitHub di sua iniziativa.
```

e la riga 59 con:

```markdown
Nella scheda **Info** trovi anche il pulsante **«Controlla ora»**, che fa la verifica subito — funziona anche a casella spenta, perché è un'azione esplicita che decidi tu lì per lì. Il risultato compare sotto il pulsante: *sei aggiornato*, oppure *è disponibile la versione X* (e la finestra dell'aggiornamento si apre sopra le Impostazioni), oppure *controllo non riuscito* se manca la rete o GitHub non è raggiungibile.
```

- [ ] **Step 3: Guida inglese — capitolo Updating**

In `docs/guide/en/02-installation.md` sostituire il paragrafo che inizia con «When a new version exists» con:

```markdown
When a new version exists (if you turned the check on), RoboKeep opens a window that says so,
with **What's new**, **Download** and **Later**. Download grabs the right package for your
install and puts it in Downloads, showing the progress in the window itself. Then it's up to
you: close RoboKeep, extract the zip **over** the current folder (overwriting it), reopen.
Settings, jobs, history and logs live in `%APPDATA%\RoboKeep` and are left untouched. If a
scheduled task points at that folder, it picks up the new version on its own after the
replacement.

**Later** closes the window and the version is offered again the next day: there is no way to
silence a version for good, because the updates of a backup program carry fixes you had better
not miss.
```

- [ ] **Step 4: Guida inglese — capitolo Settings**

In `docs/guide/en/14-settings.md` sostituire la riga 57 con:

```markdown
The **"Check for updates automatically"** checkbox decides whether RoboKeep checks on its own for a newer version: one request to `api.github.com` at startup and then, if you leave the app open, at most once a day. If an attempt fails (no network, GitHub unreachable) it retries after two hours instead of waiting for the next day. If it finds a newer version, a window opens with **What's new**, **Download** and **Later**. Off, RoboKeep never contacts GitHub on its own.
```

e la riga 59 con:

```markdown
The **About** tab also has a **"Check now"** button, which checks right away — it works even with the checkbox off, because it's an explicit action you decide in the moment. The result appears under the button: *you are up to date*, or *version X is available* (and the update window opens on top of Settings), or *check failed* if there's no network or GitHub can't be reached.
```

- [ ] **Step 5: Changelog**

In `CHANGELOG.md`, subito dopo il paragrafo introduttivo e prima di `## [1.10.0]`, inserire:

```markdown
## [Unreleased]

### Changed
- **The update notice is now a window of its own.** It used to be a banner in the main window:
  whoever pressed "Check now" in the About tab had to close Settings to go and read it. The
  window now opens in front of whoever asked for it, at startup or from Settings, with
  "What's new", "Download" (with the progress bar inside the window) and "Later".
- **"Ignore this version" is gone.** A backup program's updates carry fixes that had better not
  be silenced for good; "Later" closes the notice and the version is offered again the next day.

### Fixed
- **A failed update check no longer blocks the next one for 24 hours.** The date of the last
  check was saved even when the network was missing or GitHub timed out, so an offline startup
  took the check away for a full day. A failed attempt is now retried after two hours.
- **RoboKeep left open for days now checks for updates again.** The check only ran when the
  window opened; it now also runs while the window stays open, with the same once-a-day cadence,
  and the notice never interrupts a running operation.
```

- [ ] **Step 6: Verificare che la guida nell'app non si sia rotta**

Run: `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj --nologo`
Expected: PASS, tutti (la guida viene letta dall'app: un errore di sintassi Markdown emergerebbe qui o nella finestra Guida).

- [ ] **Step 7: Commit**

```bash
git add docs/guide/it/02-installazione.md docs/guide/it/14-impostazioni.md docs/guide/en/02-installation.md docs/guide/en/14-settings.md CHANGELOG.md docs/superpowers/specs/2026-10-12-finestra-aggiornamento-design.md docs/superpowers/plans/2026-10-12-finestra-aggiornamento.md
git commit -F - <<'EOF'
docs: la guida descrive la finestra dell'aggiornamento

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
```
