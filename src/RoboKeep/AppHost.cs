using System.IO;
using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep;

/// <summary>
/// Composizione dei servizi e bootstrap condiviso tra GUI e CLI.
/// </summary>
public sealed class AppHost
{
    public AppConfig Config { get; private set; }
    public ConfigStore Store { get; }
    public CredentialService Credentials { get; }
    public LastResultStore Results { get; }
    public ForceCopyHashStore ForceCopyHashes { get; }
    public RunHistoryStore History { get; }
    public FaultedDiskStore FaultedDisks { get; }
    public string LockFolder => Path.Combine(Store.DirectoryPath, "locks");

    private AppHost(ConfigStore store, AppConfig config)
    {
        Store = store;
        Config = config;
        if (string.IsNullOrWhiteSpace(config.Settings.LogRoot))
            config.Settings.LogRoot = Path.Combine(store.DirectoryPath, "logs");
        if (string.IsNullOrWhiteSpace(config.Settings.TempRoot))
            config.Settings.TempRoot = Path.Combine(store.DirectoryPath, "temp");
        Credentials = new CredentialService(config.Settings.CredentialScope);
        Results = new LastResultStore(Path.Combine(store.DirectoryPath, "lastresults.json"));
        ForceCopyHashes = new ForceCopyHashStore(Path.Combine(store.DirectoryPath, "forcecopy-hashes.json"));
        History = new RunHistoryStore(Path.Combine(store.DirectoryPath, "runhistory.json"));
        FaultedDisks = new FaultedDiskStore(Path.Combine(store.DirectoryPath, "faulted-disks.json"));
    }

    public static AppHost Load(string? configPath = null)
    {
        var path = configPath ?? Path.Combine(AppDataLocator.PrepareDataRoot(), "config.json");
        var store = new ConfigStore(path);
        return new AppHost(store, store.Load());
    }

    public void SaveConfig() => Store.Save(Config);

    public void ReloadConfig() => Config = Store.Load();

    /// <summary>Costruisce l'orchestratore con la configurazione corrente.</summary>
    public BackupRunner BuildRunner()
    {
        var planner = new ForceCopyPlanner(ForceCopyHashes);
        var runner = new RobocopyRunner(forceCopyPlanner: planner);
        var log = new LogService(Config.Settings);
        var email = new EmailService(Credentials);
        var snapshots = new SnapshotService(runner);
        return new BackupRunner(Config, runner, log, email, Credentials, Results, snapshots, LockFolder,
            Path.Combine(Store.DirectoryPath, "vss"), History, faultedDisks: FaultedDisks);
    }

    /// <summary>
    /// Esecuzione headless per la riga di comando.
    /// Supporta: <c>--run-all</c>, <c>--job "Nome"</c>, <c>--dry-run</c>.
    /// Restituisce un exit code (0 = tutti i job riusciti).
    /// </summary>
    public async Task<int> RunHeadlessAsync(CliOptions options)
    {
        var runner = BuildRunner();
        var progress = new Progress<string>(Console.WriteLine);

        IReadOnlyList<JobResult> results;
        if (options.RunAll)
        {
            results = await runner.RunAllAsync(options.DryRun, progress);
        }
        else
        {
            var job = runner.FindJob(options.JobName!);
            if (job is null)
            {
                Console.Error.WriteLine($"Job '{options.JobName}' non trovato nella configurazione.");
                return 2;
            }
            results = new[] { await runner.RunJobAsync(job, options.DryRun, progress) };
        }

        foreach (var r in results)
        {
            var esito = r.Skipped ? "SALTATO" : r.Success ? "OK" : "ERRORE";
            Console.WriteLine($"{r.JobName}: {esito} - {r.Status}");
        }

        // Saltare non e' fallire: un job il cui disco non e' collegato non deve far risultare
        // fallita l'attivita' pianificata (era la causa degli allarmi rossi ogni notte).
        return results.All(r => r.Success || r.Skipped) ? 0 : 1;
    }
}
