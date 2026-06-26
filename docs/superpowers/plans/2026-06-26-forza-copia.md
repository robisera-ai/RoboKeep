# Forza copia — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere una lista per-job "Forza copia" che ricopia i file con data/dimensione congelate tramite una seconda passata robocopy `/IS /IT`, con modalità opzionale "smart" (ricopia solo se l'hash SHA256 è cambiato).

**Architecture:** Il modello `BackupJob` guadagna `ForceCopyFiles` + `ForceCopySmart`. `RobocopyArgsBuilder` ottiene un metodo `BuildForceCopyPass` per la seconda passata. Un `ForceCopyPlanner` (con persistenza `ForceCopyHashStore`) decide i filtri da passare: in modalità semplice i pattern, in smart i soli file il cui hash è cambiato. `RobocopyRunner` esegue le due passate nello stesso `RunAsync`, somma i conteggi e combina gli exit code in OR. La GUI (`JobEditorWindow`) aggiunge un riquadro con la lista e la spunta smart.

**Tech Stack:** .NET 10 (net10.0-windows), C#, xUnit, WPF + WPF-UI, System.Text.Json, System.Security.Cryptography (SHA256).

---

## File Structure

- **Modifica** `src/RobocopySW.Core/Models/BackupJob.cs` — due nuove proprietà.
- **Modifica** `src/RobocopySW.Core/Services/RobocopyArgsBuilder.cs` — metodo `BuildForceCopyPass`.
- **Crea** `src/RobocopySW.Core/Services/ForceCopyHashStore.cs` — persistenza hash per job (JSON).
- **Crea** `src/RobocopySW.Core/Services/ForceCopyPlanner.cs` — pianifica i filtri (semplice/smart).
- **Modifica** `src/RobocopySW.Core/Services/RobocopyRunner.cs` — due passate + aggregazione.
- **Modifica** `src/RobocopySW/AppHost.cs` — costruisce store+planner e li passa al runner.
- **Modifica** `src/RobocopySW/Localization/Loc.cs` — 5 chiavi × 5 lingue.
- **Modifica** `src/RobocopySW/ViewModels/JobEditorViewModel.cs` — proprietà + preview.
- **Modifica** `src/RobocopySW/JobEditorWindow.xaml` — riquadro UI.
- **Test** `src/RobocopySW.Tests/RobocopyArgsBuilderTests.cs`, `ForceCopyHashStoreTests.cs` (nuovo), `ForceCopyPlannerTests.cs` (nuovo), `RobocopyRunnerIntegrationTests.cs`.

**Comandi comuni:**
- Build: `dotnet build src/RobocopySW.sln -c Debug --nologo`
- Tutti i test: `dotnet test src/RobocopySW.sln --nologo`
- Test filtrati: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~ForceCopyPlannerTests"`

**Nota commit:** messaggi senza virgolette doppie (rompono PowerShell), e terminare con `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

### Task 1: Modello — proprietà ForceCopyFiles + ForceCopySmart

**Files:**
- Modify: `src/RobocopySW.Core/Models/BackupJob.cs`
- Test: `src/RobocopySW.Tests/ConfigStoreTests.cs` (se assente, crearlo)

- [ ] **Step 1: Scrivi il test di round-trip**

Aggiungi in `ConfigStoreTests.cs` (se il file non esiste, crealo con questo contenuto e gli `using` mostrati):

```csharp
using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

public sealed class ForceCopyConfigRoundTripTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "RbcCfg_" + Guid.NewGuid().ToString("N"), "config.json");

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void ForceCopyFields_RoundTripThroughConfigStore()
    {
        var store = new ConfigStore(_path);
        var config = store.Load();
        config.Jobs.Add(new BackupJob
        {
            Name = "J",
            Source = @"C:\s",
            Destination = @"D:\d",
            ForceCopyFiles = new() { "*.pst", "db.dat" },
            ForceCopySmart = true,
        });
        store.Save(config);

        var job = new ConfigStore(_path).Load().Jobs.Single(j => j.Name == "J");
        Assert.Equal(new[] { "*.pst", "db.dat" }, job.ForceCopyFiles);
        Assert.True(job.ForceCopySmart);
    }
}
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~ForceCopyConfigRoundTripTests"`
Atteso: FAIL di compilazione (`ForceCopyFiles`/`ForceCopySmart` non esistono).

- [ ] **Step 3: Aggiungi le proprietà al modello**

In `BackupJob.cs`, subito dopo la proprietà `ExcludeDirs` (riga ~53), aggiungi:

