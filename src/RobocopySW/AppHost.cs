using RobocopySW.Core.Models;
using RobocopySW.Core.Services;

namespace RobocopySW;

/// <summary>
/// Composizione dei servizi e bootstrap condiviso tra GUI e CLI.
/// </summary>
public sealed class AppHost
{
    public AppConfig Config { get; private set; }
    public ConfigStore Store { get; }
    public CredentialService Credentials { get; }

    private AppHost(ConfigStore store, AppConfig config)
    {
        Store = store;
        Config = config;
        Credentials = new CredentialService();
    }

    public static AppHost Load(string? configPath = null)
    {
        var store = new ConfigStore(configPath ?? ConfigStore.DefaultConfigPath);
        return new AppHost(store, store.Load());
    }

    public void SaveConfig() => Store.Save(Config);

    public void ReloadConfig() => Config = Store.Load();

    /// <summary>Costruisce l'orchestratore con la configurazione corrente.</summary>
    public BackupRunner BuildRunner()
    {
        var runner = new RobocopyRunner();
        var log = new LogService(Config.Settings);
        var email = new EmailService(Credentials);
        return new BackupRunner(Config, runner, log, email, Credentials);
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
            Console.WriteLine($"{r.JobName}: {(r.Success ? "OK" : "ERRORE")} (exit {r.ExitCode}) - {r.Status}");

        return results.All(r => r.Success) ? 0 : 1;
    }
}
