# Creazione guidata nuovo job (wizard) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere una creazione guidata a passi multipli per i nuovi job: poche domande con spiegazioni che derivano le opzioni robocopy ottimali e precompilano l'editor esistente.

**Architecture:** La logica risposte→`BackupJob` è una funzione pura testabile (`JobWizardPlanner`) nel progetto Core. La UI è una `JobWizardWindow` WPF a 5 passi (TabControl con intestazioni nascoste) guidata da un `JobWizardViewModel`. Il pulsante "Nuovo" della MainWindow apre il wizard; alla conferma costruisce un `BackupJob` e apre il normale editor precompilato. "Salta" apre l'editor vuoto (comportamento odierno).

**Tech Stack:** .NET 10 (net10.0-windows), C#, xUnit, WPF + WPF-UI (FluentWindow), MVVM (ObservableObject in `RobocopySW.Infra`).

---

## File Structure

- **Crea** `src/RobocopySW.Core/Models/StorageKind.cs` — enum SSD/HDD/USB/Rete.
- **Crea** `src/RobocopySW.Core/Models/JobWizardAnswers.cs` — input puro del planner.
- **Crea** `src/RobocopySW.Core/Services/JobWizardPlanner.cs` — risposte → `BackupJob` (puro).
- **Crea** `src/RobocopySW.Tests/JobWizardPlannerTests.cs` — test del planner.
- **Modifica** `src/RobocopySW/Localization/Loc.cs` — chiavi `Wiz_*` (5 lingue).
- **Crea** `src/RobocopySW/ViewModels/JobWizardViewModel.cs` — risposte + navigazione + anteprima.
- **Crea** `src/RobocopySW/JobWizardWindow.xaml` (+ `.xaml.cs`) — la finestra a 5 passi.
- **Modifica** `src/RobocopySW/MainWindow.xaml.cs` — `OnNewJob` apre il wizard.

**Nota architettura test:** il progetto `RobocopySW.Tests` referenzia solo `RobocopySW.Core`, non il progetto WPF. Quindi i test automatici coprono il **planner** (dove sta la logica). ViewModel/XAML/integrazione si verificano con build + smoke manuale, come già fanno l'editor e i suoi ViewModel.

**Comandi comuni:**
- Build: `dotnet build src/RobocopySW.sln -c Debug --nologo`
- Tutti i test: `dotnet test src/RobocopySW.sln --nologo`
- Test filtrati: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~JobWizardPlannerTests"`

**Nota commit:** messaggi senza virgolette doppie; terminare con `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. (Con la Bash tool gli apici singoli vanno bene; non raddoppiarli.)

---

### Task 1: Core — StorageKind, JobWizardAnswers, JobWizardPlanner

**Files:**
- Create: `src/RobocopySW.Core/Models/StorageKind.cs`
- Create: `src/RobocopySW.Core/Models/JobWizardAnswers.cs`
- Create: `src/RobocopySW.Core/Services/JobWizardPlanner.cs`
- Test: `src/RobocopySW.Tests/JobWizardPlannerTests.cs`

- [ ] **Step 1: Scrivi i test**

Crea `src/RobocopySW.Tests/JobWizardPlannerTests.cs`:

```csharp
using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

namespace RobocopySW.Tests;

public class JobWizardPlannerTests
{
    private static JobWizardAnswers Base() => new()
    {
        Name = "J",
        Source = @"C:\src",
        Destination = @"D:\dst",
    };

    [Theory]
    [InlineData(StorageKind.Ssd, StorageKind.Ssd, 16)]
    [InlineData(StorageKind.Ssd, StorageKind.Hdd, 2)]
    [InlineData(StorageKind.Ssd, StorageKind.Usb, 4)]
    [InlineData(StorageKind.Ssd, StorageKind.Network, 8)]
    [InlineData(StorageKind.Network, StorageKind.Network, 8)]
    [InlineData(StorageKind.Hdd, StorageKind.Network, 2)]
    public void RecommendedThreads_TakesMinOfBothEnds(StorageKind src, StorageKind dst, int expected)
    {
        Assert.Equal(expected, JobWizardPlanner.RecommendedThreads(src, dst));
    }

    [Fact]
    public void BuildJob_CopiesNameSourceDestination_Trimmed()
    {
        var a = Base();
        a.Name = "  Documenti  ";
        a.Source = @"  C:\s  ";
        a.Destination = @"  D:\d  ";
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Equal("Documenti", job.Name);
        Assert.Equal(@"C:\s", job.Source);
        Assert.Equal(@"D:\d", job.Destination);
    }

    [Fact]
    public void BuildJob_Mirror_MapsToMirrorFlag()
    {
        var a = Base(); a.Mirror = true;
        Assert.True(JobWizardPlanner.BuildJob(a).Mirror);
        a.Mirror = false;
        Assert.False(JobWizardPlanner.BuildJob(a).Mirror);
    }

    [Fact]
    public void BuildJob_LargeFiles_EnablesRestartable_NotUnbuffered()
    {
        var a = Base(); a.HasLargeFiles = true;
        var job = JobWizardPlanner.BuildJob(a);
        Assert.True(job.Restartable);
        Assert.False(job.UnbufferedIO);
    }

