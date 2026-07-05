using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>Opzione di credenziale per la combo (id + etichetta leggibile).</summary>
public sealed record CredentialOption(string? Id, string Display);

/// <summary>ViewModel per la finestra di modifica di un job, con anteprima live del comando.</summary>
public sealed class JobEditorViewModel : ObservableObject
{
    private readonly BackupJob _job;

    public JobEditorViewModel(BackupJob job, IEnumerable<CredentialEntry> credentials)
    {
        _job = job;
        Credentials = new List<CredentialOption> { new(null, Loc.Instance["Cred_NoneLocal"]) };
        foreach (var c in credentials)
            Credentials.Add(new CredentialOption(c.Id, $"{c.Id} ({c.Host})"));
        _selectedCredential = Credentials.FirstOrDefault(o => o.Id == job.CredentialId) ?? Credentials[0];
    }

    public BackupJob Job => _job;
    public List<CredentialOption> Credentials { get; }

    public string Name
    {
        get => _job.Name;
        set { _job.Name = value; OnPropertyChanged(); }
    }

    public string Source
    {
        get => _job.Source;
        set { _job.Source = value; OnPropertyChanged(); RaisePreview(); }
    }

    public string Destination
    {
        get => _job.Destination;
        set { _job.Destination = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool Mirror
    {
        get => _job.Mirror;
        set { _job.Mirror = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool ExcludeOlder
    {
        get => _job.ExcludeOlder;
        set { _job.ExcludeOlder = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool CopyAll
    {
        get => _job.CopyAll;
        set { _job.CopyAll = value; OnPropertyChanged(); RaisePreview(); }
    }

    public int MultiThread
    {
        get => _job.MultiThread;
        set { _job.MultiThread = value; OnPropertyChanged(); RaisePreview(); }
    }

    /// <summary>/J — ottimizza file grandi. Attivandolo si disattiva <see cref="Restartable"/>.</summary>
    public bool UnbufferedIO
    {
        get => _job.UnbufferedIO;
        set
        {
            _job.UnbufferedIO = value;
            if (value) _job.Restartable = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Restartable));
            RaisePreview();
        }
    }

    /// <summary>/Z — copia riavviabile. Attivandolo si disattiva <see cref="UnbufferedIO"/>.</summary>
    public bool Restartable
    {
        get => _job.Restartable;
        set
        {
            _job.Restartable = value;
            if (value) _job.UnbufferedIO = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnbufferedIO));
            RaisePreview();
        }
    }

    /// <summary>/V — registra tutti i file nel log (anche quelli saltati).</summary>
    public bool LogAllFiles
    {
        get => _job.LogAllFiles;
        set { _job.LogAllFiles = value; OnPropertyChanged(); RaisePreview(); }
    }

    public int Retries
    {
        get => _job.Retries;
        set { _job.Retries = value; OnPropertyChanged(); RaisePreview(); }
    }

    public int Wait
    {
        get => _job.Wait;
        set { _job.Wait = value; OnPropertyChanged(); RaisePreview(); }
    }

    public bool Enabled
    {
        get => _job.Enabled;
        set { _job.Enabled = value; OnPropertyChanged(); }
    }

    /// <summary>Esclusioni file, una per riga (es. <c>*.tmp</c>).</summary>
    public string ExcludeFilesText
    {
        get => string.Join(Environment.NewLine, _job.ExcludeFiles);
        set { _job.ExcludeFiles = SplitLines(value); OnPropertyChanged(); RaisePreview(); }
    }

    /// <summary>Esclusioni cartelle, una per riga (es. <c>cache</c>).</summary>
    public string ExcludeDirsText
    {
        get => string.Join(Environment.NewLine, _job.ExcludeDirs);
        set { _job.ExcludeDirs = SplitLines(value); OnPropertyChanged(); RaisePreview(); }
    }

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

    private CredentialOption _selectedCredential;
    public CredentialOption SelectedCredential
    {
        get => _selectedCredential;
        set { _selectedCredential = value; _job.CredentialId = value?.Id; OnPropertyChanged(); }
    }

    /// <summary>Anteprima live della riga di comando robocopy.</summary>
    public string CommandPreview
    {
        get
        {
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
        }
    }

    public bool Versioned
    {
        get => _job.Versioned;
        set { _job.Versioned = value; OnPropertyChanged(); }
    }

    public int SnapshotKeepCount
    {
        get => _job.SnapshotKeepCount;
        set { _job.SnapshotKeepCount = value; OnPropertyChanged(); }
    }

    public int SnapshotMaxAgeDays
    {
        get => _job.SnapshotMaxAgeDays;
        set { _job.SnapshotMaxAgeDays = value; OnPropertyChanged(); }
    }

    /// <summary>Copia anche i file aperti creando uno snapshot VSS della sorgente.</summary>
    public bool UseVss
    {
        get => _job.UseVss;
        set { _job.UseVss = value; OnPropertyChanged(); }
    }

    /// <summary>Indice combo pianificazione: 0=None, 1=Daily, 2=Weekly, 3=Monthly.</summary>
    public int ScheduleIndex
    {
        get => (int)_job.Schedule;
        set
        {
            _job.Schedule = (ScheduleKind)value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowWeekDay));
            OnPropertyChanged(nameof(ShowMonthDay));
            OnPropertyChanged(nameof(ScheduleEnabled));
        }
    }

    public bool ScheduleEnabled => _job.Schedule != ScheduleKind.None;
    public bool ShowWeekDay => _job.Schedule == ScheduleKind.Weekly;
    public bool ShowMonthDay => _job.Schedule == ScheduleKind.Monthly;

    public string ScheduleTime
    {
        get => _job.ScheduleTime;
        set { _job.ScheduleTime = value; OnPropertyChanged(); }
    }

    /// <summary>Indice combo giorno: 0=lunedì ... 6=domenica (ordine europeo).</summary>
    public int ScheduleWeekDayIndex
    {
        get => ((int)_job.ScheduleWeekDay + 6) % 7; // DayOfWeek: Sunday=0 → indice 6
        set { _job.ScheduleWeekDay = (DayOfWeek)((value + 1) % 7); OnPropertyChanged(); }
    }

    public int ScheduleMonthDay
    {
        get => _job.ScheduleMonthDay;
        set { _job.ScheduleMonthDay = Math.Clamp(value, 1, 31); OnPropertyChanged(); }
    }

    public bool VerifyAfterRun
    {
        get => _job.VerifyAfterRun;
        set { _job.VerifyAfterRun = value; OnPropertyChanged(); }
    }

    public int InterPacketGapMs
    {
        get => _job.InterPacketGapMs;
        set { _job.InterPacketGapMs = Math.Max(0, value); OnPropertyChanged(); RaisePreview(); }
    }

    /// <summary>Validazione minima prima del salvataggio.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return Loc.Instance["Editor_Val_Name"];
        if (string.IsNullOrWhiteSpace(Source)) return Loc.Instance["Editor_Val_Source"];
        if (string.IsNullOrWhiteSpace(Destination)) return Loc.Instance["Editor_Val_Dest"];
        if (_job.Schedule != ScheduleKind.None && !TimeOnly.TryParse(_job.ScheduleTime, out _))
            return Loc.Instance["Editor_Val_ScheduleTime"];
        return null;
    }

    private void RaisePreview() => OnPropertyChanged(nameof(CommandPreview));

    private static List<string> SplitLines(string text) =>
        (text ?? "")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
}