```csharp
    /// <summary>Pattern di file da forzare in copia anche se data/dimensione non cambiano
    /// (seconda passata robocopy con /IS /IT). Es. *.pst, database.dat. Vuoto = feature disattivata.</summary>
    public List<string> ForceCopyFiles { get; set; } = new();

    /// <summary>Modalità della lista <see cref="ForceCopyFiles"/>:
    /// false = "copia sempre" (ricopia integrale a ogni run);
    /// true = "smart" (ricopia solo i file il cui hash è cambiato dall'ultimo backup).</summary>
    public bool ForceCopySmart { get; set; }
```

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~ForceCopyConfigRoundTripTests"`
Atteso: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RobocopySW.Core/Models/BackupJob.cs src/RobocopySW.Tests/ConfigStoreTests.cs
git commit -m "feat: ForceCopyFiles e ForceCopySmart nel modello BackupJob"
```

---

### Task 2: Motore — RobocopyArgsBuilder.BuildForceCopyPass

**Files:**
- Modify: `src/RobocopySW.Core/Services/RobocopyArgsBuilder.cs`
- Test: `src/RobocopySW.Tests/RobocopyArgsBuilderTests.cs`

- [ ] **Step 1: Scrivi i test della seconda passata**

Aggiungi in fondo a `RobocopyArgsBuilderTests.cs` (prima della `}` finale):

```csharp
    [Fact]
    public void ForceCopyPass_FiltersFollowSourceAndDestination()
    {
        var args = RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst", "db.dat" });
        Assert.Equal(@"C:\src", args[0]);
        Assert.Equal(@"D:\dst", args[1]);
        Assert.Equal("*.pst", args[2]);
        Assert.Equal("db.dat", args[3]);
    }

    [Fact]
    public void ForceCopyPass_IncludesIsItAndE_NotMirNotXo()
    {
        var args = RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst" });
        Assert.Contains("/IS", args);
        Assert.Contains("/IT", args);
        Assert.Contains("/E", args);
        Assert.DoesNotContain("/MIR", args);
        Assert.DoesNotContain("/XO", args);
    }

    [Fact]
    public void ForceCopyPass_RespectsCopyAllMtAndZ()
    {
        var job = NewJob();
        job.CopyAll = true;
        job.Restartable = true;
        var args = RobocopyArgsBuilder.BuildForceCopyPass(job, new[] { "*.pst" });
        Assert.Contains("/COPYALL", args);
        Assert.Contains("/MT:8", args);
        Assert.Contains("/Z", args);
    }

    [Fact]
    public void ForceCopyPass_DryRunAddsListOnly()
    {
        Assert.Contains("/L", RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst" }, dryRun: true));
        Assert.DoesNotContain("/L", RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst" }, dryRun: false));
    }
```

- [ ] **Step 2: Esegui e verifica fallimento**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~RobocopyArgsBuilderTests"`
Atteso: FAIL di compilazione (`BuildForceCopyPass` non esiste).

- [ ] **Step 3: Implementa il metodo**

In `RobocopyArgsBuilder.cs`, subito dopo il metodo `Build` (prima di `ToDisplayString`), aggiungi:

```csharp
    /// <summary>
    /// Costruisce gli argomenti della passata "forza copia": copia i file indicati da
    /// <paramref name="filters"/> anche se identici (<c>/IS /IT</c>), senza mai cancellare
    /// (niente <c>/MIR</c>) e senza saltare i più vecchi (niente <c>/XO</c>).
    /// </summary>
    public static IReadOnlyList<string> BuildForceCopyPass(
        BackupJob job, IReadOnlyList<string> filters, bool dryRun = false, string? logFile = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(filters);

        var source = (job.Source ?? "").Trim();
        var dest = (job.Destination ?? "").Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(dest))
            throw new InvalidOperationException(
                $"Il job '{job.Name}' deve avere sorgente e destinazione valorizzate.");

        var args = new List<string> { source, dest };
        args.AddRange(filters.Where(f => !string.IsNullOrWhiteSpace(f)));

        args.Add("/E");                 // ricorsivo, mai /MIR (la passata forzata non cancella)
        args.Add("/IS");                // include same: copia anche i file identici
        args.Add("/IT");                // include tweaked
        args.Add(job.CopyAll ? "/COPYALL" : "/COPY:DAT");
        args.Add("/XJ");

        if (job.MultiThread > 0)
            args.Add($"/MT:{Math.Min(job.MultiThread, MaxThreads)}");

        if (job.UnbufferedIO)
            args.Add("/J");
        else if (job.Restartable)
            args.Add("/Z");

        args.Add($"/R:{Math.Max(0, job.Retries)}");
        args.Add($"/W:{Math.Max(0, job.Wait)}");

        if (dryRun)
            args.Add("/L");

        if (!string.IsNullOrWhiteSpace(logFile))
        {
            args.Add("/TEE");
            args.Add($"/LOG:{logFile}");
        }

        return args;
    }
```

- [ ] **Step 4: Esegui e verifica successo**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~RobocopyArgsBuilderTests"`
Atteso: PASS (inclusi i nuovi 4 test).

- [ ] **Step 5: Commit**

```bash
git add src/RobocopySW.Core/Services/RobocopyArgsBuilder.cs src/RobocopySW.Tests/RobocopyArgsBuilderTests.cs
git commit -m "feat: BuildForceCopyPass per la seconda passata robocopy"
```

---

### Task 3: Persistenza — ForceCopyHashStore

**Files:**
- Create: `src/RobocopySW.Core/Services/ForceCopyHashStore.cs`
- Test: `src/RobocopySW.Tests/ForceCopyHashStoreTests.cs`

- [ ] **Step 1: Scrivi i test**

Crea `src/RobocopySW.Tests/ForceCopyHashStoreTests.cs`:

```csharp
using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

public sealed class ForceCopyHashStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcHash_" + Guid.NewGuid().ToString("N"));
    private string Path_ => System.IO.Path.Combine(_dir, "forcecopy-hashes.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenFileMissing()
    {
        Assert.Empty(new ForceCopyHashStore(Path_).Load("J"));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new ForceCopyHashStore(Path_);
        store.Save("J", new Dictionary<string, string> { [@"C:\a.dat"] = "ABC" });

        var loaded = new ForceCopyHashStore(Path_).Load("J");
        Assert.Equal("ABC", loaded[@"C:\a.dat"]);
    }

    [Fact]
    public void Save_IsolatesByJobName()
    {
        var store = new ForceCopyHashStore(Path_);
        store.Save("J1", new Dictionary<string, string> { [@"C:\a"] = "1" });
        store.Save("J2", new Dictionary<string, string> { [@"C:\b"] = "2" });

        var reread = new ForceCopyHashStore(Path_);
        Assert.Equal("1", reread.Load("J1")[@"C:\a"]);
        Assert.Equal("2", reread.Load("J2")[@"C:\b"]);
        Assert.Empty(reread.Load("J1").Where(kv => kv.Key == @"C:\b"));
    }
}
```

- [ ] **Step 2: Esegui e verifica fallimento**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~ForceCopyHashStoreTests"`
Atteso: FAIL di compilazione (`ForceCopyHashStore` non esiste).

- [ ] **Step 3: Implementa lo store**

Crea `src/RobocopySW.Core/Services/ForceCopyHashStore.cs`:

```csharp
using System.Text.Json;

namespace RobocopySW.Core.Services;

/// <summary>
/// Persistenza degli hash dei file in modalità "Forza copia smart", per job, in un file JSON.
/// Struttura: { nomeJob: { percorsoFileAssoluto: hashHex } }. Best-effort: errori ignorati.
/// </summary>
public sealed class ForceCopyHashStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public ForceCopyHashStore(string path) => _path = path;

    /// <summary>Hash noti per il job (percorso file → hash). Vuoto se assente/illeggibile.</summary>
    public IReadOnlyDictionary<string, string> Load(string jobName)
    {
        var all = LoadAll();
        return all.TryGetValue(jobName, out var map) ? map : new Dictionary<string, string>();
    }

    /// <summary>Sostituisce gli hash del job indicato (merge con gli altri job).</summary>
    public void Save(string jobName, IReadOnlyDictionary<string, string> hashes)
    {
        try
        {
            var all = LoadAll();
            all[jobName] = new Dictionary<string, string>(hashes);

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_path, JsonSerializer.Serialize(all, Options));
        }
        catch
        {
            // best-effort: un errore di scrittura non deve interrompere il backup.
        }
    }

    private Dictionary<string, Dictionary<string, string>> LoadAll()
    {
        try
        {
            if (!File.Exists(_path))
                return new();
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                       File.ReadAllText(_path)) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
```

- [ ] **Step 4: Esegui e verifica successo**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~ForceCopyHashStoreTests"`
Atteso: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RobocopySW.Core/Services/ForceCopyHashStore.cs src/RobocopySW.Tests/ForceCopyHashStoreTests.cs
git commit -m "feat: ForceCopyHashStore per la persistenza degli hash"
```

---

### Task 4: Pianificazione — ForceCopyPlanner

**Files:**
- Create: `src/RobocopySW.Core/Services/ForceCopyPlanner.cs`
- Test: `src/RobocopySW.Tests/ForceCopyPlannerTests.cs`

- [ ] **Step 1: Scrivi i test**

Crea `src/RobocopySW.Tests/ForceCopyPlannerTests.cs`:

```csharp
using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

public sealed class ForceCopyPlannerTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "RbcPlan_" + Guid.NewGuid().ToString("N"));
    private readonly string _src;
    private readonly string _hashPath;

    public ForceCopyPlannerTests()
    {
        _src = Path.Combine(_base, "src");
        _hashPath = Path.Combine(_base, "hashes.json");
        Directory.CreateDirectory(_src);
    }

    public void Dispose()
    {
        if (Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
    }

    private BackupJob Job(bool smart, params string[] patterns) => new()
    {
        Name = "J",
        Source = _src,
        Destination = Path.Combine(_base, "dst"),
        ForceCopyFiles = patterns.ToList(),
        ForceCopySmart = smart,
    };

    private ForceCopyPlanner NewPlanner() => new(new ForceCopyHashStore(_hashPath));

    [Fact]
    public async Task EmptyList_ReturnsEmptyPlan()
    {
        var plan = await NewPlanner().PlanAsync(Job(smart: false), null, default);
        Assert.Empty(plan.Filters);
    }

    [Fact]
    public async Task SimpleMode_ReturnsPatternsAsFilters()
    {
        var plan = await NewPlanner().PlanAsync(Job(smart: false, "*.pst", "db.dat"), null, default);
        Assert.False(plan.Smart);
        Assert.Equal(new[] { "*.pst", "db.dat" }, plan.Filters);
    }

    [Fact]
    public async Task SmartMode_NewFile_IsTreatedAsChanged()
    {
        File.WriteAllText(Path.Combine(_src, "data.bin"), "AAAA");
        var plan = await NewPlanner().PlanAsync(Job(smart: true, "*.bin"), null, default);
        Assert.True(plan.Smart);
        Assert.Contains("data.bin", plan.Filters);
        Assert.NotEmpty(plan.NewHashes);
    }

    [Fact]
    public async Task SmartMode_UnchangedFile_IsNotInFilters()
    {
        File.WriteAllText(Path.Combine(_src, "data.bin"), "AAAA");
        var planner = NewPlanner();
        var first = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        planner.Commit("J", first.NewHashes);

        var second = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        Assert.Empty(second.Filters);
    }

    [Fact]
    public async Task SmartMode_ContentChangedSameSize_IsInFilters()
    {
        var file = Path.Combine(_src, "data.bin");
        File.WriteAllText(file, "AAAA");
        var planner = NewPlanner();
        var first = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        planner.Commit("J", first.NewHashes);

        File.WriteAllText(file, "BBBB"); // stessa lunghezza, contenuto diverso
        var second = await planner.PlanAsync(Job(smart: true, "*.bin"), null, default);
        Assert.Contains("data.bin", second.Filters);
    }
}
```

- [ ] **Step 2: Esegui e verifica fallimento**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~ForceCopyPlannerTests"`
Atteso: FAIL di compilazione (`ForceCopyPlanner`/`ForceCopyPlan` non esistono).

- [ ] **Step 3: Implementa pianificatore e record**

Crea `src/RobocopySW.Core/Services/ForceCopyPlanner.cs`:

```csharp
using System.Security.Cryptography;
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>
/// Piano della passata "Forza copia": <paramref name="Filters"/> sono i filtri robocopy
/// (pattern in modalità semplice, nomi dei file cambiati in smart); <paramref name="NewHashes"/>
/// sono gli hash correnti di tutti i file abbinati (solo smart, da salvare a copia riuscita).
/// </summary>
public sealed record ForceCopyPlan(
    IReadOnlyList<string> Filters,
    IReadOnlyDictionary<string, string> NewHashes,
    bool Smart);

/// <summary>
/// Decide quali file forzare in copia. In modalità semplice restituisce i pattern così come sono.
/// In modalità smart enumera i file abbinati, ne calcola l'hash SHA256 e restituisce solo quelli
/// il cui hash è cambiato rispetto all'ultimo backup (via <see cref="ForceCopyHashStore"/>).
/// </summary>
public sealed class ForceCopyPlanner
{
    private readonly ForceCopyHashStore _store;

    public ForceCopyPlanner(ForceCopyHashStore store) => _store = store;

    public async Task<ForceCopyPlan> PlanAsync(
        BackupJob job, IProgress<string>? progress, CancellationToken ct)
    {
        var patterns = (job.ForceCopyFiles ?? new())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

        var empty = new Dictionary<string, string>();

        if (patterns.Count == 0)
            return new ForceCopyPlan(Array.Empty<string>(), empty, job.ForceCopySmart);

        if (!job.ForceCopySmart)
            return new ForceCopyPlan(patterns, empty, false);

        var source = (job.Source ?? "").Trim();
        if (!Directory.Exists(source))
            return new ForceCopyPlan(Array.Empty<string>(), empty, true);

        var stored = _store.Load(job.Name);
        var newHashes = new Dictionary<string, string>();
        var changed = new List<string>();

        foreach (var file in EnumerateMatches(source, patterns))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"[hash] {Path.GetFileName(file)}");
            var hash = await ComputeHashAsync(file, ct).ConfigureAwait(false);
            newHashes[file] = hash;

            if (!stored.TryGetValue(file, out var old) ||
                !string.Equals(old, hash, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(Path.GetFileName(file));
            }
        }

        return new ForceCopyPlan(changed.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), newHashes, true);
    }

    /// <summary>Salva gli hash dopo una copia riuscita.</summary>
    public void Commit(string jobName, IReadOnlyDictionary<string, string> newHashes) =>
        _store.Save(jobName, newHashes);

    private static IEnumerable<string> EnumerateMatches(string source, IEnumerable<string> patterns)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(source, pattern, SearchOption.AllDirectories); }
            catch { continue; }
            foreach (var f in files)
                set.Add(f);
        }
        return set;
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var bytes = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(bytes);
    }
}
```

- [ ] **Step 4: Esegui e verifica successo**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~ForceCopyPlannerTests"`
Atteso: PASS (5 test).

- [ ] **Step 5: Commit**

```bash
git add src/RobocopySW.Core/Services/ForceCopyPlanner.cs src/RobocopySW.Tests/ForceCopyPlannerTests.cs
git commit -m "feat: ForceCopyPlanner con rilevamento modifiche via SHA256"
```

---

### Task 5: Esecuzione — due passate in RobocopyRunner

**Files:**
- Modify: `src/RobocopySW.Core/Services/RobocopyRunner.cs`
- Test: `src/RobocopySW.Tests/RobocopyRunnerIntegrationTests.cs`

- [ ] **Step 1: Scrivi il test di integrazione**

Aggiungi in `RobocopyRunnerIntegrationTests.cs`, prima della `}` finale della classe:

```csharp
    [Fact]
    public async Task ForceCopy_Simple_ReCopiesOtherwiseSkippedFile()
    {
        var hashPath = Path.Combine(_base, "hashes.json");
        var planner = new ForceCopyPlanner(new ForceCopyHashStore(hashPath));
        var runner = new RobocopyRunner(null, planner);

        var job = Job(mirror: true);
        job.ForceCopyFiles = new() { "a.txt" }; // modalità semplice (ForceCopySmart = false)

        // Primo backup: copia tutto.
        var first = await runner.RunAsync(job);
        Assert.True(first.Result.Success);

        // Secondo backup: la passata normale salterebbe a.txt (identico),
        // ma la passata "forza copia" lo ricopia comunque.
        var second = await runner.RunAsync(job);
        Assert.True(second.Result.Success);
        Assert.True(second.Result.FilesCopied >= 1);
    }
```

- [ ] **Step 2: Esegui e verifica fallimento**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~RobocopyRunnerIntegrationTests.ForceCopy_Simple_ReCopiesOtherwiseSkippedFile"`
Atteso: FAIL di compilazione (`RobocopyRunner` non ha il costruttore con `ForceCopyPlanner`).

- [ ] **Step 3: Riscrivi RobocopyRunner con le due passate**

Sostituisci **interamente** il contenuto di `src/RobocopySW.Core/Services/RobocopyRunner.cs` con:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>Risultato grezzo di un'esecuzione robocopy: esito + output testuale completo.</summary>
public sealed class RobocopyRunResult
{
    public required JobResult Result { get; init; }
    public required string Output { get; init; }
}

/// <summary>
/// Esegue robocopy come processo esterno, catturando l'output in tempo reale, e ne ricava un
/// <see cref="JobResult"/>. Se il job ha una lista "Forza copia", esegue una seconda passata
/// (<see cref="RobocopyArgsBuilder.BuildForceCopyPass"/>) e aggrega conteggi ed exit code.
/// </summary>
public sealed class RobocopyRunner
{
    private readonly string _robocopyPath;
    private readonly ForceCopyPlanner? _forceCopyPlanner;

    private static readonly Encoding OemEncoding = ResolveOemEncoding();

    public RobocopyRunner(string? robocopyPath = null, ForceCopyPlanner? forceCopyPlanner = null)
    {
        _robocopyPath = robocopyPath ?? Path.Combine(Environment.SystemDirectory, "Robocopy.exe");
        _forceCopyPlanner = forceCopyPlanner;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    private static Encoding ResolveOemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding((int)GetOEMCP());
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    public async Task<RobocopyRunResult> RunAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTime.Now;

        // Passata principale (mirror/copia normale): comportamento invariato.
        var (exit1, text1) = await RunPassAsync(RobocopyArgsBuilder.Build(job, dryRun), progress, ct)
            .ConfigureAwait(false);
        var fullText = new StringBuilder(text1);
        var counts = RobocopyOutputParser.ParseCounts(text1.Split('\n'));
        var exitCombined = exit1;

        // Passata "Forza copia": solo se la lista è valorizzata e c'è un pianificatore.
        if (job.ForceCopyFiles is { Count: > 0 } && _forceCopyPlanner is not null)
        {
            var plan = await _forceCopyPlanner.PlanAsync(job, progress, ct).ConfigureAwait(false);
            if (plan.Filters.Count > 0)
            {
                var pass2Args = RobocopyArgsBuilder.BuildForceCopyPass(job, plan.Filters, dryRun);
                var (exit2, text2) = await RunPassAsync(pass2Args, progress, ct).ConfigureAwait(false);
                fullText.AppendLine().Append(text2);
                exitCombined |= exit2; // gli exit code robocopy sono bitfield: l'OR preserva l'esito peggiore

                var c2 = RobocopyOutputParser.ParseCounts(text2.Split('\n'));
                counts = new RobocopyCounts(
                    counts.DirsCopied + c2.DirsCopied,
                    counts.FilesCopied + c2.FilesCopied,
                    counts.FilesSkipped + c2.FilesSkipped,
                    counts.FilesFailed + c2.FilesFailed,
                    counts.FilesExtra + c2.FilesExtra,
                    counts.DirsFailed + c2.DirsFailed);

                // Aggiorna gli hash salvati solo a passata forzata riuscita (così un errore = ricopia al prossimo run).
                if (!dryRun && plan.Smart && ExitCodeInterpreter.Interpret(exit2).Success)
                    _forceCopyPlanner.Commit(job.Name, plan.NewHashes);
            }
        }

        var interpreted = ExitCodeInterpreter.Interpret(exitCombined);
        var text = fullText.ToString();

        var result = new JobResult
        {
            JobName = job.Name,
            ExitCode = exitCombined,
            Success = interpreted.Success,
            Status = interpreted.Summary,
            StartedAt = started,
            Duration = DateTime.Now - started,
            DryRun = dryRun,
            DirsCopied = counts.DirsCopied,
            FilesCopied = counts.FilesCopied,
            FilesSkipped = counts.FilesSkipped,
            FilesExtra = counts.FilesExtra,
            FilesFailed = counts.FilesFailed,
            DirsFailed = counts.DirsFailed,
        };

        return new RobocopyRunResult { Result = result, Output = text };
    }

    /// <summary>Avvia un singolo processo robocopy e restituisce (exit code, output catturato).</summary>
    private async Task<(int ExitCode, string Output)> RunPassAsync(
        IReadOnlyList<string> args, IProgress<string>? progress, CancellationToken ct)
    {
        var output = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = _robocopyPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = OemEncoding,
            StandardErrorEncoding = OemEncoding,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };

        void OnData(string? line)
        {
            if (line is null) return;
            lock (output) output.AppendLine(line);
            progress?.Report(line);
        }

        process.OutputDataReceived += (_, e) => OnData(e.Data);
        process.ErrorDataReceived += (_, e) => OnData(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* il processo potrebbe essere già terminato */ }
        }))
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }

        return (process.ExitCode, output.ToString());
    }
}
```

- [ ] **Step 4: Esegui i test del runner e verifica successo**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~RobocopyRunnerIntegrationTests"`
Atteso: PASS (tutti i test esistenti + il nuovo).