    [Fact]
    public void BuildJob_NoLargeFiles_NoRestartableNoUnbuffered()
    {
        var job = JobWizardPlanner.BuildJob(Base());
        Assert.False(job.Restartable);
        Assert.False(job.UnbufferedIO);
    }

    [Fact]
    public void BuildJob_Permissions_MapsToCopyAll()
    {
        var a = Base(); a.PreservePermissions = true;
        Assert.True(JobWizardPlanner.BuildJob(a).CopyAll);
        a.PreservePermissions = false;
        Assert.False(JobWizardPlanner.BuildJob(a).CopyAll);
    }

    [Fact]
    public void BuildJob_Network_RaisesRetriesAndWait()
    {
        var a = Base(); a.DestStorage = StorageKind.Network;
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Equal(3, job.Retries);
        Assert.Equal(10, job.Wait);

        var local = JobWizardPlanner.BuildJob(Base());
        Assert.Equal(1, local.Retries);
        Assert.Equal(5, local.Wait);
    }

    [Fact]
    public void BuildJob_FrozenPatterns_FillForceCopy_SmartStaysOff()
    {
        var a = Base();
        a.FrozenMetadataPatterns = new() { "*.vc", "  ", "db.dat" };
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Equal(new[] { "*.vc", "db.dat" }, job.ForceCopyFiles);
        Assert.False(job.ForceCopySmart);
    }

    [Fact]
    public void BuildJob_ExcludeCommonTemp_FillsExcludeLists()
    {
        var a = Base(); a.ExcludeCommonTemp = true;
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Contains("cache", job.ExcludeDirs);
        Assert.Contains("node_modules", job.ExcludeDirs);
        Assert.Contains("*.tmp", job.ExcludeFiles);

        var none = JobWizardPlanner.BuildJob(Base());
        Assert.Empty(none.ExcludeDirs);
        Assert.Empty(none.ExcludeFiles);
    }
}
```

- [ ] **Step 2: Esegui e verifica fallimento**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~JobWizardPlannerTests"`
Atteso: FAIL di compilazione (StorageKind / JobWizardAnswers / JobWizardPlanner non esistono).

- [ ] **Step 3: Crea l'enum**

Crea `src/RobocopySW.Core/Models/StorageKind.cs`:

```csharp
namespace RobocopySW.Core.Models;

/// <summary>Tipo di supporto di una sorgente/destinazione, usato dalla creazione guidata
/// per consigliare le opzioni robocopy (soprattutto /MT). L'ordine corrisponde alle voci
/// della ComboBox del wizard.</summary>
public enum StorageKind
{
    Ssd,
    Hdd,
    Usb,
    Network,
}
```

- [ ] **Step 4: Crea il modello delle risposte**

Crea `src/RobocopySW.Core/Models/JobWizardAnswers.cs`:

```csharp
namespace RobocopySW.Core.Models;

/// <summary>Risposte della creazione guidata di un job: input puro per <see cref="Services.JobWizardPlanner"/>.</summary>
public sealed class JobWizardAnswers
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public StorageKind SourceStorage { get; set; } = StorageKind.Ssd;
    public StorageKind DestStorage { get; set; } = StorageKind.Ssd;
    public bool Mirror { get; set; } = true;
    public bool HasLargeFiles { get; set; }
    public List<string> FrozenMetadataPatterns { get; set; } = new();
    public bool PreservePermissions { get; set; }
    public bool ExcludeCommonTemp { get; set; }
}
```

- [ ] **Step 5: Crea il planner**

Crea `src/RobocopySW.Core/Services/JobWizardPlanner.cs`:

```csharp
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>Traduce le risposte della creazione guidata in un <see cref="BackupJob"/> (funzione pura).</summary>
public static class JobWizardPlanner
{
    /// <summary>Thread /MT consigliati = minimo tra sorgente e destinazione
    /// (HDD=2, USB=4, Rete=8, SSD=16): un HDD coinvolto abbassa sempre il parallelismo.</summary>
    public static int RecommendedThreads(StorageKind source, StorageKind dest) =>
        Math.Min(Rank(source), Rank(dest));

    private static int Rank(StorageKind kind) => kind switch
    {
        StorageKind.Hdd => 2,
        StorageKind.Usb => 4,
        StorageKind.Network => 8,
        StorageKind.Ssd => 16,
        _ => 8,
    };

    public static BackupJob BuildJob(JobWizardAnswers a)
    {
        ArgumentNullException.ThrowIfNull(a);

        var onNetwork = a.SourceStorage == StorageKind.Network || a.DestStorage == StorageKind.Network;

        var job = new BackupJob
        {
            Name = (a.Name ?? "").Trim(),
            Source = (a.Source ?? "").Trim(),
            Destination = (a.Destination ?? "").Trim(),
            Mirror = a.Mirror,
            MultiThread = RecommendedThreads(a.SourceStorage, a.DestStorage),
            Restartable = a.HasLargeFiles,
            UnbufferedIO = false,
            CopyAll = a.PreservePermissions,
            Retries = onNetwork ? 3 : 1,
            Wait = onNetwork ? 10 : 5,
            ForceCopyFiles = (a.FrozenMetadataPatterns ?? new())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList(),
            ForceCopySmart = false,
            Enabled = true,
        };

        if (a.ExcludeCommonTemp)
        {
            job.ExcludeDirs = new List<string> { "cache", "tmp", "Temp", "node_modules" };
            job.ExcludeFiles = new List<string> { "*.tmp", "~$*" };
        }

        return job;
    }
}
```

