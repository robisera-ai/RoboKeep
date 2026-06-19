using System.Collections.ObjectModel;
using System.Text;
using RobocopySW.Core.Models;
using RobocopySW.Infra;

namespace RobocopySW.ViewModels;

/// <summary>ViewModel principale: lista job, esecuzione e log live.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly StringBuilder _log = new();

    public MainViewModel(AppHost host)
    {
        _host = host;
        Jobs = new ObservableCollection<JobViewModel>();
        ReloadJobs();

        RunSelectedCommand = new AsyncRelayCommand(_ => RunSelectedAsync(false), _ => SelectedJob is not null && !IsBusy);
        PreviewSelectedCommand = new AsyncRelayCommand(_ => RunSelectedAsync(true), _ => SelectedJob is not null && !IsBusy);
        RunAllCommand = new AsyncRelayCommand(_ => RunAllAsync(false), _ => Jobs.Count > 0 && !IsBusy);
        PreviewAllCommand = new AsyncRelayCommand(_ => RunAllAsync(true), _ => Jobs.Count > 0 && !IsBusy);
        ClearLogCommand = new RelayCommand(() => { _log.Clear(); OnPropertyChanged(nameof(LogText)); });
    }

    public AppHost Host => _host;
    public ObservableCollection<JobViewModel> Jobs { get; }

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
        set { if (SetField(ref _isBusy, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    public string LogText => _log.ToString();

    public string StatusText => IsBusy ? "Esecuzione in corso…" : $"{Jobs.Count} job configurati";

    public AsyncRelayCommand RunSelectedCommand { get; }
    public AsyncRelayCommand PreviewSelectedCommand { get; }
    public AsyncRelayCommand RunAllCommand { get; }
    public AsyncRelayCommand PreviewAllCommand { get; }
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

    private void AppendLog(string line)
    {
        _log.AppendLine(line);
        OnPropertyChanged(nameof(LogText));
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
        var runner = _host.BuildRunner();
        var progress = new Progress<string>(AppendLog);
        try
        {
            AppendLog($"===== {(dryRun ? "ANTEPRIMA" : "ESECUZIONE")} {DateTime.Now:HH:mm:ss} =====");
            foreach (var jvm in jobs)
            {
                jvm.IsRunning = true;
                jvm.LastStatus = dryRun ? "anteprima…" : "in corso…";
                AppendLog($"--- {jvm.Name} ---");
                try
                {
                    var result = await runner.RunJobAsync(jvm.Model, dryRun, progress);
                    jvm.LastStatus = $"{(result.Success ? "OK" : "ERRORE")} · {result.FilesCopied} copiati · {result.FilesExtra} extra";
                    AppendLog($"=> {jvm.Name}: {result.Status} (exit {result.ExitCode})");
                }
                catch (Exception ex)
                {
                    jvm.LastStatus = "ERRORE";
                    AppendLog($"!! {jvm.Name}: {ex.Message}");
                }
                finally
                {
                    jvm.IsRunning = false;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