- [ ] **Step 5: Commit**

```bash
git add src/RobocopySW.Core/Services/RobocopyRunner.cs src/RobocopySW.Tests/RobocopyRunnerIntegrationTests.cs
git commit -m "feat: seconda passata Forza copia in RobocopyRunner con aggregazione"
```

---

### Task 6: Wiring — AppHost costruisce planner e store

**Files:**
- Modify: `src/RobocopySW/AppHost.cs`

- [ ] **Step 1: Aggiungi il campo store e collega il planner**

In `AppHost.cs`:

1. Aggiungi una proprietà dopo `Results` (riga ~15):

```csharp
    public ForceCopyHashStore ForceCopyHashes { get; }
```

2. Nel costruttore privato, dopo l'assegnazione di `Results` (riga ~22), aggiungi:

```csharp
        ForceCopyHashes = new ForceCopyHashStore(Path.Combine(store.DirectoryPath, "forcecopy-hashes.json"));
```

3. In `BuildRunner()`, sostituisci la riga `var runner = new RobocopyRunner();` con:

```csharp
        var planner = new ForceCopyPlanner(ForceCopyHashes);
        var runner = new RobocopyRunner(forceCopyPlanner: planner);
```

- [ ] **Step 2: Build**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori.

- [ ] **Step 3: Commit**