- [ ] **Step 6: Esegui e verifica successo**

Run: `dotnet test src/RobocopySW.sln --nologo --filter "FullyQualifiedName~JobWizardPlannerTests"`
Atteso: PASS (tutti i casi, incluse le 6 righe Theory).

- [ ] **Step 7: Commit**

```bash
git add src/RobocopySW.Core/Models/StorageKind.cs src/RobocopySW.Core/Models/JobWizardAnswers.cs src/RobocopySW.Core/Services/JobWizardPlanner.cs src/RobocopySW.Tests/JobWizardPlannerTests.cs
git commit -m "feat: JobWizardPlanner (risposte guidate -> BackupJob)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: Localizzazione — chiavi Wiz_* (5 lingue)

**Files:**
- Modify: `src/RobocopySW/Localization/Loc.cs`

- [ ] **Step 1: Aggiungi le chiavi in ciascun blocco lingua**

In `Loc.cs`, ogni lingua è un `Dictionary<string,string>`. Aggiungi le 28 chiavi `Wiz_*` in **ognuna** delle 5 lingue (Italiano, English, Español, Français, Deutsch). Inseriscile subito dopo la riga `["Editor_ExcludeDirsTip"] = ...;` di ciascuna lingua (le 5 occorrenze già usate per le chiavi `Editor_ForceCopy*`; inserisci dopo quel blocco).

**Valori canonici Italiano (1ª occorrenza):**

```csharp
        ["Wiz_Title"] = "Nuovo job — creazione guidata",
        ["Wiz_Step"] = "Passo {0} di {1}",
        ["Wiz_Back"] = "Indietro",
        ["Wiz_Next"] = "Avanti",
        ["Wiz_Open"] = "Apri nell'editor",
        ["Wiz_Skip"] = "Salta e configura a mano",
        ["Wiz_Step1Title"] = "Dati di base",
        ["Wiz_Step2Title"] = "Tipo di dischi",
        ["Wiz_Step3Title"] = "Comportamento",
        ["Wiz_Step4Title"] = "Casi speciali",
        ["Wiz_Step5Title"] = "Riepilogo",
        ["Wiz_SourceKind"] = "Tipo della sorgente",
        ["Wiz_DestKind"] = "Tipo della destinazione",
        ["Wiz_KindHint"] = "Con un disco fisso (HDD) pochi thread vanno più veloci; SSD e rete reggono più parallelismo.",
        ["Wiz_Ssd"] = "SSD / NVMe",
        ["Wiz_Hdd"] = "Disco fisso (HDD)",
        ["Wiz_Usb"] = "USB esterno",
        ["Wiz_Network"] = "Share di rete",
        ["Wiz_SyncQuestion"] = "La destinazione deve…",
        ["Wiz_SyncMirror"] = "Rispecchiare la sorgente (elimina anche i file rimossi)",
        ["Wiz_SyncAccumulate"] = "Accumulare (non cancella mai nulla)",
        ["Wiz_Large"] = "Ci sono file molto grandi (VM, container, archivi multi-GB)",
        ["Wiz_Frozen"] = "Ho file il cui contenuto cambia ma data e dimensione restano uguali",
        ["Wiz_FrozenHint"] = "Es. container VeraCrypt o database. Se è VeraCrypt, conviene disattivare il flag che conserva la data del container invece di ricopiarlo ogni volta.",
        ["Wiz_FrozenPatterns"] = "Pattern (uno per riga, es. *.vc)",
        ["Wiz_Perms"] = "Conserva permessi/ACL e proprietari dei file",
        ["Wiz_PermsHint"] = "Serve per ripristinare su un altro PC/server; può causare accesso negato nelle ricopie.",
        ["Wiz_Excl"] = "Escludi cartelle cache e file temporanei comuni",
        ["Wiz_Summary"] = "Cosa verrà impostato",
        ["Wiz_NetCredNote"] = "Sorgente o destinazione in rete: ricordati di impostare la credenziale nell'editor.",
