using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

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

    public bool HasOpenFiles
    {
        get => _a.HasOpenFiles;
        set { _a.HasOpenFiles = value; OnPropertyChanged(); RaisePreview(); }
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
            HasOpenFiles = _a.HasOpenFiles,
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