```bash
git add src/RobocopySW/AppHost.cs
git commit -m "feat: AppHost collega ForceCopyPlanner e hash store al runner"
```

---

### Task 7: Localizzazione — 5 chiavi × 5 lingue

**Files:**
- Modify: `src/RobocopySW/Localization/Loc.cs`

- [ ] **Step 1: Aggiungi le chiavi in ciascun blocco lingua**

In `Loc.cs`, subito **dopo** la riga `["Editor_ExcludeDirsTip"] = ...` in **ognuna** delle 5 lingue (righe ~160, ~315, ~470, ~625, ~780), inserisci il blocco corrispondente.

Italiano (dopo riga ~160):

```csharp
        ["Editor_ForceCopy"] = "Forza copia (ignora data/dimensione)",
        ["Editor_ForceCopyTip"] = "File ricopiati anche se data e dimensione non cambiano (es. container, DB, archivi .pst). Uno per riga, per nome o estensione. Vengono ricopiati interi a ogni backup, salvo attivare l'opzione qui sotto.",
        ["Editor_ForceCopySmart"] = "Copia solo se il contenuto è cambiato (calcola hash; più lento in lettura)",
        ["Editor_ForceCopySmartTip"] = "Con questa opzione l'app calcola un hash SHA256 di ogni file in elenco e lo ricopia solo se è davvero cambiato dall'ultimo backup. Utile per file grandi che cambiano di rado; ogni backup deve però leggerli per intero.",
        ["Editor_ForceCopyPreviewSmart"] = "<file modificati>",
```

