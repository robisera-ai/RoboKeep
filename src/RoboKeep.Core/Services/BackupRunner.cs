using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

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
    private readonly LastResultStore? _results;
    private readonly SnapshotService? _snapshots;
    private readonly string? _lockFolder;
    private readonly string? _vssSessionRoot;

    public BackupRunner(
        AppConfig config,
        RobocopyRunner runner,
        LogService log,
        EmailService email,
        CredentialService credentials,
        LastResultStore? results = null,
        SnapshotService? snapshots = null,
        string? lockFolder = null,
        string? vssSessionRoot = null)
    {
        _config = config;
        _runner = runner;
        _log = log;
        _email = email;
        _credentials = credentials;
        _results = results;
        _snapshots = snapshots;
        _lockFolder = lockFolder;
        _vssSessionRoot = vssSessionRoot;
    }

    /// <summary>Trova un job per nome (case-insensitive).</summary>
    public BackupJob? FindJob(string name) =>
        _config.Jobs.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Esegue un singolo job, gestendo credenziali, log ed email.</summary>
    public async Task<JobResult> RunJobAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        // Il lock file persiste se il processo viene terminato brutalmente; viene rimosso nel
        // finally (via using) quando il run termina normalmente (successo, errore o cancel).
        using var lockHandle = dryRun || _lockFolder is null ? null : JobLockFile.Acquire(_lockFolder, job.Name);

        // Snapshot VSS: se richiesto e disponibile, la copia legge dallo snapshot invece
        // che dalla sorgente viva. Se non disponibile → copia normale con avviso (mai bloccare).
        VssSession? vss = null;
        string? sourceOverride = null;
        if (job.UseVss && !dryRun && _vssSessionRoot is not null)
        {
            try
            {
                var ledger = new VssLedger(Path.Combine(_vssSessionRoot, "vss-ledger.json"));
                vss = await VssSession.OpenAsync(job.Source, _vssSessionRoot, ledger, ct).ConfigureAwait(false);
                sourceOverride = vss.SnapshotSourcePath;
                progress?.Report(string.Format(CoreLoc.S("Vss_Created"),
                    VssPathMapper.GetVolumeRoot(job.Source)));
            }
            catch (VssUnavailableException ex)
            {
                progress?.Report(string.Format(CoreLoc.S("Vss_Unavailable"), ex.Message));
            }
        }

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

            RobocopyRunResult run;
            if (job.Versioned && !dryRun && _snapshots is not null)
            {
                if (HardLinkSupport.IsSupported(job.Destination))
                {
                    run = await _snapshots.RunVersionedAsync(job, progress, ct, sourceOverride).ConfigureAwait(false);
                }
                else
                {
                    progress?.Report("[versioning] ATTENZIONE: la destinazione non supporta gli hard-link. "
                        + "Eseguo un mirror semplice (nessuno snapshot). Usa una destinazione NTFS locale per le versioni.");
                    run = await _runner.RunAsync(job, dryRun, progress, ct, sourceOverride: sourceOverride).ConfigureAwait(false);
                }
            }
            else
            {
                run = await _runner.RunAsync(job, dryRun, progress, ct, sourceOverride: sourceOverride).ConfigureAwait(false);
            }

            // Riepilogo nostro, leggibile e in italiano (l'output nativo di robocopy ha le
            // intestazioni localizzate che sbordano dalle colonne).
            var recap = BuildRecap(run.Result, dryRun);
            foreach (var line in recap)
                progress?.Report(line);

            var logContent = run.Output + Environment.NewLine + string.Join(Environment.NewLine, recap);
            run.Result.LogPath = _log.WriteAndArchive(job.Name, logContent, run.Result.StartedAt);

            // Persisti l'ultimo esito (solo esecuzioni reali, non le anteprime), così la GUI
            // può mostrarlo anche dopo un backup eseguito dall'attività pianificata.
            if (!dryRun)
            {
                _results?.Update(new JobLastResult
                {
                    JobName = job.Name,
                    Success = run.Result.Success,
                    ExitCode = run.Result.ExitCode,
                    FilesCopied = run.Result.FilesCopied,
                    FilesSkipped = run.Result.FilesSkipped,
                    FilesExtra = run.Result.FilesExtra,
                    FilesFailed = run.Result.FilesFailed,
                    DirsFailed = run.Result.DirsFailed,
                    FinishedAt = DateTime.Now,
                });
            }

            try
            {
                await _email.SendResultAsync(_config.Settings.Email, run.Result, run.Result.LogPath, ct)
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
            if (vss is not null)
            {
                await vss.DisposeAsync().ConfigureAwait(false);
                progress?.Report(CoreLoc.S("Vss_Released"));
            }

            if (connected && cred is not null)
                _credentials.Disconnect(cred.Host);
        }
    }

    private static string[] BuildRecap(JobResult r, bool dryRun)
    {
        var title = dryRun ? CoreLoc.S("Recap_TitlePreview") : CoreLoc.S("Recap_Title");
        var ok = r.Success ? "OK" : CoreLoc.S("Lbl_Error");
        var extraNote = dryRun ? CoreLoc.S("Recap_ExtraDry") : CoreLoc.S("Recap_ExtraReal");

        static string Line(string label, string value) => $"{label,-18}: {value}";
        // I conteggi "extra" portano la nota esplicativa solo quando ce ne sono.
        string Extra(long n) => n > 0 ? $"{n}{extraNote}" : n.ToString();

        return new[]
        {
            "",
            "====== " + title + " ======",
            Line(CoreLoc.S("Lbl_Result"), $"{ok} - {r.Status}"),
            // Prima le cartelle (copiate, fallite, extra), poi i file (copiati, invariati, extra, falliti).
            Line(CoreLoc.S("Lbl_FoldersCopied"), r.DirsCopied.ToString()),
            Line(CoreLoc.S("Lbl_DirsFailed"), r.DirsFailed.ToString()),
            Line(CoreLoc.S("Lbl_FoldersExtra"), Extra(r.DirsExtra)),
            Line(CoreLoc.S("Lbl_FilesCopied"), r.FilesCopied.ToString()),
            Line(CoreLoc.S("Lbl_FilesUnchanged"), r.FilesSkipped.ToString()),
            Line(CoreLoc.S("Lbl_FilesExtra"), Extra(r.FilesExtra)),
            Line(CoreLoc.S("Lbl_FilesFailed"), r.FilesFailed.ToString()),
            Line(CoreLoc.S("Lbl_Duration"), r.Duration.ToString(@"hh\:mm\:ss")),
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