```

**Valori English (2ª occorrenza):**

```csharp
        ["Wiz_Title"] = "New job — guided setup",
        ["Wiz_Step"] = "Step {0} of {1}",
        ["Wiz_Back"] = "Back",
        ["Wiz_Next"] = "Next",
        ["Wiz_Open"] = "Open in editor",
        ["Wiz_Skip"] = "Skip and configure manually",
        ["Wiz_Step1Title"] = "Basics",
        ["Wiz_Step2Title"] = "Disk types",
        ["Wiz_Step3Title"] = "Behavior",
        ["Wiz_Step4Title"] = "Special cases",
        ["Wiz_Step5Title"] = "Summary",
        ["Wiz_SourceKind"] = "Source type",
        ["Wiz_DestKind"] = "Destination type",
        ["Wiz_KindHint"] = "With a spinning disk (HDD) fewer threads are faster; SSD and network handle more parallelism.",
        ["Wiz_Ssd"] = "SSD / NVMe",
        ["Wiz_Hdd"] = "Hard disk (HDD)",
        ["Wiz_Usb"] = "External USB",
        ["Wiz_Network"] = "Network share",
        ["Wiz_SyncQuestion"] = "The destination should…",
        ["Wiz_SyncMirror"] = "Mirror the source (also deletes removed files)",
        ["Wiz_SyncAccumulate"] = "Accumulate (never deletes anything)",
        ["Wiz_Large"] = "There are very large files (VMs, containers, multi-GB archives)",
        ["Wiz_Frozen"] = "I have files whose content changes but date and size stay the same",
        ["Wiz_FrozenHint"] = "E.g. VeraCrypt containers or databases. For VeraCrypt, prefer disabling the container timestamp-preserve option instead of re-copying it every time.",
        ["Wiz_FrozenPatterns"] = "Patterns (one per line, e.g. *.vc)",
        ["Wiz_Perms"] = "Preserve file permissions/ACLs and owners",
        ["Wiz_PermsHint"] = "Needed when restoring to another PC/server; can cause access-denied on re-copies.",
        ["Wiz_Excl"] = "Exclude common cache folders and temporary files",
        ["Wiz_Summary"] = "What will be set",
        ["Wiz_NetCredNote"] = "Source or destination on the network: remember to set the credential in the editor.",
```

**Español, Français, Deutsch (3ª/4ª/5ª occorrenza):** aggiungi le stesse 28 chiavi tradotte nella rispettiva lingua, coerenti con lo stile delle voci `Editor_*` già presenti in quei blocchi (registro, terminologia robocopy invariata come `/MT`, `*.vc`). Mantieni i segnaposto `{0}`/`{1}` in `Wiz_Step`. Salva il file in **UTF-8** preservando gli accenti.

- [ ] **Step 2: Build + verifica parità chiavi**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori.

Verifica che ogni chiave compaia **5 volte** (una per lingua). Per ciascuna chiave esegui un controllo, es.:
Run: `grep -c "\"Wiz_Title\"" src/RobocopySW/Localization/Loc.cs` → atteso `5`.
Ripeti per `Wiz_NetCredNote`, `Wiz_KindHint`, `Wiz_SyncMirror` (campione). Se un conteggio ≠ 5, completa la lingua mancante.

- [ ] **Step 3: Commit**

```bash
git add src/RobocopySW/Localization/Loc.cs
git commit -m "i18n: chiavi creazione guidata (Wiz_) in 5 lingue

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: ViewModel — JobWizardViewModel

**Files:**
- Create: `src/RobocopySW/ViewModels/JobWizardViewModel.cs`

(Build-verified; la logica di mappatura è già coperta dai test del planner in Task 1.)

- [ ] **Step 1: Crea il ViewModel**

Crea `src/RobocopySW/ViewModels/JobWizardViewModel.cs`:

```csharp
using RobocopySW.Core.Models;
using RobocopySW.Core.Services;
using RobocopySW.Infra;
using RobocopySW.Localization;

namespace RobocopySW.ViewModels;

/// <summary>ViewModel della creazione guidata: raccoglie le risposte, gestisce la navigazione a passi
/// e mostra l'anteprima del comando robocopy risultante.</summary>
public sealed class JobWizardViewModel : ObservableObject
{
    public const int StepCount = 5;

    private readonly JobWizardAnswers _a = new();
    private bool _hasFrozen;
    private int _step;

    // --- Passo 1: dati di base ---
    public string Name
    {
        get => _a.Name;
        set { _a.Name = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); }
    }

    public string Source
    {
        get => _a.Source;
        set { _a.Source = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); RaisePreview(); }
    }

    public string Destination
    {
        get => _a.Destination;
        set { _a.Destination = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); RaisePreview(); }
    }

    // --- Passo 2: tipo dischi (indice ComboBox <-> StorageKind) ---
    public int SourceStorageIndex
    {
        get => (int)_a.SourceStorage;
        set { _a.SourceStorage = (StorageKind)value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowNetCredNote)); RaisePreview(); }
    }

    public int DestStorageIndex
    {
        get => (int)_a.DestStorage;
        set { _a.DestStorage = (StorageKind)value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowNetCredNote)); RaisePreview(); }
    }

    // --- Passo 3: comportamento ---
    public bool Mirror
    {
        get => _a.Mirror;
        set { _a.Mirror = value; OnPropertyChanged(); OnPropertyChanged(nameof(Accumulate)); RaisePreview(); }
    }

    public bool Accumulate
    {
        get => !_a.Mirror;
        set { _a.Mirror = !value; OnPropertyChanged(); OnPropertyChanged(nameof(Mirror)); RaisePreview(); }
    }

    public bool HasLargeFiles
    {
        get => _a.HasLargeFiles;
        set { _a.HasLargeFiles = value; OnPropertyChanged(); RaisePreview(); }
    }

    // --- Passo 4: casi speciali ---
    public bool HasFrozen
    {
        get => _hasFrozen;
        set { _hasFrozen = value; OnPropertyChanged(); RaisePreview(); }
    }

    public string FrozenPatternsText
    {
        get => string.Join(Environment.NewLine, _a.FrozenMetadataPatterns);
        set { _a.FrozenMetadataPatterns = SplitLines(value); OnPropertyChanged(); RaisePreview(); }
    }

    public bool PreservePermissions
    {
        get => _a.PreservePermissions;
        set { _a.PreservePermissions = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool ExcludeCommonTemp
    {
        get => _a.ExcludeCommonTemp;
        set { _a.ExcludeCommonTemp = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool ShowNetCredNote =>
        _a.SourceStorage == StorageKind.Network || _a.DestStorage == StorageKind.Network;

    // --- Navigazione ---
    public int CurrentStep
    {
        get => _step;
        set
        {
            _step = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(ShowNext));
            OnPropertyChanged(nameof(ShowOpen));
            OnPropertyChanged(nameof(StepLabel));
        }
    }

    public bool CanGoBack => CurrentStep > 0;
    public bool IsLastStep => CurrentStep == StepCount - 1;
    public bool ShowNext => !IsLastStep;
    public bool ShowOpen => IsLastStep;

    public bool CanGoNext => CurrentStep != 0
        || (!string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(Source)
            && !string.IsNullOrWhiteSpace(Destination));

    public string StepLabel => string.Format(Loc.Instance["Wiz_Step"], CurrentStep + 1, StepCount);

    public void GoNext() { if (CanGoNext && !IsLastStep) CurrentStep++; }
    public void GoBack() { if (CanGoBack) CurrentStep--; }

    // --- Anteprima e risultato ---
    public string CommandPreview
    {
        get
        {
            try { return RobocopyArgsBuilder.ToDisplayString(RobocopyArgsBuilder.Build(BuildResult())); }
            catch (Exception ex) { return "(" + ex.Message + ")"; }
        }
    }

    /// <summary>Costruisce il job dalle risposte. I pattern "Forza copia" valgono solo se HasFrozen.</summary>
    public BackupJob BuildResult()
    {
        var answers = new JobWizardAnswers
        {
            Name = _a.Name,
            Source = _a.Source,
            Destination = _a.Destination,
            SourceStorage = _a.SourceStorage,
            DestStorage = _a.DestStorage,
            Mirror = _a.Mirror,
            HasLargeFiles = _a.HasLargeFiles,
            FrozenMetadataPatterns = _hasFrozen ? new List<string>(_a.FrozenMetadataPatterns) : new List<string>(),
            PreservePermissions = _a.PreservePermissions,
            ExcludeCommonTemp = _a.ExcludeCommonTemp,
        };
        return JobWizardPlanner.BuildJob(answers);
    }

    private void RaisePreview() => OnPropertyChanged(nameof(CommandPreview));

    private static List<string> SplitLines(string text) =>
        (text ?? "")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori.

- [ ] **Step 3: Commit**

```bash
git add src/RobocopySW/ViewModels/JobWizardViewModel.cs
git commit -m "feat: JobWizardViewModel (risposte, navigazione passi, anteprima)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: Finestra — JobWizardWindow (XAML + code-behind)

**Files:**
- Create: `src/RobocopySW/JobWizardWindow.xaml`
- Create: `src/RobocopySW/JobWizardWindow.xaml.cs`

- [ ] **Step 1: Crea lo XAML**

Crea `src/RobocopySW/JobWizardWindow.xaml`:

