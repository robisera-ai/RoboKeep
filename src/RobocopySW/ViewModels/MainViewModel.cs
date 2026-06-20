using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using RobocopySW.Infra;

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
        Jobs.CollectionChanged += (_, _) =>
        {
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

    public string StatusText => IsBusy ? "Esecuzione in corso…" : $"{Jobs.Count} job configurati";

    public AsyncRelayCommand RunSelectedCommand { get; }
    public AsyncRelayCommand PreviewSelectedCommand { get; }
    public AsyncRelayCommand RunAllCommand { get; }
    public AsyncRelayCommand PreviewAllCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    /// <summary>Ricostruisce la collezione dei job dalla configurazione corrente.</summary>
    public void ReloadJobs()
    {
        Jobs.Clear();
        foreach (var job in _host.Config.Jobs)
            Jobs.Add(new JobViewModel(job));
        OnPropertyChanged(nameof(StatusText));
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
            Enqueue($"===== {(dryRun ? "ANTEPRIMA" : "ESECUZIONE")} {DateTime.Now:HH:mm:ss} =====");
            foreach (var jvm in jobs)
            {
                if (_cts.IsCancellationRequested) break;
                jvm.IsRunning = true;
                jvm.LastStatus = dryRun ? "anteprima…" : "in corso…";
                Enqueue($"--- {jvm.Name} ---");
                try
                {
                    var result = await runner.RunJobAsync(jvm.Model, dryRun, progress, _cts.Token);
                    jvm.LastStatus = $"{(result.Success ? "OK" : "ERRORE")} · {result.FilesCopied} copiati · {result.FilesExtra} extra";
                    Enqueue($"=> {jvm.Name}: {result.Status} (exit {result.ExitCode})");
                }
                catch (OperationCanceledException)
                {
                    jvm.LastStatus = "annullato";
                    Enqueue($"!! {jvm.Name}: annullato dall'utente");
                }
                catch (Exception ex)
                {
                    jvm.LastStatus = "ERRORE";
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
