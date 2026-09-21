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
    private readonly Func<string?, DiskEventSummary> _diskEvents;

    // Dischi (radici, es. "E:\") che in questa sessione hanno segnalato un errore hardware: i job
    // successivi che li toccano non partono. Un --run-all notturno con cinque job sullo stesso
    // disco non deve martellarlo cinque volte dopo il primo errore.
    private readonly HashSet<string> _faultedRoots = new(StringComparer.OrdinalIgnoreCase);

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
        RunHistoryStore? history = null,
        Func<string?, DiskEventSummary>? diskEvents = null)
    {
        _diskEvents = diskEvents ?? (path => DiskEventLog.Collect(path));
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
        // Rotazione dei dischi: due dischi alternati hanno spesso la stessa lettera. Se il
        // volume collegato non e' quello per cui il job e' stato configurato - o se non c'e'
        // nessun disco - si salta senza toccare NULLA (niente lock, niente UAC per VSS, niente
        // robocopy): un mirror sul disco sbagliato cancellerebbe i dati che ci trova.
        var currentVolume = VolumeIdentity.ForPath(job.Destination);
        var check = VolumeIdentity.CheckDestination(job.Destination, job.DestinationVolumeId);
        if (VolumeGuard.IsAway(check))
        {
            var unknown = CoreLoc.S("Volume_Unknown");
            var expectedLabel = string.IsNullOrEmpty(job.DestinationVolumeLabel) ? unknown : job.DestinationVolumeLabel;
            // Disco assente e disco sbagliato si decidono allo stesso modo, ma si raccontano
            // diversamente: senza disco non c'e' nessuna etichetta "trovata" da nominare.
            progress?.Report(check == VolumeCheck.DiskAbsent
                ? string.Format(CoreLoc.S("Volume_SkippedAbsent"), expectedLabel)
                : string.Format(CoreLoc.S("Volume_Skipped"), expectedLabel,
                    string.IsNullOrEmpty(currentVolume?.Label) ? unknown : currentVolume!.Label));

            var now = DateTime.Now;
            if (!dryRun)
                _history?.Append(RunHistoryEntry.ForSkipped(job.Name, now));
            // L'ultimo esito NON viene aggiornato: un salto non cancella la memoria di un
            // successo precedente.
            return new JobResult
            {
                JobName = job.Name,
                Skipped = true,
                Success = false,
                Status = CoreLoc.S("Volume_SkippedStatus"),
                StartedAt = now,
                DryRun = dryRun,
            };
        }

        // Disco che ha gia' segnalato un errore hardware in questa sessione: non lo si tocca piu'.
        // A differenza del disco assente, questo E' un fallimento (il backup non e' stato fatto
        // e c'e' un problema da risolvere): Success = false, Skipped = false.
        var faulted = RootOf(job.Destination) is { } dr && _faultedRoots.Contains(dr) ? dr
            : RootOf(job.Source) is { } sr && _faultedRoots.Contains(sr) ? sr
            : null;
        if (faulted is not null)
        {
            progress?.Report(string.Format(CoreLoc.S("Hw_SkippedAfterFault"), faulted));
            var now = DateTime.Now;
            if (!dryRun)
                _history?.Append(new RunHistoryEntry(job.Name, RunHistoryEntry.KindBackup, now, now,
                    false, RobocopyRunner.HardwareFailureExitCode, 0, 0, 0, 0, 0, null));
            return new JobResult
            {
                JobName = job.Name,
                Success = false,
                ExitCode = RobocopyRunner.HardwareFailureExitCode,
                HardwareError = true,
                Status = CoreLoc.S("Hw_Status"),
                StartedAt = now,
                DryRun = dryRun,
            };
        }

        // Il PC non deve sospendersi per inattivita' nel mezzo del lavoro: a un disco USB la
        // sospensione toglie l'alimentazione durante le scritture.
        using var awake = SleepBlocker.Acquire($"RoboKeep: {job.Name}");

        // Errori disco che Windows ha registrato di recente per sorgente e destinazione: e' il
        // preavviso che di solito precede il danno di settimane. Non blocca il job (gli eventi non
        // identificano il disco fisico con certezza), ma lo dice all'inizio E nel riepilogo: i
        // backup pianificati non passano dal pre-avvio della finestra, e questo e' il loro unico avviso.
        var healthNotes = new List<string>();
        foreach (var root in new[] { job.Destination, job.Source }.Select(RootOf).OfType<string>()
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var events = _diskEvents(root);
            if (events.Total > 0)
                healthNotes.Add(string.Format(CoreLoc.S("Health_Warning"), root, events.Describe()));
        }
        foreach (var note in healthNotes)
            progress?.Report(note);

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

            var startedAt = DateTime.Now;
            string? faultPath = null;
            RobocopyRunResult run;
            try
            {
                if (job.Versioned && !dryRun && _snapshots is not null)
                {
                    if (HardLinkSupport.IsSupported(job.Destination))
                    {
                        run = await _snapshots.RunVersionedAsync(job, progress, ct, sourceOverride).ConfigureAwait(false);
                    }
                    else
                    {
                        progress?.Report(CoreLoc.S("Versioning_NoHardLink"));
                        run = await _runner.RunAsync(job, dryRun, progress, ct, sourceOverride: sourceOverride).ConfigureAwait(false);
                    }
                }
                else
                {
                    run = await _runner.RunAsync(job, dryRun, progress, ct, sourceOverride: sourceOverride).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException && DiskError.IsUnreadable(ex))
            {
                // Errore hardware fuori da robocopy (clone hard-link, pulizia snapshot, hash della
                // forza-copia): stesso trattamento, il job diventa un fallimento con log ed email.
                faultPath = (ex as DiskHardwareException)?.FaultPath;
                run = new RobocopyRunResult
                {
                    Output = "",
                    Result = new JobResult
                    {
                        JobName = job.Name,
                        ExitCode = RobocopyRunner.HardwareFailureExitCode,
                        Success = false,
                        Status = CoreLoc.S("Hw_Status"),
                        HardwareError = true,
                        HardwareErrorDetail = ex.Message,
                        StartedAt = startedAt,
                        Duration = DateTime.Now - startedAt,
                        DryRun = dryRun,
                    },
                };
            }

            // Riepilogo nostro, leggibile e in italiano (l'output nativo di robocopy ha le
            // intestazioni localizzate che sbordano dalle colonne).
            var recap = BuildRecap(run.Result, dryRun).ToList();
            if (run.Result.ThreadCapNote is { } capNote)
                recap.Add(capNote);
            recap.AddRange(healthNotes);
            if (run.Result.HardwareError)
            {
                // Dalle righe di robocopy non si distingue il lato che ha ceduto (il percorso
                // riportato e' sempre quello sorgente): in mancanza di un percorso certo si mette
                // a riposo il disco di destinazione, quello su cui il job scrive.
                MarkFaulted(faultPath ?? job.Destination);
                recap.Add(string.Format(CoreLoc.S("Hw_Stop"), run.Result.HardwareErrorDetail));
                recap.Add(CoreLoc.S("Hw_Advice"));
            }

            // La verifica rilegge per intero sorgente e destinazione: si fa ogni VerifyEveryDays
            // giorni, non a ogni backup. Conta qualunque verifica COMPLETATA in cronologia, anche
            // manuale; una interrotta da un errore hardware no. Si decide qui, prima di chiudere
            // il log del backup, cosi' "non prevista oggi" resta scritto anche nel file.
            var verifyWanted = job.VerifyAfterRun && !dryRun && run.Result.Success;
            var lastVerify = verifyWanted && job.VerifyEveryDays > 0
                ? _history?.List(job.Name).FirstOrDefault(e => e.IsCompletedVerify)?.StartedAt
                : null;
            var verifyDue = VerifySchedule.IsDue(job.VerifyEveryDays, lastVerify, DateTime.Now);
            if (verifyWanted && !verifyDue)
                recap.Add(string.Format(CoreLoc.S("Verify_NotDue"), lastVerify!.Value.ToString("d"),
                    VerifySchedule.DaysUntilDue(job.VerifyEveryDays, lastVerify, DateTime.Now)));

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
                progress?.Report(string.Format(CoreLoc.S("Email_SendFailed"), ex.Message));
            }

            // Verifica integrità automatica: solo per run reali riusciti, mai bloccante. Ha un log
            // suo, collegato alla sua voce di cronologia: il log del backup qui e' gia' chiuso.
            if (verifyWanted && verifyDue)
            {
                var verifyStart = DateTime.Now;
                var verifyLog = new VerifyLogRecorder(progress);
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
                            sourceOverride ?? job.Source, target, job.ExcludeFiles, job.ExcludeDirs, verifyLog, ct)
                            .ConfigureAwait(false);
                        ReportVerify(vr, verifyLog);
                        _history?.Append(RunHistoryEntry.ForVerify(job.Name, verifyStart, vr,
                            verifyLog.Save(_log, job.Name, verifyStart, vr)));
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (DiskError.IsUnreadable(ex))
                {
                    // La verifica legge TUTTO il disco: al primo errore hardware si ferma, e il
                    // disco va a riposo anche per i job successivi di questa sessione.
                    MarkFaulted((ex as DiskHardwareException)?.FaultPath ?? job.Destination);
                    verifyLog.Report(string.Format(CoreLoc.S("Hw_VerifyStop"), ex.Message));
                    verifyLog.Report(CoreLoc.S("Hw_Advice"));
                    _history?.Append(RunHistoryEntry.ForVerifyInterrupted(job.Name, verifyStart,
                        verifyLog.Save(_log, job.Name, verifyStart, null)));
                }
                catch (Exception ex)
                {
                    progress?.Report(string.Format(CoreLoc.S("Verify_Failed"), ex.Message));
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

    /// <summary>Radice del volume locale di un percorso ("E:\"), o null per percorsi vuoti,
    /// di rete o non interpretabili: solo un disco locale puo' essere "messo a riposo".</summary>
    private static string? RootOf(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            return root;
        }
        catch { return null; }
    }

    private void MarkFaulted(string? path)
    {
        if (RootOf(path) is { } root) _faultedRoots.Add(root);
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
