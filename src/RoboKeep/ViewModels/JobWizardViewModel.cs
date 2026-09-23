using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>ViewModel della creazione guidata: raccoglie le risposte, gestisce la navigazione a passi
/// e mostra l'anteprima del comando robocopy risultante.</summary>
public sealed class JobWizardViewModel : ObservableObject
{
    public const int StepCount = 4;

    private readonly JobWizardAnswers _a = new();
    private readonly IReadOnlyList<CredentialEntry> _credentials;
    private bool _hasFrozen;
    private int _step;

    /// <param name="credentials">Credenziali gia' salvate: se una copre la share scelta, il wizard la usa.</param>
    public JobWizardViewModel(IEnumerable<CredentialEntry>? credentials = null)
        => _credentials = (credentials ?? Array.Empty<CredentialEntry>()).ToList();

    // --- Passo 1: dati di base ---
    public string Name
    {
        get => _a.Name;
        set { _a.Name = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoNext)); }
    }

    public string Source
    {
        get => _a.Source;
        set
        {
            _a.Source = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoNext));
            RaiseNetCred();
            RaisePreview();
        }
    }

    public string Destination
    {
        get => _a.Destination;
        set
        {
            _a.Destination = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoNext));
            RaiseNetCred();
            OnPropertyChanged(nameof(ShowVersionsNote));
            RaisePreview();
        }
    }

    // --- Passo 2: comportamento ---
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

    public bool KeepVersions
    {
        get => _a.KeepVersions;
        set { _a.KeepVersions = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowVersionsNote)); RaisePreview(); }
    }

    public int VersionsToKeep
    {
        get => _a.VersionsToKeep;
        set { _a.VersionsToKeep = value; OnPropertyChanged(); RaisePreview(); }
    }

    // Pianificazione: stessa mappatura dell'editor (JobEditorViewModel), cosi' i due restano coerenti.
    /// <summary>Indice combo frequenza: 0=None, 1=Daily, 2=Weekly, 3=Monthly.</summary>
    public int ScheduleIndex
    {
        get => (int)_a.Schedule;
        set
        {
            _a.Schedule = (ScheduleKind)value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScheduleEnabled));
            OnPropertyChanged(nameof(ShowWeekDay));
            OnPropertyChanged(nameof(ShowMonthDay));
            RaisePreview();
        }
    }

    public bool ScheduleEnabled => _a.Schedule != ScheduleKind.None;
    public bool ShowWeekDay => _a.Schedule == ScheduleKind.Weekly;
    public bool ShowMonthDay => _a.Schedule == ScheduleKind.Monthly;

    public string ScheduleTime
    {
        get => _a.ScheduleTime;
        set { _a.ScheduleTime = value; OnPropertyChanged(); RaisePreview(); }
    }

    /// <summary>Indice combo giorno: 0=lunedi' ... 6=domenica (ordine europeo).</summary>
    public int ScheduleWeekDayIndex
    {
        get => ((int)_a.ScheduleWeekDay + 6) % 7; // DayOfWeek: Sunday=0 -> indice 6
        set { _a.ScheduleWeekDay = (DayOfWeek)((value + 1) % 7); OnPropertyChanged(); RaisePreview(); }
    }

    public int ScheduleMonthDay
    {
        get => _a.ScheduleMonthDay;
        set { _a.ScheduleMonthDay = Math.Clamp(value, 1, 31); OnPropertyChanged(); RaisePreview(); }
    }

    public bool ScheduleLastDayOfMonth
    {
        get => _a.ScheduleLastDayOfMonth;
        set { _a.ScheduleLastDayOfMonth = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScheduleFixedDayEnabled)); RaisePreview(); }
    }

    public bool ScheduleFixedDayEnabled => !_a.ScheduleLastDayOfMonth;

    // --- Passo 3: casi speciali ---
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

    // --- Credenziali di rete (passo 4) ---
    // Se sorgente o destinazione sono una share UNC, il wizard chiede utente e password qui, senza
    // mandare l'utente in Impostazioni e poi in Modifica. Se una credenziale salvata copre gia' la
    // share, la usa e non chiede niente. Le unita' mappate (Z:) non passano di qui: le gestisce Windows.
    /// <summary>La share UNC coinvolta (destinazione prima), o null.</summary>
    public string? NetworkShare => Core.Services.NetworkShare.FirstShare(_a.Destination, _a.Source);

    /// <summary>Credenziale gia' salvata che copre la share, o null.</summary>
    public CredentialEntry? ExistingCredential =>
        NetworkShare is { } s ? Core.Services.NetworkShare.FindCredential(_credentials, s) : null;

    public bool ShowNetCredFields => NetworkShare is not null && ExistingCredential is null;
    public bool ShowNetCredExisting => NetworkShare is not null && ExistingCredential is not null;

    public string NetCredIntro => NetworkShare is { } s
        ? ExistingCredential is { } c
            ? string.Format(Loc.Instance["Wiz_NetCredExisting"], s, c.Id)
            : string.Format(Loc.Instance["Wiz_NetCredIntro"], s)
        : "";

    private string _netUser = "";
    /// <summary>Utente digitato nel wizard (la password la legge la finestra dal PasswordBox).</summary>
    public string NetUser
    {
        get => _netUser;
        set { _netUser = value; OnPropertyChanged(); }
    }

    private void RaiseNetCred()
    {
        OnPropertyChanged(nameof(NetworkShare));
        OnPropertyChanged(nameof(ExistingCredential));
        OnPropertyChanged(nameof(ShowNetCredFields));
        OnPropertyChanged(nameof(ShowNetCredExisting));
        OnPropertyChanged(nameof(NetCredIntro));
    }

    /// <summary>Avviso: le versioni datate servono hard-link, che la destinazione scelta non offre.
    /// <see cref="HardLinkSupport.IsSupported"/> non lancia e risale al primo antenato esistente.</summary>
    public bool ShowVersionsNote =>
        KeepVersions && !string.IsNullOrWhiteSpace(Destination) && !HardLinkSupport.IsSupported(Destination);

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
            Mirror = _a.Mirror,
            HasLargeFiles = _a.HasLargeFiles,
            FrozenMetadataPatterns = _hasFrozen ? new List<string>(_a.FrozenMetadataPatterns) : new List<string>(),
            PreservePermissions = _a.PreservePermissions,
            ExcludeCommonTemp = _a.ExcludeCommonTemp,
            HasOpenFiles = _a.HasOpenFiles,
            KeepVersions = _a.KeepVersions,
            VersionsToKeep = _a.VersionsToKeep,
            Schedule = _a.Schedule,
            ScheduleTime = _a.ScheduleTime,
            ScheduleWeekDay = _a.ScheduleWeekDay,
            ScheduleMonthDay = _a.ScheduleMonthDay,
            ScheduleLastDayOfMonth = _a.ScheduleLastDayOfMonth,
        };
        var job = JobWizardPlanner.BuildJob(answers);
        // Una credenziale gia' salvata che copre la share si assegna subito; quella nuova la crea
        // la finestra principale (serve la cifratura DPAPI) e la assegna dopo.
        job.CredentialId = ExistingCredential?.Id;
        return job;
    }

    private void RaisePreview() => OnPropertyChanged(nameof(CommandPreview));

    private static List<string> SplitLines(string text) =>
        (text ?? "")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
