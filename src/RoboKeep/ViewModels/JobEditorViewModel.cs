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
        RefreshVolumeState();
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
        set
        {
            if (_job.Destination == value) return;
            _job.Destination = value;
            // Destinazione cambiata → il disco memorizzato non c'entra piu' nulla: riassocia.
            // NB: il salvataggio a destinazione INVARIATA non riassocia mai (vedi UseCurrentVolume):
            // altrimenti basterebbe aprire l'editor col disco sbagliato inserito e salvare una
            // modifica qualsiasi per annullare in silenzio la protezione.
            UseCurrentVolume();
            OnPropertyChanged();
            RaisePreview();
        }
    }

    public bool Mirror
    {
        get => _job.Mirror;
        set { _job.Mirror = value; OnPropertyChanged(); RaisePreview(); }
    }

    /// <summary>Soglia di sicurezza delle cancellazioni in mirror (0 = nessun controllo). Il valore
    /// non viene corretto qui: uno fuori intervallo lo intercetta <see cref="Validate"/>, cosi'
    /// l'utente vede che cosa ha scritto invece di trovarlo cambiato in silenzio.</summary>
    public int MirrorDeleteLimitPercent
    {
        get => _job.MirrorDeleteLimitPercent;
        set { _job.MirrorDeleteLimitPercent = value; OnPropertyChanged(); }
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

    public bool ScheduleLastDayOfMonth
    {
        get => _job.ScheduleLastDayOfMonth;
        set { _job.ScheduleLastDayOfMonth = value; OnPropertyChanged(); OnPropertyChanged(nameof(ScheduleFixedDayEnabled)); }
    }

    /// <summary>Il numero del giorno si modifica solo se non e' scelto "ultimo del mese".</summary>
    public bool ScheduleFixedDayEnabled => !_job.ScheduleLastDayOfMonth;

    public bool VerifyAfterRun
    {
        get => _job.VerifyAfterRun;
        set { _job.VerifyAfterRun = value; OnPropertyChanged(); }
    }

    public int VerifyEveryDays
    {
        get => _job.VerifyEveryDays;
        set { _job.VerifyEveryDays = Math.Max(0, value); OnPropertyChanged(); }
    }

    public int InterPacketGapMs
    {
        get => _job.InterPacketGapMs;
        set { _job.InterPacketGapMs = Math.Max(0, value); OnPropertyChanged(); RaisePreview(); }
    }

    private bool _volumeMismatch;
    private bool _localDiskAbsent;
    private VolumeInfo? _current;
    private bool _isNetwork;

    /// <summary>true se il job è associato a un disco (id memorizzato non vuoto).</summary>
    public bool HasVolume => !string.IsNullOrEmpty(_job.DestinationVolumeId);

    /// <summary>La riga "Disco" è visibile quando la destinazione è un disco locale di cui
    /// parlare: il job è già protetto, c'è un disco presente da cui associarlo, oppure la
    /// destinazione punta a un'unità locale che ora non è collegata (per dirlo apertamente
    /// invece di nascondere che la protezione non è attiva). Nascosta solo per le destinazioni
    /// di rete e per un percorso vuoto/non locale.</summary>
    public bool ShowVolumeRow => !_isNetwork && (HasVolume || _current is not null || _localDiskAbsent);

    /// <summary>true quando la destinazione è su un'unità locale non collegata e il job non è
    /// protetto: mostra un avviso al posto del pulsante (non c'è un disco da associare adesso).
    /// Rende visibile il caso in cui cambiare destinazione verso un disco staccato lascerebbe
    /// il job senza protezione, che altrimenti passerebbe in silenzio.</summary>
    public bool ShowConnectDiskHint => _localDiskAbsent && !HasVolume;

    /// <summary>Testo principale della riga: l'etichetta del disco associato, oppure "non
    /// protetto" quando il job non ha ancora un disco (i job creati prima della v1.5).</summary>
    public string VolumeStatusText => HasVolume
        ? (string.IsNullOrEmpty(VolumeLabel) ? Loc.Instance["Editor_VolumeUnknown"] : VolumeLabel)
        : Loc.Instance["Editor_VolumeUnprotected"];

    /// <summary>Etichetta del disco a cui il job è associato (vuota = nessuna associazione).</summary>
    public string VolumeLabel => _job.DestinationVolumeLabel ?? "";

    /// <summary>true se il disco attualmente collegato NON è quello associato al job.</summary>
    public bool VolumeMismatch => _volumeMismatch;

    /// <summary>Avviso da mostrare quando il disco collegato è un altro.</summary>
    public string VolumeMismatchText =>
        string.Format(Loc.Instance["Editor_VolumeMismatch"],
            string.IsNullOrEmpty(_current?.Label) ? Loc.Instance["Editor_VolumeUnknown"] : _current!.Label);

    /// <summary>Il pulsante d'azione è visibile quando c'è un disco presente e o il job non è
    /// ancora protetto (→ "Proteggi con questo disco") o è collegato il disco sbagliato
    /// (→ "Usa questo disco"). Senza un disco presente non c'è nulla da associare.</summary>
    public bool VolumeActionVisible => _current is not null && (!HasVolume || _volumeMismatch);

    /// <summary>Etichetta del pulsante d'azione: prima associazione contro riassociazione.</summary>
    public string VolumeActionText =>
        Loc.Instance[HasVolume ? "Editor_UseThisDisk" : "Editor_ProtectDisk"];

    /// <summary>Ricalcola lo stato del disco (chiamato all'apertura e a ogni riassociazione).</summary>
    public void RefreshVolumeState()
    {
        _isNetwork = VolumeIdentity.IsNetworkPath(_job.Destination);
        _current = _isNetwork ? null : VolumeIdentity.ForPath(_job.Destination);
        // Percorso con radice di unità (es. "E:\...") ma volume non identificabile: il disco di
        // quella lettera non è collegato ora. Diverso da un percorso vuoto o di rete.
        _localDiskAbsent = !_isNetwork && _current is null && HasDriveRoot(_job.Destination);
        _volumeMismatch = VolumeGuard.Check(_job.DestinationVolumeId, _current) == VolumeCheck.WrongDisk;
        OnPropertyChanged(nameof(HasVolume));
        OnPropertyChanged(nameof(ShowVolumeRow));
        OnPropertyChanged(nameof(ShowConnectDiskHint));
        OnPropertyChanged(nameof(VolumeStatusText));
        OnPropertyChanged(nameof(VolumeLabel));
        OnPropertyChanged(nameof(VolumeMismatch));
        OnPropertyChanged(nameof(VolumeMismatchText));
        OnPropertyChanged(nameof(VolumeActionVisible));
        OnPropertyChanged(nameof(VolumeActionText));
    }

    /// <summary>true se il percorso ha una radice di unità locale ("X:\"): serve a distinguere
    /// un disco locale staccato da un percorso vuoto o di rete. Best-effort, mai un'eccezione.</summary>
    private static bool HasDriveRoot(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(path));
            return root is not null && root.Length >= 2 && char.IsLetter(root[0]) && root[1] == ':';
        }
        catch { return false; }
    }

    /// <summary>Associa il job al disco attualmente collegato: usato dal pulsante
    /// "Usa questo disco" e automaticamente quando cambia la destinazione. È l'UNICO modo
    /// di riassociare un job a destinazione invariata, e deve restare esplicito.</summary>
    public void UseCurrentVolume()
    {
        JobVolumeAssociation.ToCurrentDisk(_job);
        RefreshVolumeState();
    }

    /// <summary>Validazione minima prima del salvataggio.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return Loc.Instance["Editor_Val_Name"];
        if (Name.Contains('"'))
            return Loc.Instance["Editor_Val_NameQuotes"];
        if (string.IsNullOrWhiteSpace(Source)) return Loc.Instance["Editor_Val_Source"];
        if (string.IsNullOrWhiteSpace(Destination)) return Loc.Instance["Editor_Val_Dest"];
        if (_job.MirrorDeleteLimitPercent is < 0 or > 100)
            return Loc.Instance["Editor_Val_MirrorDeleteLimit"];
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