```xml
<ui:FluentWindow x:Class="RobocopySW.JobWizardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        xmlns:ctl="clr-namespace:RobocopySW.Controls"
        xmlns:l="clr-namespace:RobocopySW.Localization"
        mc:Ignorable="d"
        Title="{l:Tr Wiz_Title}" Height="660" Width="720"
        ExtendsContentIntoTitleBar="True"
        WindowBackdropType="Mica"
        WindowCornerPreference="Round"
        WindowStartupLocation="CenterOwner">
    <ui:FluentWindow.Resources>
        <Style TargetType="TextBlock" x:Key="Label">
            <Setter Property="Margin" Value="0,12,8,4"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
        <Style TargetType="TextBlock" x:Key="Hint">
            <Setter Property="Opacity" Value="0.7"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="TextWrapping" Value="Wrap"/>
            <Setter Property="Margin" Value="0,2,0,0"/>
        </Style>
        <Style TargetType="TextBlock" x:Key="StepTitle">
            <Setter Property="FontSize" Value="18"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Margin" Value="0,4,0,10"/>
        </Style>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
    </ui:FluentWindow.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <ui:TitleBar Grid.Row="0" Title="{l:Tr Wiz_Title}">
            <ui:TitleBar.Icon>
                <ui:SymbolIcon Symbol="WandSparkle24"/>
            </ui:TitleBar.Icon>
        </ui:TitleBar>

        <Grid Grid.Row="1" Margin="20,4,20,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            <TextBlock Grid.Row="0" Text="{Binding StepLabel}" Opacity="0.7" Margin="0,0,0,6"/>

            <TabControl Grid.Row="1" SelectedIndex="{Binding CurrentStep}" BorderThickness="0" Background="Transparent">
                <TabControl.ItemContainerStyle>
                    <Style TargetType="TabItem">
                        <Setter Property="Visibility" Value="Collapsed"/>
                    </Style>
                </TabControl.ItemContainerStyle>

                <!-- Passo 1: dati di base -->
                <TabItem>
                    <StackPanel>
                        <TextBlock Text="{l:Tr Wiz_Step1Title}" Style="{StaticResource StepTitle}"/>
                        <TextBlock Text="{l:Tr Editor_Name}" Style="{StaticResource Label}"/>
                        <ui:TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}"/>
                        <TextBlock Text="{l:Tr Editor_Source}" Style="{StaticResource Label}"/>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <ui:TextBox Grid.Column="0" Text="{Binding Source, UpdateSourceTrigger=PropertyChanged}"/>
                            <ui:Button Grid.Column="1" Content="{l:Tr Common_Browse}" Icon="{ui:SymbolIcon FolderOpen24}" Margin="8,0,0,0" Click="OnBrowseSource"/>
                        </Grid>
                        <TextBlock Text="{l:Tr Editor_Dest}" Style="{StaticResource Label}"/>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <ui:TextBox Grid.Column="0" Text="{Binding Destination, UpdateSourceTrigger=PropertyChanged}"/>
                            <ui:Button Grid.Column="1" Content="{l:Tr Common_Browse}" Icon="{ui:SymbolIcon FolderOpen24}" Margin="8,0,0,0" Click="OnBrowseDest"/>
                        </Grid>
                    </StackPanel>
                </TabItem>

                <!-- Passo 2: tipo dischi -->
                <TabItem>
                    <StackPanel>
                        <TextBlock Text="{l:Tr Wiz_Step2Title}" Style="{StaticResource StepTitle}"/>
                        <TextBlock Text="{l:Tr Wiz_SourceKind}" Style="{StaticResource Label}"/>
                        <ComboBox SelectedIndex="{Binding SourceStorageIndex}">
                            <ComboBoxItem Content="{l:Tr Wiz_Ssd}"/>
                            <ComboBoxItem Content="{l:Tr Wiz_Hdd}"/>
                            <ComboBoxItem Content="{l:Tr Wiz_Usb}"/>
                            <ComboBoxItem Content="{l:Tr Wiz_Network}"/>
                        </ComboBox>
                        <TextBlock Text="{l:Tr Wiz_DestKind}" Style="{StaticResource Label}"/>
                        <ComboBox SelectedIndex="{Binding DestStorageIndex}">
                            <ComboBoxItem Content="{l:Tr Wiz_Ssd}"/>
                            <ComboBoxItem Content="{l:Tr Wiz_Hdd}"/>
                            <ComboBoxItem Content="{l:Tr Wiz_Usb}"/>
                            <ComboBoxItem Content="{l:Tr Wiz_Network}"/>
                        </ComboBox>
                        <TextBlock Style="{StaticResource Hint}" Text="{l:Tr Wiz_KindHint}" Margin="0,12,0,0"/>
                    </StackPanel>
                </TabItem>

                <!-- Passo 3: comportamento -->
                <TabItem>
                    <StackPanel>
                        <TextBlock Text="{l:Tr Wiz_Step3Title}" Style="{StaticResource StepTitle}"/>
                        <TextBlock Text="{l:Tr Wiz_SyncQuestion}" Style="{StaticResource Label}"/>
                        <RadioButton Content="{l:Tr Wiz_SyncMirror}" IsChecked="{Binding Mirror}" GroupName="sync"/>
                        <RadioButton Content="{l:Tr Wiz_SyncAccumulate}" IsChecked="{Binding Accumulate}" GroupName="sync" Margin="0,4,0,0"/>
                        <CheckBox Content="{l:Tr Wiz_Large}" IsChecked="{Binding HasLargeFiles}" Margin="0,18,0,0"/>
                    </StackPanel>
                </TabItem>

                <!-- Passo 4: casi speciali -->
                <TabItem>
                    <StackPanel>
                        <TextBlock Text="{l:Tr Wiz_Step4Title}" Style="{StaticResource StepTitle}"/>
                        <CheckBox Content="{l:Tr Wiz_Frozen}" IsChecked="{Binding HasFrozen}"/>
                        <TextBlock Style="{StaticResource Hint}" Text="{l:Tr Wiz_FrozenHint}"/>
                        <TextBlock Text="{l:Tr Wiz_FrozenPatterns}" Style="{StaticResource Label}"/>
                        <ui:TextBox Text="{Binding FrozenPatternsText, UpdateSourceTrigger=PropertyChanged}"
                                    IsEnabled="{Binding HasFrozen}" AcceptsReturn="True" Height="56"
                                    TextWrapping="NoWrap" VerticalScrollBarVisibility="Auto" PlaceholderText="*.vc"/>
                        <CheckBox Content="{l:Tr Wiz_Perms}" IsChecked="{Binding PreservePermissions}" Margin="0,14,0,0"/>
                        <TextBlock Style="{StaticResource Hint}" Text="{l:Tr Wiz_PermsHint}"/>
                        <CheckBox Content="{l:Tr Wiz_Excl}" IsChecked="{Binding ExcludeCommonTemp}" Margin="0,14,0,0"/>
                    </StackPanel>
                </TabItem>

                <!-- Passo 5: riepilogo -->
                <TabItem>
                    <StackPanel>
                        <TextBlock Text="{l:Tr Wiz_Step5Title}" Style="{StaticResource StepTitle}"/>
                        <TextBlock Text="{l:Tr Wiz_Summary}" Style="{StaticResource Label}"/>
                        <Border CornerRadius="6" Background="#1E1E1E" Padding="10">
                            <TextBox Text="{Binding CommandPreview, Mode=OneWay}" IsReadOnly="True" Style="{x:Null}"
                                     FontFamily="Cascadia Mono, Consolas" FontSize="12" TextWrapping="Wrap"
                                     Background="#1E1E1E" Foreground="#9CDCFE" BorderThickness="0" MinHeight="90"/>
                        </Border>
                        <ui:InfoBar Margin="0,12,0,0" Severity="Informational" IsClosable="False"
                                    IsOpen="{Binding ShowNetCredNote}" Message="{l:Tr Wiz_NetCredNote}"/>
                    </StackPanel>
                </TabItem>
            </TabControl>
        </Grid>

        <Border Grid.Row="2" Padding="20,12" Background="#11888888">
            <Grid>
                <ui:Button Content="{l:Tr Wiz_Skip}" HorizontalAlignment="Left" Click="OnSkip"/>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <ui:Button Content="{l:Tr Common_Cancel}" Width="100" Margin="0,0,8,0" IsCancel="True" Click="OnCancel"/>
                    <ui:Button Content="{l:Tr Wiz_Back}" Width="100" Margin="0,0,8,0" IsEnabled="{Binding CanGoBack}" Click="OnBack"/>
                    <ui:Button Content="{l:Tr Wiz_Next}" Width="120" Appearance="Primary" Click="OnNext"
                               IsEnabled="{Binding CanGoNext}"
                               Visibility="{Binding ShowNext, Converter={StaticResource BoolToVis}}"/>
                    <ui:Button Content="{l:Tr Wiz_Open}" Width="170" Appearance="Primary" Icon="{ui:SymbolIcon Edit24}" Click="OnOpen"
                               Visibility="{Binding ShowOpen, Converter={StaticResource BoolToVis}}"/>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 2: Crea il code-behind**

Crea `src/RobocopySW/JobWizardWindow.xaml.cs`:

```csharp
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RobocopySW.Core.Models;
using RobocopySW.Localization;
using RobocopySW.ViewModels;

