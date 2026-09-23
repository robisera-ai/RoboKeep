# Controllo aggiornamenti — piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** avvisare nell'app quando su GitHub esiste una versione più recente, offrire lo scaricamento del pacchetto giusto, e farlo solo con consenso esplicito, come descritto in `docs/superpowers/specs/2026-09-23-controllo-aggiornamenti-design.md`.

**Architecture:** la logica sta nel Core in due classi pure + rete (`UpdateChecker`, `InstallKind`); tre campi nuovi in `AppSettings`; il `MainViewModel` orchestra (consenso, controllo all'avvio, banner, comandi); `SettingsWindow` ha la casella e «Controlla ora». Nessuna nuova dipendenza: `HttpClient` e `System.Text.Json` sono nel framework.

**Tech Stack:** .NET 10, WPF + WPF-UI 4.3, xUnit. Test: `cd src && dotnet test RoboKeep.Tests --nologo`.

**Regole del repo:** nessun commit finché l'utente non ha verificato; CRLF (`unix2dos -q` sui file toccati); mai terminare RoboKeep se blocca la build (chiedere di chiuderlo); testi in 5 lingue in `Loc.cs` con parità verificata da `LocParityTests`; i simboli WPF-UI vanno verificati (`grep -c <Nome> ~/.nuget/packages/wpf-ui/4.3.0/lib/net9.0-windows7.0/Wpf.Ui.dll` deve dare 1).

---

## Mappa dei file

| File | Ruolo |
|---|---|
| `src/RoboKeep.Core/Services/UpdateChecker.cs` (nuovo) | parse della risposta GitHub, confronto versioni, cadenza, fetch, download |
| `src/RoboKeep.Core/Services/InstallKind.cs` (nuovo) | self-contained o framework-dependent |
| `src/RoboKeep.Core/Models/AppSettings.cs` | `UpdateCheck`, `LastUpdateCheck`, `IgnoredUpdateVersion` |
| `src/RoboKeep.Tests/UpdateCheckerTests.cs`, `InstallKindTests.cs` (nuovi) | |
| `src/RoboKeep/ViewModels/MainViewModel.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` | consenso, controllo all'avvio, banner, comandi |
| `src/RoboKeep/ViewModels/SettingsViewModel.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs` | casella + «Controlla ora» |
| `src/RoboKeep/Localization/Loc.cs` | testi (5 lingue) |
| `docs/guide/it|en/17-*.md`, `02-*.md`, `README*.md`, `CHANGELOG.md`, `config/config.example.json` | documentazione |

---

### Task 1: `InstallKind` e `AppSettings`

**Files:**
- Create: `src/RoboKeep.Core/Services/InstallKind.cs`
- Create: `src/RoboKeep.Tests/InstallKindTests.cs`
- Modify: `src/RoboKeep.Core/Models/AppSettings.cs` (dopo `StartMinimized`)

- [ ] **Step 1: test**

```csharp
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public sealed class InstallKindTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcKind_" + Guid.NewGuid().ToString("N"));
    public InstallKindTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void SelfContained_WhenCoreclrIsNextToTheExe()
    {
        Assert.False(InstallKind.IsSelfContained(_dir));
        File.WriteAllText(Path.Combine(_dir, "coreclr.dll"), "");
        Assert.True(InstallKind.IsSelfContained(_dir));
    }

    [Fact]
    public void MissingFolder_IsNotSelfContained()
        => Assert.False(InstallKind.IsSelfContained(Path.Combine(_dir, "non-esiste")));

    [Fact]
    public void AssetName_MatchesTheInstall()
    {
        Assert.Equal("RoboKeep-1.8.0-win-x64-selfcontained.zip", InstallKind.AssetName(new Version(1, 8, 0), selfContained: true));
        Assert.Equal("RoboKeep-1.8.0-win-x64-framework-dependent.zip", InstallKind.AssetName(new Version(1, 8, 0), selfContained: false));
    }
}
```

- [ ] **Step 2: implementazione**

```csharp
namespace RoboKeep.Core.Services;

/// <summary>
/// Riconosce come e' stata installata l'app, per scaricare il pacchetto giusto: la pubblicazione
/// self-contained porta con se' il runtime (coreclr.dll accanto all'eseguibile), quella
/// framework-dependent no. Best-effort: in dubbio, framework-dependent (il pacchetto piccolo).
/// </summary>
public static class InstallKind
{
    public static bool IsSelfContained(string appDir)
    {
        try { return File.Exists(Path.Combine(appDir, "coreclr.dll")); }
        catch { return false; }
    }

    /// <summary>Nome dell'asset di release per la versione e il tipo di installazione (deve
    /// combaciare con quello prodotto da .github/workflows/release.yml).</summary>
    public static string AssetName(Version version, bool selfContained) =>
        $"RoboKeep-{version.Major}.{version.Minor}.{version.Build}-win-x64-{(selfContained ? "selfcontained" : "framework-dependent")}.zip";
}
```

- [ ] **Step 3: `AppSettings`** — aggiungere:

```csharp
    /// <summary>Controllo aggiornamenti: null = mai chiesto (la finestra principale lo chiede
    /// una volta), true/false = scelta dell'utente. Con false l'app non fa nessuna connessione.</summary>
    public bool? UpdateCheck { get; set; }

    /// <summary>Ultimo controllo eseguito: si ricontrolla al massimo una volta ogni 24 ore.</summary>
    public DateTime? LastUpdateCheck { get; set; }

    /// <summary>Versione che l'utente ha scelto di ignorare ("1.8.0"): non viene piu' proposta;
    /// una versione successiva si'.</summary>
    public string? IgnoredUpdateVersion { get; set; }
```

- [ ] **Step 4:** `dotnet test src/RoboKeep.Tests --nologo --filter InstallKindTests` → 3 verdi; `AppSettingsRoundTripTests` verde (i nuovi campi con null non devono comparire nel JSON: `ConfigStore` usa `WhenWritingNull`).

---

### Task 2: `UpdateChecker` — parte pura (parse, versioni, cadenza)

**Files:**
- Create: `src/RoboKeep.Core/Services/UpdateChecker.cs`
- Create: `src/RoboKeep.Tests/UpdateCheckerTests.cs`

- [ ] **Step 1: test** (il JSON e' quello reale della release 1.7.0, ridotto ai campi usati)

```csharp
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class UpdateCheckerTests
{
    private const string ReleaseJson = """
    {
      "tag_name": "v1.7.0",
      "html_url": "https://github.com/robisera-ai/RoboKeep/releases/tag/v1.7.0",
      "assets": [
        { "name": "RoboKeep-1.7.0-win-x64-framework-dependent.zip", "size": 3455526,
          "browser_download_url": "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/RoboKeep-1.7.0-win-x64-framework-dependent.zip" },
        { "name": "RoboKeep-1.7.0-win-x64-selfcontained.zip", "size": 68512001,
          "browser_download_url": "https://github.com/robisera-ai/RoboKeep/releases/download/v1.7.0/RoboKeep-1.7.0-win-x64-selfcontained.zip" }
      ]
    }
    """;

    [Fact]
    public void Parse_ReadsVersionPageAndBothAssets()
    {
        var info = UpdateChecker.Parse(ReleaseJson)!;
        Assert.Equal(new Version(1, 7, 0), info.Latest);
        Assert.EndsWith("/tag/v1.7.0", info.ReleaseUrl);
        Assert.Equal(68512001, info.SelfContainedSize);
        Assert.EndsWith("selfcontained.zip", info.SelfContainedUrl);
        Assert.Equal(3455526, info.FrameworkDependentSize);
        Assert.EndsWith("framework-dependent.zip", info.FrameworkDependentUrl);
    }

    [Theory]
    [InlineData("""{ "tag_name": "v1.7.0", "html_url": "x", "assets": [] }""")]           // niente pacchetti
    [InlineData("""{ "tag_name": "nightly", "html_url": "x", "assets": [] }""")]          // tag non parsabile
    [InlineData("""{ "message": "Not Found" }""")]                                        // risposta di errore
    [InlineData("non json")]
    public void Parse_ReturnsNull_WhenTheReleaseIsNotUsable(string json)
        => Assert.Null(UpdateChecker.Parse(json));

    [Theory]
    [InlineData("1.8.0", "1.7.0", null, true)]
    [InlineData("1.7.0", "1.7.0", null, false)]
    [InlineData("1.6.9", "1.7.0", null, false)]
    [InlineData("1.8.0", "1.7.0", "1.8.0", false)]   // ignorata
    [InlineData("1.8.1", "1.7.0", "1.8.0", true)]    // ignorata la 1.8.0, la 1.8.1 si propone
    public void IsNewer_ComparesAndHonoursIgnored(string latest, string current, string? ignored, bool expected)
        => Assert.Equal(expected, UpdateChecker.IsNewer(Version.Parse(latest), Version.Parse(current), ignored));

    [Fact]
    public void IsDue_Every24Hours()
    {
        var now = new DateTime(2026, 9, 23, 12, 0, 0);
        Assert.True(UpdateChecker.IsDue(null, now));
        Assert.False(UpdateChecker.IsDue(now.AddHours(-23), now));
        Assert.True(UpdateChecker.IsDue(now.AddHours(-25), now));
    }
}
```

- [ ] **Step 2: implementazione (parte pura + fetch + download)**

```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RoboKeep.Core.Services;

/// <summary>Ultima release pubblicata su GitHub, ridotta a cio' che serve all'app.</summary>
public sealed record UpdateInfo(
    Version Latest, string ReleaseUrl,
    string SelfContainedUrl, long SelfContainedSize,
    string FrameworkDependentUrl, long FrameworkDependentSize);

/// <summary>
/// Controllo aggiornamenti: una richiesta HTTPS a GitHub (releases/latest), nessun dato
/// dell'utente oltre a IP e User-Agent. Parte pura testabile (parse, confronto, cadenza) e
/// parte di rete best-effort: qualunque errore = "nessuna informazione", mai un'eccezione a video.
/// </summary>
public static class UpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/robisera-ai/RoboKeep/releases/latest";
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Legge la risposta dell'API; null se non e' una release usabile (tag non
    /// parsabile, manca uno dei due pacchetti, JSON di errore o rotto).</summary>
    public static UpdateInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagEl) || !root.TryGetProperty("assets", out var assets)) return null;
            var tag = (tagEl.GetString() ?? "").TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var version)) return null;
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";

            string? scUrl = null, fdUrl = null; long scSize = 0, fdSize = 0;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                var dl = a.GetProperty("browser_download_url").GetString() ?? "";
                var size = a.GetProperty("size").GetInt64();
                if (name.Equals(InstallKind.AssetName(version, true), StringComparison.OrdinalIgnoreCase)) { scUrl = dl; scSize = size; }
                else if (name.Equals(InstallKind.AssetName(version, false), StringComparison.OrdinalIgnoreCase)) { fdUrl = dl; fdSize = size; }
            }
            if (scUrl is null || fdUrl is null) return null;
            return new UpdateInfo(version, url, scUrl, scSize, fdUrl, fdSize);
        }
        catch { return null; }
    }

    /// <summary>true se la release e' piu' recente di quella in uso e non e' quella ignorata.</summary>
    public static bool IsNewer(Version latest, Version current, string? ignored)
    {
        if (latest <= current) return false;
        return !(Version.TryParse(ignored ?? "", out var ign) && ign == latest);
    }

    public static bool IsDue(DateTime? lastCheck, DateTime now) =>
        lastCheck is null || now - lastCheck.Value >= CheckInterval;

    /// <summary>Versione dell'app in uso (Major.Minor.Build).</summary>
    public static Version Current
    {
        get
        {
            var v = typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
        }
    }

    private static HttpClient NewClient()
    {
        var c = new HttpClient { Timeout = FetchTimeout };
        // GitHub rifiuta le richieste senza User-Agent.
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RoboKeep", Current.ToString()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    /// <summary>Interroga GitHub; null su qualunque errore (rete assente, timeout, 404, JSON inatteso).</summary>
    public static async Task<UpdateInfo?> FetchAsync(CancellationToken ct = default, string? apiUrl = null)
    {
        try
        {
            using var client = NewClient();
            var json = await client.GetStringAsync(apiUrl ?? LatestReleaseApi, ct).ConfigureAwait(false);
            return Parse(json);
        }
        catch { return null; }
    }

    /// <summary>Scarica in <paramref name="targetPath"/> passando da un file .part; verifica la
    /// dimensione attesa (se > 0) e rinomina. Restituisce false (e non lascia file) su errore o
    /// dimensione sbagliata. Lancia solo OperationCanceledException.</summary>
    public static async Task<bool> DownloadAsync(string url, string targetPath, long expectedSize,
        IProgress<double>? progress, CancellationToken ct)
    {
        var part = targetPath + ".part";
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RoboKeep", Current.ToString()));
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? expectedSize;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            long done = 0;
            await using (var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
            {
                var buffer = new byte[1 << 16];
                int n;
                while ((n = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    done += n;
                    if (total > 0) progress?.Report((double)done / total);
                }
            }

            if (expectedSize > 0 && new FileInfo(part).Length != expectedSize) { File.Delete(part); return false; }
            File.Move(part, targetPath, overwrite: true);
            return true;
        }
        catch (OperationCanceledException) { TryDelete(part); throw; }
        catch { TryDelete(part); return false; }
    }

    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
```

- [ ] **Step 3:** `dotnet test src/RoboKeep.Tests --nologo --filter UpdateCheckerTests` → verdi.

---

### Task 3: test di `DownloadAsync` con un server locale

**Files:**
- Modify: `src/RoboKeep.Tests/UpdateCheckerTests.cs` (aggiungere)

- [ ] **Step 1: test** — un `HttpListener` su una porta libera serve un buffer di dimensione nota:

```csharp
    private static (System.Net.HttpListener Listener, string Url) StartServer(byte[] body)
    {
        var port = new Random().Next(20000, 60000);
        var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                System.Net.HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch { break; }
                ctx.Response.ContentLength64 = body.Length;
                await ctx.Response.OutputStream.WriteAsync(body);
                ctx.Response.Close();
            }
        });
        return (listener, $"http://127.0.0.1:{port}/RoboKeep-test.zip");
    }

    [Fact]
    public async Task Download_WritesTheFile_WhenSizeMatches()
    {
        var body = new byte[100_000];
        new Random(1).NextBytes(body);
        var (server, url) = StartServer(body);
        var dir = Path.Combine(Path.GetTempPath(), "RbcDl_" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(dir, "pacchetto.zip");
        try
        {
            var seen = new List<double>();
            var ok = await UpdateChecker.DownloadAsync(url, target, body.Length, new Progress<double>(seen.Add), CancellationToken.None);
            await Task.Delay(50); // Progress<T> consegna in modo asincrono
            Assert.True(ok);
            Assert.Equal(body, File.ReadAllBytes(target));
            Assert.False(File.Exists(target + ".part"));
            Assert.Contains(seen, p => p >= 0.99);
        }
        finally { server.Stop(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Download_LeavesNothing_WhenSizeIsWrong()
    {
        var (server, url) = StartServer(new byte[1000]);
        var dir = Path.Combine(Path.GetTempPath(), "RbcDl_" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(dir, "pacchetto.zip");
        try
        {
            var ok = await UpdateChecker.DownloadAsync(url, target, expectedSize: 999, null, CancellationToken.None);
            Assert.False(ok);
            Assert.False(File.Exists(target));
            Assert.False(File.Exists(target + ".part"));
        }
        finally { server.Stop(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Fetch_ReturnsNull_WhenServerIsUnreachable()
        => Assert.Null(await UpdateChecker.FetchAsync(CancellationToken.None, "http://127.0.0.1:9/nessuno"));
```

Nota: `HttpListener` su `127.0.0.1` con porta alta non richiede privilegi. Se un test fallisce in CI per porta occupata, ritentare con un'altra porta (ciclo di 3 tentativi attorno a `listener.Start()`).

- [ ] **Step 2:** suite completa verde.

---

### Task 4: testi (5 lingue)

**Files:**
- Modify: `src/RoboKeep/Localization/Loc.cs` — in ciascuna delle 5 sezioni, accanto a `Main_ReenableDisks` (le chiavi Upd_*) e a `About_Version` (le chiavi Set_Update*).

| chiave | it | en | es | fr | de |
|---|---|---|---|---|---|
| Upd_ConsentTitle | Controllo aggiornamenti | Update check | Comprobar actualizaciones | Recherche de mises à jour | Updateprüfung |
| Upd_Consent | Vuoi che RoboKeep controlli se ci sono aggiornamenti? Fa una sola richiesta a GitHub all'avvio, al massimo una volta al giorno, senza inviare dati tuoi. Puoi cambiare idea in Impostazioni. | Do you want RoboKeep to check for updates? It makes a single request to GitHub at startup, at most once a day, sending none of your data. You can change your mind in Settings. | ¿Quieres que RoboKeep compruebe si hay actualizaciones? Hace una sola petición a GitHub al iniciar, como máximo una vez al día, sin enviar datos tuyos. Puedes cambiar de opinión en Configuración. | Voulez-vous que RoboKeep recherche les mises à jour ? Il fait une seule requête à GitHub au démarrage, au plus une fois par jour, sans envoyer vos données. Vous pouvez changer d'avis dans les Paramètres. | Soll RoboKeep nach Updates suchen? Es stellt beim Start eine einzige Anfrage an GitHub, höchstens einmal täglich, ohne Ihre Daten zu senden. Sie können das in den Einstellungen ändern. |
| Upd_Available | È disponibile RoboKeep {0} (hai la {1}). Scarica lo zip, chiudi RoboKeep, estrailo sopra la cartella attuale e riapri. | RoboKeep {0} is available (you have {1}). Download the zip, close RoboKeep, extract it over the current folder and reopen. | RoboKeep {0} está disponible (tienes la {1}). Descarga el zip, cierra RoboKeep, extráelo sobre la carpeta actual y vuelve a abrir. | RoboKeep {0} est disponible (vous avez la {1}). Téléchargez le zip, fermez RoboKeep, extrayez-le sur le dossier actuel et rouvrez. | RoboKeep {0} ist verfügbar (Sie haben {1}). Laden Sie die Zip-Datei herunter, schließen Sie RoboKeep, entpacken Sie sie über den aktuellen Ordner und öffnen Sie es erneut. |
| Upd_WhatsNew | Novità | What's new | Novedades | Nouveautés | Neuerungen |
| Upd_Download | Scarica | Download | Descargar | Télécharger | Herunterladen |
| Upd_Ignore | Ignora questa versione | Ignore this version | Ignorar esta versión | Ignorer cette version | Diese Version ignorieren |
| Upd_Downloading | Scaricamento… {0}% | Downloading… {0}% | Descargando… {0}% | Téléchargement… {0} % | Wird heruntergeladen… {0}% |
| Upd_Downloaded | Scaricato in {0}. Chiudi RoboKeep, estrai lo zip sopra la cartella attuale e riapri. | Downloaded to {0}. Close RoboKeep, extract the zip over the current folder and reopen. | Descargado en {0}. Cierra RoboKeep, extrae el zip sobre la carpeta actual y vuelve a abrir. | Téléchargé dans {0}. Fermez RoboKeep, extrayez le zip sur le dossier actuel et rouvrez. | Heruntergeladen nach {0}. Schließen Sie RoboKeep, entpacken Sie die Zip-Datei über den aktuellen Ordner und öffnen Sie es erneut. |
| Upd_DownloadFailed | Scaricamento non riuscito: riprova, oppure scarica dalla pagina della release. | Download failed: try again, or download from the release page. | Descarga fallida: inténtalo de nuevo o descarga desde la página de la versión. | Téléchargement échoué : réessayez ou téléchargez depuis la page de la version. | Download fehlgeschlagen: erneut versuchen oder von der Release-Seite herunterladen. |
| Set_UpdateCheck | Cerca aggiornamenti automaticamente (una richiesta a GitHub all'avvio, al massimo una al giorno) | Check for updates automatically (one request to GitHub at startup, at most once a day) | Buscar actualizaciones automáticamente (una petición a GitHub al iniciar, como máximo una al día) | Rechercher les mises à jour automatiquement (une requête à GitHub au démarrage, au plus une par jour) | Automatisch nach Updates suchen (eine Anfrage an GitHub beim Start, höchstens einmal täglich) |
| Set_UpdateCheckNow | Controlla ora | Check now | Comprobar ahora | Vérifier maintenant | Jetzt prüfen |
| Set_UpdateUpToDate | Sei aggiornato. | You are up to date. | Estás al día. | Vous êtes à jour. | Sie sind auf dem neuesten Stand. |
| Set_UpdateAvailable | È disponibile la {0}: chiudi le Impostazioni, l'avviso è nella finestra principale. | Version {0} is available: close Settings, the notice is in the main window. | Está disponible la {0}: cierra Configuración, el aviso está en la ventana principal. | La {0} est disponible : fermez les Paramètres, l'avis est dans la fenêtre principale. | Version {0} ist verfügbar: schließen Sie die Einstellungen, der Hinweis steht im Hauptfenster. |
| Set_UpdateFailed | Controllo non riuscito (rete assente o GitHub non raggiungibile). | Check failed (no network or GitHub unreachable). | Comprobación fallida (sin red o GitHub inaccesible). | Vérification échouée (pas de réseau ou GitHub injoignable). | Prüfung fehlgeschlagen (kein Netz oder GitHub nicht erreichbar). |

- [ ] `dotnet test src/RoboKeep.Tests --nologo --filter LocParity` → verde (le chiavi Upd_/Set_Update* non sono ancora usate: il test controlla solo che le usate esistano).

---

### Task 5: `MainViewModel` — consenso, controllo, banner, comandi

**Files:**
- Modify: `src/RoboKeep/ViewModels/MainViewModel.cs`
- Modify: `src/RoboKeep/MainWindow.xaml` (banner sotto quello di salute), `src/RoboKeep/MainWindow.xaml.cs` (consenso + avvio)

- [ ] **Step 1: proprietà e comandi nel view model**

```csharp
    // --- Aggiornamenti ---
    private UpdateInfo? _update;
    private string _updateBannerText = "";
    private bool _updateBannerVisible;
    private bool _updateBusy;
    private CancellationTokenSource? _downloadCts;

    public string UpdateBannerText { get => _updateBannerText; private set => SetField(ref _updateBannerText, value); }
    public bool UpdateBannerVisible { get => _updateBannerVisible; set => SetField(ref _updateBannerVisible, value); }
    /// <summary>Scaricamento in corso: i pulsanti del banner si disattivano.</summary>
    public bool UpdateActionsEnabled => !_updateBusy;
    public RelayCommand UpdateWhatsNewCommand { get; }
    public RelayCommand UpdateDownloadCommand { get; }
    public RelayCommand UpdateIgnoreCommand { get; }
```

Nel costruttore:

```csharp
        UpdateWhatsNewCommand = new RelayCommand(() => OpenUrl(_update?.ReleaseUrl));
        UpdateDownloadCommand = new RelayCommand(async () => await DownloadUpdateAsync(), () => UpdateActionsEnabled);
        UpdateIgnoreCommand = new RelayCommand(IgnoreUpdate, () => UpdateActionsEnabled);
```

Metodi:

```csharp
    /// <summary>Controlla se esiste una versione nuova. <paramref name="force"/> ignora la cadenza
    /// (pulsante "Controlla ora"). Restituisce: null = errore, false = aggiornato, true = nuova.</summary>
    public async Task<bool?> CheckForUpdatesAsync(bool force)
    {
        var s = _host.Config.Settings;
        if (!force && (s.UpdateCheck != true || !UpdateChecker.IsDue(s.LastUpdateCheck, DateTime.Now))) return null;

        var info = await UpdateChecker.FetchAsync();
        if (info is null) return null;
        s.LastUpdateCheck = DateTime.Now;
        _host.SaveConfig();

        if (!UpdateChecker.IsNewer(info.Latest, UpdateChecker.Current, force ? null : s.IgnoredUpdateVersion))
        {
            UpdateBannerVisible = false;
            return false;
        }
        _update = info;
        UpdateBannerText = string.Format(Loc.Instance["Upd_Available"], info.Latest, UpdateChecker.Current);
        UpdateBannerVisible = true;
        return true;
    }

    private void IgnoreUpdate()
    {
        if (_update is null) return;
        _host.Config.Settings.IgnoredUpdateVersion = _update.Latest.ToString();
        _host.SaveConfig();
        UpdateBannerVisible = false;
    }

    private async Task DownloadUpdateAsync()
    {
        if (_update is null || _updateBusy) return;
        var selfContained = InstallKind.IsSelfContained(AppContext.BaseDirectory);
        var url = selfContained ? _update.SelfContainedUrl : _update.FrameworkDependentUrl;
        var size = selfContained ? _update.SelfContainedSize : _update.FrameworkDependentSize;
        var folder = DownloadsFolder();
        var target = Path.Combine(folder, InstallKind.AssetName(_update.Latest, selfContained));

        SetUpdateBusy(true);
        _downloadCts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double>(p => UpdateBannerText = string.Format(Loc.Instance["Upd_Downloading"], (int)(p * 100)));
            var ok = await UpdateChecker.DownloadAsync(url, target, size, progress, _downloadCts.Token);
            UpdateBannerText = ok
                ? string.Format(Loc.Instance["Upd_Downloaded"], folder)
                : Loc.Instance["Upd_DownloadFailed"];
            if (ok) RevealInExplorer(target);
        }
        catch (OperationCanceledException) { UpdateBannerText = Loc.Instance["Upd_DownloadFailed"]; }
        finally { SetUpdateBusy(false); _downloadCts.Dispose(); _downloadCts = null; }
    }

    private void SetUpdateBusy(bool busy)
    {
        _updateBusy = busy;
        OnPropertyChanged(nameof(UpdateActionsEnabled));
        UpdateDownloadCommand.RaiseCanExecuteChanged();
        UpdateIgnoreCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Cartella Download dell'utente; se non esiste, %TEMP% (lo dice il banner).</summary>
    private static string DownloadsFolder()
    {
        var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(dl) ? dl : Path.GetTempPath();
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private static void RevealInExplorer(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); } catch { }
    }
```

`RelayCommand` usa `CommandManager.RequerySuggested`: NON ha `RaiseCanExecuteChanged`. In `SetUpdateBusy` sostituire le due chiamate con `System.Windows.Input.CommandManager.InvalidateRequerySuggested();`. Il costruttore accetta solo `Action`: usare `new RelayCommand(() => _ = DownloadUpdateAsync(), () => UpdateActionsEnabled)`.

- [ ] **Step 2: `MainWindow.xaml`** — subito dopo l'`ui:InfoBar` della salute (stesso `Grid.Row="0"`: metterli in uno `StackPanel` verticale se non lo sono gia'):

```xml
<ui:InfoBar Severity="Informational" IsClosable="True"
            IsOpen="{Binding UpdateBannerVisible, Mode=TwoWay}"
            Title="{l:Tr Upd_ConsentTitle}" Message="{Binding UpdateBannerText}">
    <ui:InfoBar.Content>
        <StackPanel Orientation="Horizontal" Margin="0,4,0,4">
            <ui:Button Content="{l:Tr Upd_WhatsNew}" Icon="{ui:SymbolIcon Open24}" Command="{Binding UpdateWhatsNewCommand}" Margin="0,0,8,0"/>
            <ui:Button Content="{l:Tr Upd_Download}" Icon="{ui:SymbolIcon ArrowDownload24}" Appearance="Primary" Command="{Binding UpdateDownloadCommand}" Margin="0,0,8,0"/>
            <ui:Button Content="{l:Tr Upd_Ignore}" Command="{Binding UpdateIgnoreCommand}"/>
        </StackPanel>
    </ui:InfoBar.Content>
</ui:InfoBar>
```

I simboli `Open24` e `ArrowDownload24` esistono nella DLL (gia verificato).

- [ ] **Step 3: `MainWindow.xaml.cs`** — nel costruttore, dopo `DataContext = _vm;`:

```csharp
        Loaded += OnLoadedCheckUpdates;
```

e il gestore:

```csharp
    // Consenso una volta sola, poi controllo in background (mai dalla riga di comando: questa e'
    // la finestra). Se la rete manca, silenzio: l'utente non deve vedere errori all'avvio.
    private async void OnLoadedCheckUpdates(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedCheckUpdates;
        var s = _host.Config.Settings;
        if (s.UpdateCheck is null)
        {
            var r = System.Windows.MessageBox.Show(this, Loc.Instance["Upd_Consent"], Loc.Instance["Upd_ConsentTitle"],
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            s.UpdateCheck = r == MessageBoxResult.Yes;
            _host.SaveConfig();
        }
        try { await _vm.CheckForUpdatesAsync(force: false); }
        catch { /* best-effort */ }
    }
```

Attenzione: la finestra puo' partire nascosta nel tray (`StartMinimized`): in quel caso NON chiedere il consenso (rimandare al primo avvio visibile): `if (s.UpdateCheck is null && IsVisible && WindowState != WindowState.Minimized) { ... }`; il controllo automatico si fa comunque solo se `UpdateCheck == true`.

- [ ] **Step 4:** build 0 avvisi; prova manuale: con `updateCheck` assente nel config, all'avvio compare la domanda; rispondendo Sì e con la versione locale 1.7.0 (o abbassando temporaneamente `<Version>` in `Directory.Build.props` a 1.6.0 per la prova) compare il banner; Scarica porta lo zip in Download e apre Esplora risorse; Ignora nasconde e scrive `ignoredUpdateVersion` nel config. Ripristinare la versione dopo la prova.

---

### Task 6: Impostazioni — casella e «Controlla ora»

**Files:**
- Modify: `src/RoboKeep/ViewModels/SettingsViewModel.cs`, `src/RoboKeep/SettingsWindow.xaml`, `src/RoboKeep/SettingsWindow.xaml.cs`, `src/RoboKeep/MainWindow.xaml.cs` (passare il view model principale)

- [ ] **Step 1: view model** — proprieta' `public bool UpdateCheck { get => _s.UpdateCheck == true; set { _s.UpdateCheck = value; OnPropertyChanged(); } }`.

- [ ] **Step 2: XAML** — nella scheda Generale, dopo il blocco `Set_CredScopeHint`:

```xml
<CheckBox Content="{l:Tr Set_UpdateCheck}" IsChecked="{Binding UpdateCheck}" Margin="0,16,0,0"/>
```

e nella scheda Info, accanto alla versione (`AppVersionText`):

```xml
<ui:Button Content="{l:Tr Set_UpdateCheckNow}" Margin="12,0,0,0" Click="OnCheckUpdatesNow" x:Name="CheckUpdatesButton"/>
```

seguito da `<TextBlock x:Name="UpdateStatusText" Opacity="0.8" Margin="0,6,0,0" TextWrapping="Wrap"/>`.

- [ ] **Step 3: code-behind** — `SettingsWindow` riceve anche il `MainViewModel` (`new SettingsWindow(_host, _vm)`), e:

```csharp
    private async void OnCheckUpdatesNow(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        try
        {
            var r = await _main.CheckForUpdatesAsync(force: true);
            UpdateStatusText.Text = r switch
            {
                true => string.Format(Loc.Instance["Set_UpdateAvailable"], /* versione */ _main.LatestUpdateVersion),
                false => Loc.Instance["Set_UpdateUpToDate"],
                null => Loc.Instance["Set_UpdateFailed"],
            };
        }
        finally { CheckUpdatesButton.IsEnabled = true; }
    }
```

Aggiungere in `MainViewModel` `public string LatestUpdateVersion => _update?.Latest.ToString() ?? "";`.

- [ ] **Step 4:** build, suite, prova manuale del pulsante nei tre esiti (aggiornato; nuova versione con `<Version>` abbassata; rete staccata).

---

### Task 7: documentazione

**Files:**
- Modify: `docs/guide/it/17-privacy-sicurezza.md`, `docs/guide/en/17-privacy-security.md`, `docs/guide/it/02-installazione.md`, `docs/guide/en/02-installation.md`, `README.md`, `readmeita.md`, `readmees.md`, `readmefr.md`, `readmede.md`, `CHANGELOG.md`, `config/config.example.json`

- [ ] **Step 1: cap. 17** — sostituire la frase «nessun controllo aggiornamenti, nessun account, nessun traffico di rete» con:

IT: «nessun account e nessun traffico di rete, con **una sola eccezione, che decidi tu**: il controllo aggiornamenti. Al primo avvio RoboKeep chiede se vuoi che controlli l'esistenza di una versione nuova; se dici sì, all'avvio (al massimo una volta al giorno) fa una richiesta HTTPS a `api.github.com` per leggere il numero dell'ultima versione pubblicata. GitHub riceve il tuo indirizzo IP e il nome del programma con la sua versione, come per qualunque sito che visiti; nessun dato sui tuoi job, dischi o file. Lo scaricamento parte solo se lo chiedi tu. Puoi spegnere tutto in Impostazioni.»

EN: «no account and no network traffic, with **one exception, and it's your call**: the update check. On first start RoboKeep asks whether you want it to check for a new version; if you say yes, at startup (at most once a day) it makes one HTTPS request to `api.github.com` to read the latest published version number. GitHub receives your IP address and the program name with its version, as for any website you visit; nothing about your jobs, disks or files. Downloading happens only when you ask. You can switch it off in Settings.»

- [ ] **Step 2: cap. 02** — aggiungere in coda una sezione «Aggiornare» / «Updating»:

IT: «Quando esiste una versione nuova (se hai attivato il controllo), nella finestra principale compare un avviso con **Novità**, **Scarica** e **Ignora questa versione**. Scarica prende il pacchetto giusto per la tua installazione e lo mette in Download. Poi fai tu: chiudi RoboKeep, estrai lo zip **sopra** la cartella attuale (sovrascrivendo), riapri. Impostazioni, job, cronologia e log stanno in `%APPDATA%\RoboKeep` e non vengono toccati. Se un'attività pianificata punta a quella cartella, dopo la sostituzione usa la versione nuova da sola.»

EN: equivalente.

- [ ] **Step 3: README (5)** — nella sezione privacy («no telemetry» / «zero telemetria»), aggiungere: «An optional update check (one request to GitHub, off until you say yes)» nelle rispettive lingue.

- [ ] **Step 4: CHANGELOG** — in Unreleased, sezione Added: «**Optional update check.** On first start RoboKeep asks whether it may check GitHub for a newer version (one HTTPS request at startup, at most once a day, no data of yours). When one exists, a banner offers *What's new*, *Download* (the right package for your install, into Downloads, size-verified) and *Ignore this version*. You replace the files yourself; RoboKeep never updates itself. Settings has the switch and a *Check now* button.»

- [ ] **Step 5: config.example.json** — aggiungere in `settings`: `"updateCheck": null,` con un commento nel README? Il JSON non ammette commenti: aggiungere `"updateCheck": true, "lastUpdateCheck": null, "ignoredUpdateVersion": null`.

---

### Task 8: verifica finale e consegna

- [ ] Suite in Debug e Release verdi; build 0 avvisi; `unix2dos` su tutti i file toccati.
- [ ] Prova manuale completa descritta nei Task 5 e 6 (con `<Version>` abbassata e poi ripristinata).
- [ ] Commit solo dopo conferma dell'utente; messaggio: `feat: controllo aggiornamenti opzionale con scaricamento del pacchetto giusto`.

---

## Auto-verifica

- Copertura della spec: consenso (T5), casella + Controlla ora (T6), cadenza 24h e solo GUI (T2, T5), banner con tre pulsanti (T5), scelta pacchetto e verifica dimensione (T1, T2, T5), errori silenziosi (T2), privacy e guida (T7), test (T1-T3).
- Nomi coerenti: `UpdateInfo`, `UpdateChecker.{Parse,IsNewer,IsDue,Current,FetchAsync,DownloadAsync}`, `InstallKind.{IsSelfContained,AssetName}`, `AppSettings.{UpdateCheck,LastUpdateCheck,IgnoredUpdateVersion}`, `MainViewModel.{CheckForUpdatesAsync,UpdateBannerText,UpdateBannerVisible,UpdateActionsEnabled,LatestUpdateVersion}`, chiavi `Upd_*` e `Set_Update*`.
