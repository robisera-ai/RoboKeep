using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using RobocopySW.Infra;
using RobocopySW.Localization;

namespace RobocopySW.ViewModels;

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

        RunSelectedCommand = new AsyncRelayCommand(_ => RunSelectedAsync(false), _ => SelectedJob is not null && !IsBusy);
        PreviewSelectedCommand = new AsyncRelayCommand(_ => RunSelectedAsync(true), _ => SelectedJob is not null && !IsBusy);
        RunAllCommand = new AsyncRelayCommand(_ => RunAllAsync(false), _ => Jobs.Count > 0 && !IsBusy);
        PreviewAllCommand = new AsyncRelayCommand(_ => RunAllAsync(true), _ => Jobs.Count > 0 && !IsBusy);
        StopCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        ClearLogCommand = new RelayCommand(() =>
        {
            lock (_bufLock) _buffer.Clear();
            LogCleared?.Invoke();
        });
        MoveUpCommand = new RelayCommand(() => MoveSelected(-1), () => CanMove(-1));
        MoveDownCommand = new RelayCommand(() => MoveSelected(+1), () => CanMove(+1));
        RefreshCommand = new RelayCommand(ReloadLastResults);

        Loc.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
    }

    // Salva la configurazione quando un job viene attivato/disattivato dalla griglia.
    private void OnJobPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JobViewModel.Enabled))
            PersistJobs();
    }

    private bool CanMove(int direction)
    {
        if (SelectedJob is null) return false;
        var i = Jobs.IndexOf(SelectedJob);
        var target = i + direction;
        return i >= 0 && target >= 0 && target < Jobs.Count;
    }

    private void MoveSelected(int direction)
    {
        var sel = SelectedJob;
        if (sel is null) return;
        var i = Jobs.IndexOf(sel);
        var target = i + direction;
        if (i < 0 || target < 0 || target >= Jobs.Count) return;

        Jobs.Move(i, target);
        SelectedJob = sel;          // mantiene la selezione sulla riga spostata
        PersistJobs();              // l'ordine dei job è la priorità di esecuzione
    }

    public AppHost Host => _host;
    public ObservableCollection<JobViewModel> Jobs { get; }

    /// <summary>true quando non ci sono job: la UI mostra lo stato vuoto.</summary>
    public bool IsEmpty => Jobs.Count == 0;

    /// <summary>Opposto di <see cref="IsEmpty"/>: c'è almeno un job.</summary>
    public bool HasJobs => Jobs.Count > 0;

    private JobViewModel? _selectedJob;
    public JobViewModel? SelectedJob
    {
        get => _selectedJob;
        set => SetField(ref _selectedJob, value);
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
        ? Loc.Instance["Status_Running"]
        : string.Format(Loc.Instance["Status_JobsConfigured"], Jobs.Count);

    public AsyncRelayCommand RunSelectedCommand { get; }
    public AsyncRelayCommand PreviewSelectedCommand { get; }
    public AsyncRelayCommand RunAllCommand { get; }
    public AsyncRelayCommand PreviewAllCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand RefreshCommand { get; }

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
    }

    /// <summary>Riallinea la lista dei job nella config e salva su disco.</summary>
    public void PersistJobs()
    {
        _host.Config.Jobs = Jobs.Select(j => j.Model).ToList();
        _host.SaveConfig();
        OnPropertyChanged(nameof(StatusText));
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

    private async Task RunSelectedAsync(bool dryRun)
    {
        if (SelectedJob is null) return;
        await RunJobsAsync(new[] { SelectedJob }, dryRun);
    }

    private async Task RunAllAsync(bool dryRun)
    {
        await RunJobsAsync(Jobs.Where(j => j.Enabled).ToList(), dryRun);
    }

    private async Task RunJobsAsync(IReadOnlyList<JobViewModel> jobs, bool dryRun)
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();
        var runner = _host.BuildRunner();
        IProgress<string> progress = new DirectProgress(Enqueue);
        _logTimer.Start();
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
                    var result = await runner.RunJobAsync(jvm.Model, dryRun, progress, _cts.Token);
                    if (dryRun)
                        jvm.LastStatus = $"{Loc.Instance["Run_OK"]} · {Loc.Instance["Run_Preview"]}";
                    else
                        jvm.LastStatus = RunStatus.Format(result.Success, result.FilesCopied, result.FilesSkipped,
                            result.FilesExtra, result.FilesFailed + result.DirsFailed, DateTime.Now);
                    Enqueue($"=> {jvm.Name}: {result.Status} (exit {result.ExitCode})");
                }
                catch (OperationCanceledException)
                {
                    jvm.LastStatus = Loc.Instance["Run_Cancelled"];
                    Enqueue($"!! {jvm.Name}: {Loc.Instance["Run_CancelledUser"]}");
                }
                catch (Exception ex)
                {
                    jvm.LastStatus = Loc.Instance["Run_Error"];
                    Enqueue($"!! {jvm.Name}: {ex.Message}");
                }
                finally
                {
                    jvm.IsRunning = false;
                }
            }
        }
        finally
        {
            _logTimer.Stop();
            FlushLog();
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }
}

/// <summary>IProgress che esegue il callback in modo sincrono sul thread chiamante (senza marshaling sulla UI).</summary>
internal sealed class DirectProgress : IProgress<string>
{
    private readonly Action<string> _action;
    public DirectProgress(Action<string> action) => _action = action;
    public void Report(string value) => _action(value);
}
