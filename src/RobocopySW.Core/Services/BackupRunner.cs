using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>
/// Orchestratore di alto livello: per ciascun job collega connessione credenziali →
/// esecuzione robocopy → scrittura/archiviazione log → notifica email → disconnessione.
/// Usato sia dalla GUI sia dalla modalità CLI silenziosa.
/// </summary>
public sealed class BackupRunner
{
    private readonly AppConfig _config;
    private readonly RobocopyRunner _runner;
    private readonly LogService _log;
    private readonly EmailService _email;
    private readonly CredentialService _credentials;

    public BackupRunner(
        AppConfig config,
        RobocopyRunner runner,
        LogService log,
        EmailService email,
        CredentialService credentials)
    {
        _config = config;
        _runner = runner;
        _log = log;
        _email = email;
        _credentials = credentials;
    }

    /// <summary>Trova un job per nome (case-insensitive).</summary>
    public BackupJob? FindJob(string name) =>
        _config.Jobs.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Esegue un singolo job, gestendo credenziali, log ed email.</summary>
    public async Task<JobResult> RunJobAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var cred = string.IsNullOrEmpty(job.CredentialId)
            ? null
            : _config.Credentials.FirstOrDefault(c => c.Id == job.CredentialId);

        var connected = false;
        try
        {
            if (cred is not null)
            {
                _credentials.Connect(cred.Host, cred.User, _credentials.Unprotect(cred.PasswordProtected));
                connected = true;
            }

            var run = await _runner.RunAsync(job, dryRun, progress, ct).ConfigureAwait(false);

            // Riepilogo nostro, leggibile e in italiano (l'output nativo di robocopy ha le
            // intestazioni localizzate che sbordano dalle colonne).
            var recap = BuildRecap(run.Result, dryRun);
            foreach (var line in recap)
                progress?.Report(line);

            var logContent = run.Output + Environment.NewLine + string.Join(Environment.NewLine, recap);
            run.Result.LogPath = _log.WriteAndArchive(job.Name, logContent, run.Result.StartedAt);

            try
            {
                await _email.SendResultAsync(_config.Settings.Email, run.Result, run.Result.LogPath)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progress?.Report($"[email] invio non riuscito: {ex.Message}");
            }

            return run.Result;
        }
        finally
        {
            if (connected && cred is not null)
                _credentials.Disconnect(cred.Host);
        }
    }

    private static string[] BuildRecap(JobResult r, bool dryRun)
    {
        var titolo = dryRun ? "RIEPILOGO ANTEPRIMA (RobocopySW)" : "RIEPILOGO (RobocopySW)";
        var extraNota = r.FilesExtra > 0
            ? (dryRun ? "  (verrebbero rimossi in mirror)" : "  (in dest, non in sorgente)")
            : "";
        return new[]
        {
            "",
            "====== " + titolo + " ======",
            $"Esito           : {(r.Success ? "OK" : "ERRORE")} (exit {r.ExitCode}) - {r.Status}",
            $"Cartelle copiate: {r.DirsCopied}",
            $"File copiati    : {r.FilesCopied}",
            $"File invariati  : {r.FilesSkipped}",
            $"File extra      : {r.FilesExtra}{extraNota}",
            $"File falliti    : {r.FilesFailed}",
            $"Durata          : {r.Duration:hh\\:mm\\:ss}",
            "==========================================",
        };
    }

    /// <summary>Esegue tutti i job abilitati in sequenza, poi pulisce i log vecchi.</summary>
    public async Task<IReadOnlyList<JobResult>> RunAllAsync(
        bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var results = new List<JobResult>();
        foreach (var job in _config.Jobs.Where(j => j.Enabled))
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"=== Job: {job.Name} ===");
            results.Add(await RunJobAsync(job, dryRun, progress, ct).ConfigureAwait(false));
        }

        if (!dryRun)
            _log.CleanupOldLogs(DateTime.Now);

        return results;
    }
}