English (dopo riga ~315):

```csharp
        ["Editor_ForceCopy"] = "Force copy (ignore date/size)",
        ["Editor_ForceCopyTip"] = "Files re-copied even if date and size don't change (e.g. containers, DBs, .pst archives). One per line, by name or extension. They are copied in full on every backup unless you enable the option below.",
        ["Editor_ForceCopySmart"] = "Copy only if content changed (computes hash; slower to read)",
        ["Editor_ForceCopySmartTip"] = "With this option the app computes a SHA256 hash of each listed file and re-copies it only if it actually changed since the last backup. Useful for large files that change rarely; every backup must read them in full.",
        ["Editor_ForceCopyPreviewSmart"] = "<changed files>",
```

Español (dopo riga ~470):

```csharp
        ["Editor_ForceCopy"] = "Forzar copia (ignorar fecha/tamaño)",
        ["Editor_ForceCopyTip"] = "Archivos que se vuelven a copiar aunque la fecha y el tamaño no cambien (p. ej. contenedores, BD, archivos .pst). Uno por línea, por nombre o extensión. Se copian enteros en cada copia de seguridad salvo que actives la opción de abajo.",
        ["Editor_ForceCopySmart"] = "Copiar solo si el contenido ha cambiado (calcula hash; lectura más lenta)",
        ["Editor_ForceCopySmartTip"] = "Con esta opción la app calcula un hash SHA256 de cada archivo de la lista y solo lo vuelve a copiar si ha cambiado realmente desde la última copia. Útil para archivos grandes que cambian poco; cada copia debe leerlos enteros.",
        ["Editor_ForceCopyPreviewSmart"] = "<archivos modificados>",
```