namespace RobocopySW;

public partial class JobWizardWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly JobWizardViewModel _vm = new();

    /// <summary>Job costruito dalle risposte (null se l'utente ha scelto "Salta e configura a mano").</summary>
    public BackupJob? ResultJob { get; private set; }

    public JobWizardWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void OnBrowseSource(object sender, RoutedEventArgs e)
    {
        var p = BrowseFolder(_vm.Source);
        if (p is not null) _vm.Source = p;
    }

    private void OnBrowseDest(object sender, RoutedEventArgs e)
    {
        var p = BrowseFolder(_vm.Destination);
        if (p is not null) _vm.Destination = p;
    }

    private static string? BrowseFolder(string? initial)
    {
        var dlg = new OpenFolderDialog { Title = Loc.Instance["Editor_BrowseTitle"] };
        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dlg.InitialDirectory = initial;
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private void OnBack(object sender, RoutedEventArgs e) => _vm.GoBack();
    private void OnNext(object sender, RoutedEventArgs e) => _vm.GoNext();
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    // Salta: apre l'editor vuoto (ResultJob = null lo segnala alla MainWindow).
    private void OnSkip(object sender, RoutedEventArgs e)
    {
        ResultJob = null;
        DialogResult = true;
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        ResultJob = _vm.BuildResult();
        DialogResult = true;
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori (XAML compila; `WandSparkle24` ed `Edit24` sono `SymbolRegular` validi — se uno non esistesse, sostituiscilo con `Options24`).

- [ ] **Step 4: Commit**

```bash
git add src/RobocopySW/JobWizardWindow.xaml src/RobocopySW/JobWizardWindow.xaml.cs
git commit -m "feat: finestra creazione guidata a 5 passi

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: Integrazione — "Nuovo" apre il wizard

**Files:**
- Modify: `src/RobocopySW/MainWindow.xaml.cs` (metodo `OnNewJob`, righe ~142-150)

- [ ] **Step 1: Sostituisci OnNewJob**

In `src/RobocopySW/MainWindow.xaml.cs`, sostituisci l'intero metodo:

```csharp
    private void OnNewJob(object sender, RoutedEventArgs e)
    {
        var job = new BackupJob { Name = Loc.Instance["Editor_NewJobName"] };
        if (ShowEditor(job))
        {
            _vm.Jobs.Add(new JobViewModel(job));
            _vm.PersistJobs();
        }
    }
```

con:

```csharp
    private void OnNewJob(object sender, RoutedEventArgs e)
    {
        // Creazione guidata; "Salta" restituisce ResultJob = null -> editor vuoto come prima.
        var wizard = new JobWizardWindow { Owner = this };
        if (wizard.ShowDialog() != true)
            return;

        var job = wizard.ResultJob ?? new BackupJob { Name = Loc.Instance["Editor_NewJobName"] };
        if (ShowEditor(job))
        {
            _vm.Jobs.Add(new JobViewModel(job));
            _vm.PersistJobs();
        }
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori.

- [ ] **Step 3: Commit**

```bash
git add src/RobocopySW/MainWindow.xaml.cs
git commit -m "feat: Nuovo apre la creazione guidata (con Salta verso editor vuoto)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 6: Verifica finale (build + test + smoke)

**Files:** nessuno (verifica).

- [ ] **Step 1: Build pulita (app chiusa)**

Assicurati che `RobocopySW.exe` non sia in esecuzione, poi:
Run: `dotnet build src/RobocopySW.sln -c Debug --nologo`
Atteso: 0 errori, 0 warning.

- [ ] **Step 2: Tutti i test**

Run: `dotnet test src/RobocopySW.sln --nologo`
Atteso: tutti PASS (gli 80 esistenti + i nuovi di `JobWizardPlannerTests`).

- [ ] **Step 3: Smoke manuale (a cura dell'utente)**

1. **Nuovo** → si apre la creazione guidata al passo 1; "Avanti" è disabilitato finché nome/sorgente/destinazione non sono compilati.
2. Passo 2: scegli sorgente SSD + destinazione HDD; passo 3: "Rispecchiare" + "file molto grandi" sì; passo 4: lascia vuoto; passo 5: il riepilogo mostra un comando con `/MIR /MT:2 /Z`. "Apri nell'editor" → l'editor è precompilato con quei valori; salva → il job compare in lista.
3. Riapri **Nuovo** e premi **Salta e configura a mano** → si apre l'editor vuoto, come oggi.
4. Prova un caso rete (destinazione "Share di rete"): al passo 5 compare l'avviso credenziale; il comando mostra `/R:3 /W:10`.

- [ ] **Step 4: (eventuale) Commit di ritocchi**

Solo se servono correzioni dallo smoke:

```bash
git add -A
git commit -m "fix: ritocchi finali creazione guidata

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**Copertura spec:**
- StorageKind + JobWizardAnswers + JobWizardPlanner (regole /MT, mirror, /Z, /COPYALL, rete→retry, force copy, esclusioni) → Task 1, con test. ✅
- Localizzazione 28 chiavi × 5 lingue → Task 2. ✅
- ViewModel (risposte, navigazione passi, validazione passo 1, anteprima, gating frozen) → Task 3. ✅
- Finestra a 5 passi (TabControl headers nascosti, footer Indietro/Avanti/Apri, Salta, riepilogo+anteprima, avviso rete) → Task 4. ✅
- "Nuovo" → wizard; "Salta" → editor vuoto → Task 5. ✅
- Verifica end-to-end → Task 6. ✅

**Coerenza tipi/firme:** `StorageKind {Ssd,Hdd,Usb,Network}` (ordine = ComboBox); `JobWizardAnswers` campi usati identici in planner e VM; `JobWizardPlanner.BuildJob(JobWizardAnswers)` e `RecommendedThreads(StorageKind,StorageKind)`; `JobWizardViewModel.BuildResult()` → `BackupJob`; `JobWizardWindow.ResultJob` (`BackupJob?`) letto in `MainWindow.OnNewJob`. Le proprietà bindate nello XAML (`Name`, `Source`, `Destination`, `SourceStorageIndex`, `DestStorageIndex`, `Mirror`, `Accumulate`, `HasLargeFiles`, `HasFrozen`, `FrozenPatternsText`, `PreservePermissions`, `ExcludeCommonTemp`, `CurrentStep`, `StepLabel`, `CanGoBack`, `CanGoNext`, `ShowNext`, `ShowOpen`, `ShowNetCredNote`, `CommandPreview`) esistono tutte nel ViewModel di Task 3.

**Chiavi Loc usate nello XAML:** `Wiz_*` (Task 2) + riuso di `Editor_Name`, `Editor_Source`, `Editor_Dest`, `Editor_BrowseTitle`, `Editor_NewJobName`, `Common_Browse`, `Common_Cancel` (già esistenti).

**Nota:** i ViewModel/finestre WPF non hanno test unitari perché il progetto test referenzia solo Core; la logica di mappatura è coperta dai test del planner (Task 1) e il resto dallo smoke (Task 6) — coerente con l'editor esistente.
