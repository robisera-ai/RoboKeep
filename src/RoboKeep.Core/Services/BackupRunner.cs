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
    private readonly RunHistoryStore? _history;

    public BackupRunner(
        AppConfig config,
        RobocopyRunner runner,
        LogService log,
        EmailService email,
        CredentialService credentials,
        LastResultStore? results = null,
        SnapshotService? snapshots = null,
        string? lockFolder = null,
        string? vssSessionRoot = null,
        RunHistoryStore? history = null)
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
        _history = history;
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
            catch (OperationCanceledException)
            {
                throw; // annullamento dell'utente: ferma il job, non degradare
            }
            catch (Exception ex)
            {
                // Qualsiasi altro errore (VssUnavailableException, IO su disco pieno, ...):
                // il backup non si ferma mai per colpa di VSS, degrada a copia normale.
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

                _history?.Append(new RunHistoryEntry(
                    job.Name, "backup", run.Result.StartedAt, DateTime.Now,
                    run.Result.Success, run.Result.ExitCode,
                    run.Result.FilesCopied, run.Result.FilesSkipped, run.Result.FilesExtra,
                    run.Result.FilesFailed, run.Result.DirsFailed, run.Result.LogPath));
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

            // Verifica integrità automatica: solo per run reali riusciti, mai bloccante.
            if (job.VerifyAfterRun && !dryRun && run.Result.Success)
            {
                var verifyStart = DateTime.Now;
                try
                {
                    var target = VerifyTargetResolver.Resolve(job);
                    if (target is null)
                    {
                        progress?.Report(CoreLoc.S("Verify_NothingToVerify"));
                    }
                    else
                    {
                        var vr = await IntegrityVerifier.VerifyAsync(
                            sourceOverride ?? job.Source, target, job.ExcludeFiles, job.ExcludeDirs, progress, ct)
                            .ConfigureAwait(false);
                        ReportVerify(vr, progress);
                        _history?.Append(new RunHistoryEntry(
                            job.Name, "verify", verifyStart, DateTime.Now,
                            vr.Mismatched == 0, 0,
                            vr.Checked, vr.Skipped, 0, vr.Mismatched + vr.Missing, 0, null));
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    progress?.Report($"[verifica] non riuscita: {ex.Message}");
                }
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

    /// <summary>Righe di riepilogo della verifica nel log (riusato dalla verifica manuale).</summary>
    public static void ReportVerify(VerifyResult vr, IProgress<string>? progress)
    {
        if (vr.Mismatched > 0)
            progress?.Report(string.Format(CoreLoc.S("Verify_RecapBad"), vr.Mismatched,
                string.Join(", ", vr.MismatchedPaths.Take(5))));
        // Identici = hashati meno i differenti e meno quelli cambiati dopo il backup.
        progress?.Report(string.Format(CoreLoc.S("Verify_RecapOk"),
            vr.Checked - vr.Mismatched - vr.ChangedSinceBackup, vr.ChangedSinceBackup, vr.Skipped, vr.Missing));
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