Français (dopo riga ~625):

```csharp
        ["Editor_ForceCopy"] = "Forcer la copie (ignorer date/taille)",
        ["Editor_ForceCopyTip"] = "Fichiers recopiés même si la date et la taille ne changent pas (ex. conteneurs, BdD, archives .pst). Un par ligne, par nom ou extension. Ils sont copiés en entier à chaque sauvegarde, sauf si vous activez l'option ci-dessous.",
        ["Editor_ForceCopySmart"] = "Copier seulement si le contenu a changé (calcule un hachage ; lecture plus lente)",
        ["Editor_ForceCopySmartTip"] = "Avec cette option, l'app calcule un hachage SHA256 de chaque fichier de la liste et ne le recopie que s'il a réellement changé depuis la dernière sauvegarde. Utile pour les gros fichiers qui changent rarement ; chaque sauvegarde doit toutefois les lire en entier.",
        ["Editor_ForceCopyPreviewSmart"] = "<fichiers modifiés>",
```

Deutsch (dopo riga ~780):

```csharp
        ["Editor_ForceCopy"] = "Kopie erzwingen (Datum/Größe ignorieren)",
        ["Editor_ForceCopyTip"] = "Dateien, die auch dann neu kopiert werden, wenn Datum und Größe gleich bleiben (z. B. Container, DBs, .pst-Archive). Eine pro Zeile, nach Name oder Endung. Sie werden bei jeder Sicherung vollständig kopiert, sofern Sie die Option unten nicht aktivieren.",
        ["Editor_ForceCopySmart"] = "Nur kopieren, wenn sich der Inhalt geändert hat (berechnet Hash; langsameres Lesen)",
        ["Editor_ForceCopySmartTip"] = "Mit dieser Option berechnet die App einen SHA256-Hash jeder aufgelisteten Datei und kopiert sie nur neu, wenn sie sich seit der letzten Sicherung tatsächlich geändert hat. Nützlich für große Dateien, die sich selten ändern; jede Sicherung muss sie jedoch vollständig lesen.",
        ["Editor_ForceCopyPreviewSmart"] = "<geänderte Dateien>",
```

