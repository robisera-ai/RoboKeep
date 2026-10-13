using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;
using RoboKeep.Infra;
using RoboKeep.Localization;

namespace RoboKeep.ViewModels;

/// <summary>ViewModel principale: lista job, esecuzione e log live (con flush batch).</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly StringBuilder _buffer = new();
    private readonly object _bufLock = new();
    private readonly DispatcherTimer _logTimer;
    private CancellationTokenSource? _cts;

    /// <summary>Notifica testo da accodare alla console di log (già batchato, sul thread UI).</summary>
    public event Action<string>? LogFlushed;
    /// <summary>Richiesta di svuotare la console di log.</summary>
    public event Action? LogCleared;

    public MainViewModel(AppHost host)
    {
        _host = host;
        Jobs = new ObservableCollection<JobViewModel>();
        Jobs.CollectionChanged += (_, e) =>
        {
            // Aggancia/sgancia il salvataggio quando un job viene attivato/disattivato dalla griglia.
            if (e.OldItems is not null)
                foreach (JobViewModel j in e.OldItems) j.PropertyChanged -= OnJobPropertyChanged;
            if (e.NewItems is not null)
                foreach (JobViewModel j in e.NewItems) j.PropertyChanged += OnJobPropertyChanged;

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasJobs));
            OnPropertyChanged(nameof(StatusText));
        };
        ReloadJobs();

        // Le righe di output arrivano dai thread di lettura del processo e vengono
        // accumulate in un buffer; un timer le riversa sulla UI poche volte al secondo.
        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _logTimer.Tick += (_, _) => FlushLog();

        PreviewSelectedCommand = new RelayCommand(() => Toggle(RunKind.Preview), () => CanToggle(RunKind.Preview));
        RunSelectedCommand = new RelayCommand(() => Toggle(RunKind.Selected), () => CanToggle(RunKind.Selected));
        RunAllCommand = new RelayCommand(() => Toggle(RunKind.All), () => CanToggle(RunKind.All));
        VerifySelectedCommand = new RelayCommand(() => Toggle(RunKind.Verify), () => CanToggle(RunKind.Verify));
        ClearLogCommand = new RelayCommand(() =>
        {
            lock (_bufLock) _buffer.Clear();
            LogCleared?.Invoke();
        });
        RefreshCommand = new RelayCommand(ReloadLastResults);


        Loc.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(StatusText));
            RaiseRunButtons();
        };
    }

    // Salva la configurazione quando un job viene attivato/disattivato dalla griglia.
    private void OnJobPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JobViewModel.Enabled))
            PersistJobs();
    }

    /// <summary>Sposta un job nel "gap" sopra/sotto la riga di destinazione (drag &amp; drop). Salva l'ordine.</summary>
    public void MoveJobToGap(JobViewModel item, JobViewModel target, bool below)
    {
        var from = Jobs.IndexOf(item);
        var t = Jobs.IndexOf(target);
        if (from < 0 || t < 0) return;

        var insert = below ? t + 1 : t;   // posizione del gap nella lista con item presente
        if (from < insert) insert--;      // rimuovendo item prima del gap, il gap scala di 1
        insert = Math.Clamp(insert, 0, Jobs.Count - 1);
        if (insert == from) return;

        Jobs.Move(from, insert);
        SelectedJob = item;
        PersistJobs();
    }

    /// <summary>Ordina FISICAMENTE i job per la proprietà mostrata nella colonna e salva l'ordine.
    /// Riordinare la collezione reale (anziché applicare un ordinamento di vista) mantiene coerente
    /// il riordino manuale via drag &amp; drop: l'ordine visibile è sempre quello salvato.</summary>
    public void SortJobs(string propertyPath, bool ascending)
    {
        var prop = typeof(JobViewModel).GetProperty(propertyPath);
        if (prop is null) return; // colonna senza proprietà corrispondente: niente da ordinare

        CollectionReorder.SortByKey(Jobs, j => prop.GetValue(j)?.ToString() ?? "", ascending);
        PersistJobs();
    }

    public ObservableCollection<JobViewModel> Jobs { get; }

    /// <summary>true quando non ci sono job: la UI mostra lo stato vuoto.</summary>
    public bool IsEmpty => Jobs.Count == 0;

    /// <summary>Opposto di <see cref="IsEmpty"/>: c'è almeno un job.</summary>
    public bool HasJobs => Jobs.Count > 0;

    private bool _healthBannerVisible;
    public bool HealthBannerVisible
    {
        get => _healthBannerVisible;
        set => SetField(ref _healthBannerVisible, value);
    }

    private string _healthBannerText = "";
    public string HealthBannerText
    {
        get => _healthBannerText;
        set => SetField(ref _healthBannerText, value);
    }

    // --- Aggiornamenti ---
    /// <summary>Esito di un controllo aggiornamenti, per chi lo mostra (Impostazioni).</summary>
    public enum UpdateCheckOutcome { Skipped, UpToDate, Available, Unreachable, NotPublic }

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

    private bool _hasFaultedDisks;
    /// <summary>true se c'e' almeno un disco a riposo per errore hardware: mostra "Riattiva dischi".</summary>
    public bool HasFaultedDisks
    {
        get => _hasFaultedDisks;
        private set => SetField(ref _hasFaultedDisks, value);
    }

    private bool _hasScheduledTasks;
    /// <summary>
    /// true se in Windows c'e' almeno un'attivita' pianificata di RoboKeep: accende il
    /// pulsante «Attivita' pianificate». Senza attivita' il pulsante resterebbe una porta
    /// su una finestra vuota.
    /// </summary>
    public bool HasScheduledTasks
    {
        get => _hasScheduledTasks;
        private set => SetField(ref _hasScheduledTasks, value);
    }

    // Numero progressivo delle letture in corso: vale solo l'ultima partita.
    private int _scheduledTasksTicket;

    /// <summary>
    /// Rilegge in sottofondo l'elenco delle attivita' di Windows. La lettura passa dall'API
    /// COM e puo' costare qualche decimo di secondo: mai sul thread della UI. Best-effort:
    /// se l'API non risponde o l'accesso e' negato, il pulsante resta spento e nessun errore
    /// arriva a video — non e' un guasto di cui l'utente debba occuparsi.
    /// Rinominare un job fa partire due letture in fila (rimozione + creazione dell'attivita'):
    /// senza il contatore, la risposta della prima — piu' lenta e gia' vecchia — arriverebbe
    /// per ultima e spegnerebbe il pulsante su un'attivita' appena creata.
    /// </summary>
    /// <returns>I job che hanno una pianificazione ma non piu' la loro attivita' in Windows
    /// (vuoto se la lettura e' fallita o e' stata superata da una piu' recente).</returns>
    public async Task<IReadOnlyList<string>> RefreshScheduledTasksAsync()
    {
        var mine = Interlocked.Increment(ref _scheduledTasksTicket);
        try
        {
            var jobs = _host.Config.Jobs.ToList();
            var (count, missing) = await Task.Run(() =>
            {
                var tasks = TaskSchedulerCatalog.List();
                return (tasks.Count, ScheduledTaskInfo.JobsWithoutTask(jobs, tasks));
            });
            if (mine != Volatile.Read(ref _scheduledTasksTicket)) return Array.Empty<string>();
            HasScheduledTasks = count > 0;
            return missing;
        }
        catch
        {
            if (mine == Volatile.Read(ref _scheduledTasksTicket))
                HasScheduledTasks = false;
            return Array.Empty<string>();
        }
    }

    private string _faultedDisksSummary = "";
    /// <summary>Elenco leggibile dei dischi a riposo (etichetta, lettera, da quando, motivo): nel
    /// tooltip del pulsante e nella conferma, cosi' con piu' dischi si sa quale si sta riabilitando.</summary>
    public string FaultedDisksSummary
    {
        get => _faultedDisksSummary;
        private set => SetField(ref _faultedDisksSummary, value);
    }

    private JobViewModel? _selectedJob;
    public JobViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetField(ref _selectedJob, value))
                OnPropertyChanged(nameof(IsVersionedJobSelected));
        }
    }

    // Righe evidenziate nella griglia. SelectedItems di DataGrid non si puo' associare, quindi
    // la finestra le passa qui a ogni cambio di selezione. Prima esisteva solo SelectedJob: la
    // griglia lasciava evidenziare piu' righe (SelectionMode predefinito: Extended), ma «Avvia
    // selezionato» e «Verifica» ne prendevano una sola, senza dirlo.
    private List<JobViewModel> _selectedJobs = new();

    /// <summary>Aggiorna le righe evidenziate (la chiama la griglia).</summary>
    public void SetSelectedJobs(IEnumerable<JobViewModel> jobs)
    {
        _selectedJobs = jobs.ToList();
        RaiseRunButtons();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>I job da avviare o verificare, nell'ordine in cui stanno nell'elenco e non in
    /// quello in cui sono stati cliccati. Le righe sopravvissute a un ricaricamento: dopo un run
    /// la collezione puo' essere ricostruita, e un riferimento vecchio farebbe partire un job che
    /// non e' piu' quello a video.</summary>
    private List<JobViewModel> SelectedOrPrimary()
    {
        var chosen = Jobs.Where(j => _selectedJobs.Contains(j)).ToList();
        if (chosen.Count > 0) return chosen;
        return SelectedJob is null ? new List<JobViewModel>() : new List<JobViewModel> { SelectedJob };
    }

    /// <summary>Quante righe prenderebbero «Avvia selezionato» e «Verifica»: lo dicono le
    /// etichette, perche' con due righe evidenziate ci si aspetta che partano entrambe.</summary>
    private int SelectedCount => SelectedOrPrimary().Count;

    /// <summary>true quando il job selezionato ha il versioning attivo (abilita il pulsante Versioni).</summary>
    public bool IsVersionedJobSelected
    {
        get
        {
            if (_selectedJob is null) return false;
            return _selectedJob.Model.Versioned;
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(StatusText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusText => IsBusy
        ? Loc.Instance[_preflighting ? "Status_Preflight" : "Status_Running"]
        : string.Format(Loc.Instance["Status_JobsConfigured"], Jobs.Count);

    public RelayCommand PreviewSelectedCommand { get; }
    public RelayCommand RunSelectedCommand { get; }
    public RelayCommand RunAllCommand { get; }
    public RelayCommand VerifySelectedCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand RefreshCommand { get; }

    // --- Esecuzione: il pulsante premuto si trasforma in "Annulla" ---
    private enum RunKind { None, Preview, Selected, All, Verify }
    private RunKind _running = RunKind.None;

    public string PreviewLabel => _running == RunKind.Preview ? Loc.Instance["Common_Cancel"] : Loc.Instance["Main_Preview"];
    // Con piu' righe evidenziate l'etichetta dice quante ne prende: «Avvia selezionati (2)».
    // Con una sola resta la frase di sempre, che non deve diventare «Avvia selezionati (1)».
    public string RunSelectedLabel => _running == RunKind.Selected
        ? Loc.Instance["Common_Cancel"]
        : SelectedCount > 1
            ? string.Format(Loc.Instance["Main_RunSelectedMany"], SelectedCount)
            : Loc.Instance["Main_RunSelected"];
    public string RunAllLabel => _running == RunKind.All ? Loc.Instance["Common_Cancel"] : Loc.Instance["Main_RunAll"];
    public string VerifyLabel => _running == RunKind.Verify
        ? Loc.Instance["Common_Cancel"]
        : SelectedCount > 1
            ? string.Format(Loc.Instance["Main_VerifyMany"], SelectedCount)
            : Loc.Instance["Main_Verify"];
    public bool PreviewCancel => _running == RunKind.Preview;
    public bool RunSelectedCancel => _running == RunKind.Selected;
    public bool RunAllCancel => _running == RunKind.All;
    public bool VerifyCancel => _running == RunKind.Verify;

    private void SetRunning(RunKind k)
    {
        _running = k;
        RaiseRunButtons();
        CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseRunButtons()
    {
        OnPropertyChanged(nameof(PreviewLabel));
        OnPropertyChanged(nameof(RunSelectedLabel));
        OnPropertyChanged(nameof(RunAllLabel));
        OnPropertyChanged(nameof(VerifyLabel));
        OnPropertyChanged(nameof(PreviewCancel));
        OnPropertyChanged(nameof(RunSelectedCancel));
        OnPropertyChanged(nameof(RunAllCancel));
        OnPropertyChanged(nameof(VerifyCancel));
    }

    private bool CanToggle(RunKind k)
    {
        if (_running == k) return true;             // posso annullare la mia operazione
        if (_running != RunKind.None) return false; // un'altra è in corso
        return k == RunKind.All ? Jobs.Count > 0 : SelectedJob is not null;
    }

    private void Toggle(RunKind k, bool fromTray = false)
    {
        switch (RunToggle.Decide(_running == k, _running != RunKind.None && _running != k, fromTray))
        {
            case ToggleAction.AskToCancel:
                if (ConfirmCancel()) _cts?.Cancel();
                return;
            case ToggleAction.Ignore:
                return;
        }
        if (_preflighting) return;
        _ = StartAsync(k);
    }

    /// <summary>«Avvia tutti» dal menu dell'icona: parte solo se non c'e' niente in corso. La voce
    /// del menu non diventa «Annulla», quindi non deve mai interrompere niente.</summary>
    public void RunAllFromTray()
    {
        if (CanToggle(RunKind.All)) Toggle(RunKind.All, fromTray: true);
    }

    /// <summary>Chiede se interrompere davvero l'operazione in corso. Predefinito No: chi preme
    /// Invio o Spazio per abitudine non interrompe un backup di un'ora.</summary>
    private static bool ConfirmCancel()
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var body = Loc.Instance["Run_ConfirmCancel"];
        var title = Loc.Instance["Run_ConfirmCancelTitle"];
        var answer = owner is not null && owner.IsVisible
            ? System.Windows.MessageBox.Show(owner, body, title,
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No)
            : System.Windows.MessageBox.Show(body, title,
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No);
        return answer == System.Windows.MessageBoxResult.Yes;
    }

    private async Task StartAsync(RunKind k)
    {
        if (k == RunKind.Verify) { await RunVerifyAsync(); return; }
        var dryRun = k == RunKind.Preview;
        List<JobViewModel> jobs;
        if (k == RunKind.All)
        {
            jobs = Jobs.Where(j => j.Enabled).ToList();
        }
        else
        {
            jobs = SelectedOrPrimary();
        }
        if (jobs.Count == 0) return;

        // Pre-check: nessuno stato di esecuzione è ancora impostato qui, quindi un return basta
        // per tornare all'idle. Preview e --run-all (non-interattivo) saltano il controllo.
        if (!await ConfirmPreflightAsync(jobs, dryRun)) return;

        await RunJobsAsync(jobs, dryRun, k);
    }

    // I controlli pre-avvio leggono il registro eventi di Windows (secondi, su un registro
    // grande) e misurano la sorgente: sul thread della UI congelavano la finestra prima ancora
    // della prima riga di log ("non risponde"). Girano su un thread di lavoro; la finestra di
    // conferma, se serve, resta sulla UI.
    private bool _preflighting;

    private async Task<bool> ConfirmPreflightAsync(IReadOnlyList<JobViewModel> jobs, bool dryRun)
    {
        if (dryRun || !_host.Config.Settings.PreflightEnabled)
            return true;

        long minFree = (long)_host.Config.Settings.MinFreeSpaceMb * 1024 * 1024;
        var budget = TimeSpan.FromSeconds(2);
        var models = jobs.Select(j => (j.Name, j.Model)).ToList();
        List<string> messages;
        List<(string Path, DateTime Until)> acknowledgeable;

        _preflighting = true;
        IsBusy = true;
        try
        {
            (messages, acknowledgeable) = await Task.Run(() =>
            {
                var list = new List<string>();
                var disks = new List<(string, DateTime)>();
                foreach (var (name, job) in models)
                {
                    var inputs = PreflightCollector.Collect(job, minFree, budget);
                    foreach (var warn in PreflightChecker.Evaluate(inputs))
                        list.Add($"- {name}: {Loc.Instance[warn.MessageKey]} {warn.Detail}".TrimEnd());
                    disks.AddRange(PreflightChecker.AcknowledgeableDisks(job, inputs));
                }
                return (list, disks);
            });
        }
        finally
        {
            _preflighting = false;
            IsBusy = false;
        }

        if (messages.Count == 0)
            return true;

        var body = Loc.Instance["Preflight_ConfirmIntro"] + "\n\n" + string.Join("\n", messages);
        var window = new PreflightWindow(body, canAcknowledge: acknowledgeable.Count > 0);
        // Finestra ridotta nella barra di notifica (avvio da «Avvia tutti» dell'icona): una finestra
        // figlia di una finestra nascosta non si vedrebbe. Allora sta da sola, in primo piano.
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
            window.Owner = owner;
        else
        {
            window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            window.Topmost = true;
        }
        window.ShowDialog();

        if (window.Choice == PreflightChoice.AcknowledgeAndStart)
        {
            // Si segna, per ogni disco, fino all'ultimo errore mostrato: quelli successivi
            // avviseranno di nuovo. Il disco si riconosce dal volume, non dalla lettera.
            foreach (var (path, until) in acknowledgeable)
                if (VolumeIdentity.ForPath(path)?.VolumeId is { } volumeId)
                    _host.DiskEventAcks.Acknowledge(volumeId, until);
            DiskEventLog.ClearCache();
        }
        return window.Choice != PreflightChoice.Cancel;
    }

    /// <summary>Ricostruisce la collezione dei job dalla configurazione corrente.</summary>
    public void ReloadJobs()
    {
        Jobs.Clear();
        foreach (var job in _host.Config.Jobs)
            Jobs.Add(new JobViewModel(job));
        ReloadLastResults();
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>Ricarica da disco l'ultimo esito di ogni job (anche da backup pianificati).</summary>
    public void ReloadLastResults()
    {
        var map = _host.Results.Load();
        foreach (var jvm in Jobs)
            jvm.ApplyLastResult(map.TryGetValue(jvm.Name, out var r) ? r : null);
        EvaluateHealth(map);
    }

    /// <summary>Calcola la salute di ogni job e aggiorna il banner di avviso.</summary>
    private void EvaluateHealth(IReadOnlyDictionary<string, JobLastResult> results)
    {
        var names = Jobs.Select(j => j.Name).ToList();
        // Quali job hanno il disco atteso non collegato: lo stesso criterio con cui il runner
        // li salta (CheckDestination esenta le destinazioni di rete e IsAway copre disco
        // sbagliato E disco assente). Serve a non far invecchiare in allarme un job che
        // semplicemente attende il suo disco.
        var away = Jobs
            .Where(j => VolumeGuard.IsAway(
                VolumeIdentity.CheckDestination(j.Model.Destination, j.Model.DestinationVolumeId)))
            .Select(j => j.Name)
            .ToHashSet();
        var health = StaleBackupEvaluator.Evaluate(names, results, _host.Config.Settings.StaleAfterDays, DateTime.Now, away);
        var interrupted = JobLockFile.GetInterrupted(_host.LockFolder, names);

        // Il dettaglio per job sta nel tooltip dell'icona sulla riga; il banner riporta solo i conteggi.
        int nInterrupted = 0, nFailed = 0, nStale = 0, nHardware = 0;
        for (int i = 0; i < Jobs.Count && i < health.Count; i++)
        {
            var jvm = Jobs[i];

            // Il lock file di un job IN ESECUZIONE in questo processo è legittimo:
            // non è un residuo di crash, non va segnalato come "interrotto".
            if (interrupted.Contains(jvm.Name) && !jvm.IsRunning)
            {
                jvm.Health = BackupHealth.Interrupted;
                jvm.HealthTooltip = Loc.Instance["Health_Interrupted"];
                nInterrupted++;
                continue;
            }

            var h = health[i].Health;
            jvm.Health = h;
            // L'errore hardware viene prima del fallimento generico: il consiglio non e'
            // "riprova" ma "controlla il supporto", e il dettaglio (codice, percorso) aiuta.
            if (h == BackupHealth.HardwareError)
            {
                var detail = results.TryGetValue(jvm.Name, out var hr) ? hr.HardwareErrorDetail ?? "" : "";
                jvm.HealthTooltip = string.Format(Loc.Instance["Health_Hardware"], detail).Trim();
                nHardware++;
            }
            else if (h == BackupHealth.Failed)
            {
                // Mirror fermato dalla guardia: nessun errore di copia, il rimedio e' una decisione
                // (avviare il job dalla finestra e confermare, o alzare la soglia). Il tooltip lo dice.
                // Disco pieno: nemmeno quello e' un errore di copia, e il dettaglio e' gia' la frase
                // completa (dove, quante versioni, quanto occupano, come fare posto).
                results.TryGetValue(jvm.Name, out var fr);
                jvm.HealthTooltip = fr switch
                {
                    { DeletionsBlocked: true } => string.Format(Loc.Instance["Health_DeletionsBlocked"],
                        fr.DeletionsBlockedDetail ?? "").Trim(),
                    { DiskFull: true, DiskFullDetail: { Length: > 0 } detail } =>
                        string.Format(Loc.Instance["Health_DiskFull"], detail).Trim(),
                    _ => Loc.Instance["Health_Failed"],
                };
                nFailed++;
            }
            else if (h == BackupHealth.Stale)
            {
                var days = results.TryGetValue(jvm.Name, out var r)
                    ? (int)(DateTime.Now - r.FinishedAt).TotalDays
                    : 0;
                jvm.HealthTooltip = days > 0
                    ? string.Format(Loc.Instance["Health_StaleN"], days)
                    : Loc.Instance["Health_Stale"];
                nStale++;
            }
            else if (h == BackupHealth.Waiting)
            {
                var days = results.TryGetValue(jvm.Name, out var r)
                    ? (int)(DateTime.Now - r.FinishedAt).TotalDays
                    : 0;
                jvm.HealthTooltip = days > 0
                    ? string.Format(Loc.Instance["Health_WaitingN"], days)
                    : Loc.Instance["Health_Waiting"];
            }
            else
            {
                jvm.HealthTooltip = null;
            }
        }

        var parts = new List<string>();
        if (nInterrupted > 0) parts.Add(string.Format(Loc.Instance["Health_CountInterrupted"], nInterrupted));
        if (nHardware > 0) parts.Add(string.Format(Loc.Instance["Health_CountHardware"], nHardware));
        if (nFailed > 0) parts.Add(string.Format(Loc.Instance["Health_CountFailed"], nFailed));
        if (nStale > 0) parts.Add(string.Format(Loc.Instance["Health_CountStale"], nStale));

        // Prima del rientro anticipato: il pulsante "Riattiva dischi" dipende dai dischi a
        // riposo, non dal banner — un disco bloccato senza job in allarme va comunque liberato.
        var faulted = _host.FaultedDisks.Load(DateTime.Now);
        HasFaultedDisks = faulted.Count > 0;
        FaultedDisksSummary = string.Join(Environment.NewLine, faulted.Select(d =>
            string.Format(Loc.Instance["Main_ReenableDisksItem"],
                string.IsNullOrEmpty(d.Label) ? RoboKeep.Core.CoreLoc.S("Volume_Unknown") : d.Label,
                d.Root, d.Since.ToString("g"), d.Detail)));

        if (parts.Count == 0)
        {
            HealthBannerText = "";
            HealthBannerVisible = false;
            return;
        }
        HealthBannerText = string.Join(" · ", parts);
        HealthBannerVisible = true;
    }

    /// <summary>Riallinea la lista dei job nella config e salva su disco.</summary>
    public void PersistJobs()
    {
        _host.Config.Jobs = Jobs.Select(j => j.Model).ToList();
        _host.SaveConfig();
        OnPropertyChanged(nameof(StatusText));
        // Modificare il job selezionato (es. attivare il versioning) può cambiare lo stato del pulsante Versioni.
        OnPropertyChanged(nameof(IsVersionedJobSelected));
    }

    // Accodamento thread-safe: chiamato dai thread di lettura del processo (NON dalla UI).
    private void Enqueue(string line)
    {
        lock (_bufLock) _buffer.AppendLine(line);
    }

    // Riversa sulla UI il testo accumulato finora (chiamato sul thread UI dal timer).
    private void FlushLog()
    {
        string text;
        lock (_bufLock)
        {
            if (_buffer.Length == 0) return;
            text = _buffer.ToString();
            _buffer.Clear();
        }
        LogFlushed?.Invoke(text);
    }

    private async Task RunJobsAsync(IReadOnlyList<JobViewModel> jobs, bool dryRun, RunKind kind)
    {
        IsBusy = true;
        SetRunning(kind);
        _cts = new CancellationTokenSource();
        var runner = _host.BuildRunner();
        IProgress<string> progress = new DirectProgress(Enqueue);
        _logTimer.Start();
        // Piu' job in una volta: le notifiche di Windows si accodano, una ogni cinque secondi, e
        // restano tutte nel Centro notifiche da chiudere a mano. Si contano e si notifica una
        // volta sola alla fine (vedi RunSummary).
        var batch = jobs.Count > 1;
        List<string> okNames = new(), failedNames = new();
        var skipped = 0;
        try
        {
            var header = dryRun ? Loc.Instance["Run_Preview"] : Loc.Instance["Run_Execution"];
            Enqueue($"===== {header} {DateTime.Now:HH:mm:ss} =====");
            foreach (var jvm in jobs)
            {
                if (_cts.IsCancellationRequested) break;
                jvm.IsRunning = true;
                jvm.LastStatus = dryRun ? Loc.Instance["Run_PreviewStatus"] : Loc.Instance["Run_InProgress"];
                Enqueue($"--- {jvm.Name} ---");
                try
                {
                    // La guardia sulle cancellazioni chiede solo nei run veri: in anteprima non c'e'
                    // niente da fermare (nessun file viene toccato), il riepilogo lo dice e basta.
                    var result = await runner.RunJobAsync(jvm.Model, dryRun, progress, _cts.Token,
                        dryRun ? null : ConfirmDeletionsAsync);
                    if (result.AlreadyRunning)
                        jvm.LastStatus = result.Status;   // «GIÀ IN ESECUZIONE»
                    else if (result.Skipped)
                        jvm.LastStatus = Loc.Instance["Run_SkippedDisk"];
                    else if (result.DeletionsBlocked || result.DiskFull)
                        jvm.LastStatus = result.Status;   // «BLOCCATO: troppe cancellazioni», «Disco pieno»
                    else if (dryRun)
                        jvm.LastStatus = $"{Loc.Instance["Run_OK"]} · {Loc.Instance["Run_Preview"]}";
                    else
                        jvm.LastStatus = RunStatus.Format(result.Success, result.FilesCopied, result.FilesSkipped,
                            result.FilesExtra, result.FilesFailed + result.DirsFailed, DateTime.Now);
                    Enqueue($"=> {jvm.Name}: {result.Status}");

                    // Nessun toast per un job saltato: non e' successo niente da notificare.
                    if (!dryRun && result.Skipped) skipped++;
                    else if (!dryRun)
                    {
                        (result.Success ? okNames : failedNames).Add(jvm.Name);
                        // In gruppo il successo non notifica: sei job erano sei banner in fila, che
                        // Windows mostra uno alla volta. Lo dice il riassunto in fondo. I falliti
                        // notificano sempre, col loro nome: li' si vuole sapere subito quale.
                        if (_host.Config.Settings.NotificationsEnabled
                            && (!batch || !result.Success)
                            && System.Windows.Application.Current?.MainWindow is MainWindow mw)
                        {
                            var msg = result.Success
                                ? Loc.Instance["Toast_Ok"]
                                : Loc.Instance["Toast_Error"];
                            // Una notifica rifiutata da Windows non e' un errore del job: prima finiva nel
                            // catch qui sotto e un backup riuscito compariva come «ERRORE».
                            if (!mw.ShowJobToast(jvm.Name, msg))
                                Enqueue($"   {jvm.Name}: {Loc.Instance["Toast_Failed"]}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    jvm.LastStatus = Loc.Instance["Run_Cancelled"];
                    Enqueue($"!! {jvm.Name}: {Loc.Instance["Run_CancelledUser"]}");
                }
                catch (Exception ex)
                {
                    // Conta come fallito anche qui, altrimenti il riassunto direbbe «6 completati»
                    // con un job caduto per un errore imprevisto.
                    if (!dryRun) failedNames.Add(jvm.Name);
                    jvm.LastStatus = Loc.Instance["Run_Error"];
                    Enqueue($"!! {jvm.Name}: {ex.Message}");
                }
                finally
                {
                    jvm.IsRunning = false;
                    // Ricalcola icone di avviso e banner man mano che i job finiscono, non solo
                    // a fine batch: un job appena riuscito non deve restare segnato in ritardo.
                    // Solo nei run reali: in anteprima l'esito non viene persistito e il reload
                    // sovrascriverebbe l'etichetta "Anteprima" appena impostata.
                    if (!dryRun)
                        ReloadLastResults();
                }
            }

            // Riassunto del gruppo. Non dopo un annullamento: chi ha premuto «Annulla» sa gia'
            // com'e' finita, e una notifica sarebbe solo rumore.
            if (batch && !dryRun && _cts?.IsCancellationRequested != true)
                ShowBatchToast(okNames, failedNames, skipped);
        }
        finally
        {
            _logTimer.Stop();
            FlushLog();
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
            SetRunning(RunKind.None);
            // Ricarica gli esiti e rivaluta la salute: il banner/le righe riflettono l'esito appena ottenuto.
            ReloadLastResults();
        }
    }

    /// <summary>Una notifica sola per tutto il gruppo, al posto di una per job.</summary>
    private void ShowBatchToast(IReadOnlyList<string> okNames, IReadOnlyList<string> failedNames, int skipped)
    {
        if (!RunSummary.Worth(okNames.Count, failedNames.Count)) return;
        if (!_host.Config.Settings.NotificationsEnabled) return;
        if (System.Windows.Application.Current?.MainWindow is not MainWindow mw) return;

        var title = Loc.Instance[RunSummary.TitleKey(failedNames.Count)];
        if (!mw.ShowJobToast(title, RunSummary.Body(okNames, failedNames, skipped)))
            Enqueue($"   {Loc.Instance["Toast_Failed"]}");
    }

    /// <summary>
    /// Domanda di conferma quando un mirror sta per cancellare piu' della soglia del job: e' il
    /// momento in cui si scopre che la sorgente si e' svuotata per errore. La chiama il runner da
    /// una continuazione su thread di background, percio' la finestra si apre sul thread della UI;
    /// l'attesa della risposta ferma il job, non l'applicazione. Se il dispatcher e' gia' fermo
    /// (app in chiusura) l'operazione risulta annullata e il task esce con OperationCanceled: per
    /// il runner e' un annullamento — il job non parte, che e' l'esito prudente.
    /// </summary>
    private Task<bool> ConfirmDeletionsAsync(MirrorDeleteEstimate estimate)
    {
        var app = System.Windows.Application.Current;
        if (app is null) return Task.FromResult(false);

        return app.Dispatcher.InvokeAsync(() =>
        {
            // Con le versioni i file escono dal backup corrente ma restano nella versione finche' la
            // ritenzione la conserva: stessi numeri, frase diversa, perche' «cancellerebbe» sarebbe falso.
            var body = string.Format(
                Loc.Instance[estimate.Versioned ? "Guard_ConfirmVersioned" : "Guard_Confirm"],
                estimate.JobName, estimate.Extra, estimate.Total, estimate.Percent, estimate.Destination);
            var title = Loc.Instance[estimate.Versioned ? "Guard_ConfirmTitleVersioned" : "Guard_ConfirmTitle"];
            var owner = app.MainWindow;
            // Finestra ridotta a icona nella barra di notifica: una MessageBox modale a una finestra
            // invisibile non si vede e non si puo' rispondere — il job resterebbe appeso per sempre.
            // Si riporta a galla la finestra, poi si chiede.
            if (owner is not null && !owner.IsVisible)
            {
                owner.Show();
                owner.Activate();
            }
            // Predefinito No: chi preme Invio per abitudine non cancella il backup.
            var answer = owner is not null
                ? System.Windows.MessageBox.Show(owner, body, title,
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No)
                : System.Windows.MessageBox.Show(body, title,
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxResult.No);
            return answer == System.Windows.MessageBoxResult.Yes;
        }).Task;
    }

    /// <summary>Verifica integrità manuale del job selezionato: hash sorgente vs destinazione.</summary>
    private async Task RunVerifyAsync()
    {
        var sel = SelectedJob;
        if (sel is null) return;
        var job = sel.Model;

        IsBusy = true;
        SetRunning(RunKind.Verify);
        _cts = new CancellationTokenSource();
        IProgress<string> progress = new DirectProgress(Enqueue);
        _logTimer.Start();
        var started = DateTime.Now;
        VerifyLogRecorder? verifyLog = null;
        try
        {
            Enqueue($"===== {Loc.Instance["Verify_Header"]} {DateTime.Now:HH:mm:ss} — {sel.Name} =====");
            var target = VerifyTargetResolver.Resolve(job);
            if (target is null)
            {
                Enqueue(RoboKeep.Core.CoreLoc.S("Verify_NothingToVerify"));
                return;
            }
            // La verifica legge per ore: il PC non deve sospendersi per inattivita' nel mezzo.
            using var awake = SleepBlocker.Acquire($"RoboKeep: {sel.Name}");
            // Anche la verifica manuale lascia il suo log, collegato alla voce di cronologia.
            verifyLog = new VerifyLogRecorder(progress);
            var vr = await IntegrityVerifier.VerifyAsync(job.Source, target, job.ExcludeFiles, job.ExcludeDirs, verifyLog, _cts.Token);
            BackupRunner.ReportVerify(vr, verifyLog);
            _host.History.Append(RunHistoryEntry.ForVerify(job.Name, started, vr,
                verifyLog.Save(new LogService(_host.Config.Settings), job.Name, started, vr)));
        }
        catch (OperationCanceledException)
        {
            Enqueue($"!! {sel.Name}: {Loc.Instance["Run_CancelledUser"]}");
        }
        catch (Exception ex) when (DiskError.IsUnreadable(ex))
        {
            var rec = verifyLog ?? new VerifyLogRecorder(progress);
            rec.Report(string.Format(RoboKeep.Core.CoreLoc.S("Hw_VerifyStop"), ex.Message));
            rec.Report(RoboKeep.Core.CoreLoc.S("Hw_Advice"));
            _host.History.Append(RunHistoryEntry.ForVerifyInterrupted(job.Name, started,
                rec.Save(new LogService(_host.Config.Settings), job.Name, started, null)));
        }
        catch (Exception ex)
        {
            Enqueue($"!! {sel.Name}: {ex.Message}");
            // Anche qui la verifica non fatta lascia il suo log e la sua voce di cronologia (e
            // resta da fare). verifyLog e' ancora null se l'errore e' arrivato prima di cominciare.
            var rec = verifyLog ?? new VerifyLogRecorder(progress);
            rec.Report(string.Format(RoboKeep.Core.CoreLoc.S("Verify_Failed"), ex.Message));
            _host.History.Append(RunHistoryEntry.ForVerifyInterrupted(job.Name, started,
                rec.Save(new LogService(_host.Config.Settings), job.Name, started, null)));
        }
        finally
        {
            _logTimer.Stop();
            FlushLog();
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
            SetRunning(RunKind.None);
        }
    }

    /// <summary>Controlla se esiste una versione nuova. <paramref name="force"/> ignora la
    /// cadenza (pulsante "Controlla ora"). La data e l'esito dell'ultimo tentativo stanno in
    /// update-state.json: qui la configurazione non si salva mai, perche' le Impostazioni
    /// possono essere aperte con modifiche non confermate.</summary>
    public async Task<UpdateCheckOutcome> CheckForUpdatesAsync(bool force)
    {
        var state = _host.UpdateState.Load();
        if (!force && (_host.Config.Settings.UpdateCheck != true
                       || !UpdateChecker.IsDue(state.LastCheck, state.LastCheckFailed, DateTime.Now)))
            return UpdateCheckOutcome.Skipped;

        var (info, status) = await UpdateChecker.FetchWithStatusAsync();
        // La data si aggiorna anche quando il controllo non riesce, ma segnata come fallita: si
        // riprova fra RetryInterval invece di restare fermi 24 ore.
        _host.UpdateState.Save(new UpdateState(DateTime.Now, info is null));
        if (info is null)
            return status == FetchStatus.NotFound ? UpdateCheckOutcome.NotPublic : UpdateCheckOutcome.Unreachable;

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

    /// <summary>Apre nel browser la pagina della release trovata (pulsante "Novita'").</summary>
    public void OpenUpdatePage() => OpenUrl(_update?.ReleaseUrl);

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

    /// <summary>Cartella Download dell'utente; se non esiste, %TEMP% (lo dice la finestra).</summary>
    private static string DownloadsFolder()
    {
        var dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(dl) ? dl : Path.GetTempPath();
    }

    /// <summary>Apre un indirizzo nel browser. Solo https: l'indirizzo viene da una risposta di
    /// rete, e UseShellExecute su uno schema qualunque aprirebbe programmi o file locali.</summary>
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
    }

    private static void RevealInExplorer(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); } catch { }
    }
}

/// <summary>IProgress che esegue il callback in modo sincrono sul thread chiamante (senza marshaling sulla UI).</summary>
internal sealed class DirectProgress : IProgress<string>
{
    private readonly Action<string> _action;
    public DirectProgress(Action<string> action) => _action = action;
    public void Report(string value) => _action(value);
}