- [ ] **Step 2: Build (verifica parità chiavi)**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori. (Se esiste un test di parità delle chiavi di localizzazione, eseguilo: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~Loc"`.)

- [ ] **Step 3: Commit**

```bash
git add src/RobocopySW/Localization/Loc.cs
git commit -m "i18n: chiavi Forza copia in 5 lingue"
```

---

### Task 8: ViewModel — proprietà e anteprima

**Files:**
- Modify: `src/RobocopySW/ViewModels/JobEditorViewModel.cs`

- [ ] **Step 1: Aggiungi le proprietà**

In `JobEditorViewModel.cs`, subito dopo la proprietà `ExcludeDirsText` (riga ~128), aggiungi:

```csharp
    /// <summary>Pattern "Forza copia", uno per riga (es. <c>*.pst</c>).</summary>
    public string ForceCopyFilesText
    {
        get => string.Join(Environment.NewLine, _job.ForceCopyFiles);
        set
        {
            _job.ForceCopyFiles = SplitLines(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasForceCopy));
            RaisePreview();
        }
    }

    /// <summary>Modalità smart (copia solo se l'hash è cambiato).</summary>
    public bool ForceCopySmart
    {
        get => _job.ForceCopySmart;
        set { _job.ForceCopySmart = value; OnPropertyChanged(); RaisePreview(); }
    }

    /// <summary>true se la lista "Forza copia" contiene almeno un pattern (abilita la spunta smart).</summary>
    public bool HasForceCopy => _job.ForceCopyFiles.Count > 0;
```

- [ ] **Step 2: Aggiorna l'anteprima del comando**

Nella proprietà `CommandPreview`, sostituisci il corpo del blocco `try` con:

```csharp
            try
            {
                var preview = RobocopyArgsBuilder.ToDisplayString(RobocopyArgsBuilder.Build(_job));
                if (_job.ForceCopyFiles.Count > 0)
                {
                    var filters = _job.ForceCopySmart
                        ? new List<string> { Loc.Instance["Editor_ForceCopyPreviewSmart"] }
                        : _job.ForceCopyFiles;
                    preview += Environment.NewLine +
                        RobocopyArgsBuilder.ToDisplayString(RobocopyArgsBuilder.BuildForceCopyPass(_job, filters));
                }
                return preview;
            }
            catch (Exception ex)
            {
                return "(" + ex.Message + ")";
            }
```

- [ ] **Step 3: Build**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori.

- [ ] **Step 4: Commit**

```bash
git add src/RobocopySW/ViewModels/JobEditorViewModel.cs
git commit -m "feat: ViewModel Forza copia (lista, smart, anteprima seconda passata)"
```

---

### Task 9: UI — riquadro Forza copia nell'editor

**Files:**
- Modify: `src/RobocopySW/JobEditorWindow.xaml`

- [ ] **Step 1: Inserisci il riquadro**

In `JobEditorWindow.xaml`, tra la chiusura del `Grid` delle esclusioni (riga ~143, subito dopo `</Grid>`) e il `TextBlock` dell'anteprima (`Text="{l:Tr Editor_CommandPreview}"`, riga ~145), inserisci:

```xml
                <ui:Card Margin="0,12,0,0" Padding="14">
                    <StackPanel>
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{l:Tr Editor_ForceCopy}" Style="{StaticResource Label}"/>
                            <ctl:InfoHint Margin="0,12,0,0" Text="{l:Tr Editor_ForceCopyTip}"/>
                        </StackPanel>
                        <ui:TextBox Text="{Binding ForceCopyFilesText, UpdateSourceTrigger=PropertyChanged}"
                                 AcceptsReturn="True" Height="64" TextWrapping="NoWrap"
                                 VerticalScrollBarVisibility="Auto" PlaceholderText="*.pst"/>
                        <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                            <CheckBox Content="{l:Tr Editor_ForceCopySmart}" IsChecked="{Binding ForceCopySmart}"
                                      IsEnabled="{Binding HasForceCopy}"/>
                            <ctl:InfoHint Margin="6,0,0,0" Text="{l:Tr Editor_ForceCopySmartTip}"/>
                        </StackPanel>
                    </StackPanel>
                </ui:Card>
```

- [ ] **Step 2: Build**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori.

- [ ] **Step 3: Commit**

```bash
git add src/RobocopySW/JobEditorWindow.xaml
git commit -m "feat: riquadro Forza copia nell'editor job"
```

---

### Task 10: Verifica finale (build + tutti i test + smoke manuale)

**Files:** nessuno (verifica).

- [ ] **Step 1: Build pulita con app chiusa**

Assicurati che `RobocopySW.exe` non sia in esecuzione, poi:
Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori, 0 warning.

- [ ] **Step 2: Tutti i test**

Run: `dotnet test src/RobocopySW.sln --nologo`
Atteso: tutti PASS (66 esistenti + i nuovi).

- [ ] **Step 3: Smoke manuale (a cura dell'utente)**

Verifica end-to-end suggerita:
1. Crea due cartelle di prova (sorgente/destinazione) con un file `prova.dat`.
2. Nell'editor job aggiungi `*.dat` nella lista "Forza copia", modalità **semplice**; salva.
3. Esegui il backup due volte: alla seconda, nonostante il file sia identico, deve risultare copiato (la seconda passata lo forza). Un file non in lista resta saltato.
4. Attiva **smart**, esegui: primo backup copia il file (hash nuovo), secondo senza modifiche non lo copia; modifica il contenuto del file (anche a parità di dimensione) e riesegui → torna a copiarlo.
5. Svuota la lista → nessuna seconda passata, comportamento identico a prima.

- [ ] **Step 4: Commit finale (solo se servono ritocchi)**

Nessun commit se i passi precedenti sono già committati. In caso di fix:

```bash
git add -A
git commit -m "fix: ritocchi finali feature Forza copia"
```

---

## Self-Review

**Copertura spec:**
- Modello `ForceCopyFiles` + `ForceCopySmart` → Task 1. ✅
- `BuildForceCopyPass` (/IS /IT, no /MIR, no /XO) → Task 2. ✅
- `ForceCopyHashStore` (persistenza accanto a lastresults) → Task 3 + wiring Task 6. ✅
- `ForceCopyPlanner` (semplice/smart, SHA256 streaming, enumerazione pattern) → Task 4. ✅
- Runner due passate + somma conteggi + OR exit code + commit hash a successo → Task 5. ✅
- Wiring AppHost → Task 6. ✅
- 5 chiavi × 5 lingue → Task 7. ✅
- ViewModel (testi + preview seconda passata) → Task 8. ✅
- UI riquadro + spunta smart abilitata solo con lista non vuota → Task 9. ✅
- Verifica end-to-end → Task 10. ✅

**Coerenza tipi/firme:** `BuildForceCopyPass(BackupJob, IReadOnlyList<string>, bool, string?)`, `ForceCopyPlanner.PlanAsync(BackupJob, IProgress<string>?, CancellationToken) → Task<ForceCopyPlan>`, `ForceCopyPlan(Filters, NewHashes, Smart)`, `ForceCopyHashStore.Load(string)`/`Save(string, IReadOnlyDictionary<string,string>)`, `RobocopyRunner(string?, ForceCopyPlanner?)` — usati coerentemente in tutti i task. `RobocopyCounts` ha 6 campi `long` (DirsCopied, FilesCopied, FilesSkipped, FilesFailed, FilesExtra, DirsFailed) — la somma in Task 5 rispetta l'ordine del costruttore.

**Limitazione nota (documentata):** in modalità smart i filtri della seconda passata sono **nomi file** (non percorsi): due file omonimi in cartelle diverse, se uno cambia, possono essere entrambi ricopiati. Effetto: copia in più innocua, correttezza preservata.
