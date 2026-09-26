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
    private readonly FaultedDiskStore? _faultedDisks;
    private readonly Func<string?, string?> _configMirrorTarget;

    // Dischi (radici, es. "E:\") che in questa sessione hanno segnalato un errore hardware: i job
    // successivi che li toccano non partono. Un --run-all notturno con cinque job sullo stesso
    // disco non deve martellarlo cinque volte dopo il primo errore. Il set in memoria copre la
    // sessione; _faultedDisks lo fa durare anche nei processi di domani (attivita' pianificata).
    private readonly HashSet<string> _faultedRoots = new(StringComparer.OrdinalIgnoreCase);

    // Dischi per cui l'email dell'episodio e' GIA' partita in questa sessione. Serve dove lo store
    // non arriva: un'unita' di rete mappata ha una radice ("Z:\") ma nessuna identita' di volume,
    // quindi niente voce su cui segnare "avvisato". Senza questo, un --run-all manderebbe
    // un'email per ogni job saltato invece di una per episodio.
    private readonly HashSet<string> _notifiedRoots = new(StringComparer.OrdinalIgnoreCase);

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
        Func<string?, DiskEventSummary>? diskEvents = null,
        FaultedDiskStore? faultedDisks = null,
        Func<string?, string?>? configMirrorTarget = null)
    {
        _diskEvents = diskEvents ?? (path => DiskEventLog.Collect(path));
        // Dove va la copia della configurazione: normalmente la radice del volume di destinazione.
        // E' un punto di innesto perche' nei test la destinazione e' una cartella temporanea, e la
        // sua radice e' il disco di sistema della macchina che esegue le prove: una prova non deve
        // scrivere in C:\. I test lo reindirizzano in una cartella loro.
        _configMirrorTarget = configMirrorTarget ?? ConfigMirror.TargetFolder;
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
        _faultedDisks = faultedDisks;
    }

    /// <summary>Trova un job per nome (case-insensitive).</summary>
    public BackupJob? FindJob(string name) =>
        _config.Jobs.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Esegue un singolo job, gestendo credenziali, log ed email.</summary>
    /// <param name="confirmDeletions">Chiesto solo quando un mirror sta per cancellare piu' della
    /// soglia del job: true = procedi comunque (una sola conferma, per questo run), false = fermati.
    /// null (riga di comando, attivita' pianificata) equivale a fermarsi: nessuno e' davanti allo
    /// schermo per decidere.</param>
    public async Task<JobResult> RunJobAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default,
        Func<MirrorDeleteEstimate, Task<bool>>? confirmDeletions = null)
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

        // Disco a riposo dopo un errore hardware: non lo si tocca piu'. Puo' esserlo in questa
        // sessione (set in memoria) o da prima (store, per identita' di volume: anche l'attivita'
        // pianificata di domani, che gira in un altro processo, lo rispetta). A differenza del
        // disco assente, questo E' un fallimento (il backup non e' stato fatto e c'e' un problema
        // da risolvere): Success = false, Skipped = false.
        var now0 = DateTime.Now;
        string? faulted = null;
        var sessionFault = false;
        FaultedDisk? persisted = null;
        foreach (var root in RootsOf(job))
        {
            sessionFault = _faultedRoots.Contains(root);
            // Lo store si consulta anche quando il set in memoria ha gia' deciso: e' lui a sapere
            // se l'utente e' stato avvisato, e senza quell'informazione un --run-all notturno
            // manderebbe un'email per ogni job saltato invece di una per episodio.
            persisted = _faultedDisks?.Find(VolumeIdentity.ForPath(root)?.VolumeId, now0);
            if (sessionFault || persisted is not null) { faulted = root; break; }
        }
        if (faulted is not null)
        {
            // Chi e' stato fermato in questa stessa sessione se lo sente raccontare cosi'; a chi
            // trova il disco gia' a riposo serve invece sapere da quando e come riabilitarlo.
            var skipNote = sessionFault
                ? string.Format(CoreLoc.S("Hw_SkippedAfterFault"), faulted)
                : string.Format(CoreLoc.S("Hw_SkippedPersisted"), faulted,
                    persisted!.Since.ToString("d"), FaultedDiskStore.ExpiryDays);
            progress?.Report(skipNote);
            var skippedResult = new JobResult
            {
                JobName = job.Name,
                Success = false,
                ExitCode = RobocopyRunner.HardwareFailureExitCode,
                HardwareError = true,
                NotStarted = true,
                HardwareErrorDetail = skipNote,
                // Non "INTERROTTO": qui non c'e' stato niente da interrompere.
                Status = CoreLoc.S("Hw_NotStartedStatus"),
                StartedAt = now0,
                DryRun = dryRun,
            };
            if (!dryRun)
            {
                _history?.Append(new RunHistoryEntry(job.Name, RunHistoryEntry.KindBackup, now0, now0,
                    false, RobocopyRunner.HardwareFailureExitCode, 0, 0, 0, 0, 0, null));
                _results?.Update(new JobLastResult
                {
                    JobName = job.Name,
                    Success = false,
                    ExitCode = RobocopyRunner.HardwareFailureExitCode,
                    HardwareError = true,
                    HardwareErrorDetail = skipNote,
                    FinishedAt = now0,
                });

                // Una email per episodio, non una per job per notte: se l'utente e' gia' stato
                // avvisato per questo disco (l'email di errore del run che l'ha messo a riposo, o
                // il primo job saltato) gli altri job saltati restano solo nel log.
                if ((persisted is null || !persisted.Notified) && !_notifiedRoots.Contains(faulted))
                {
                    try
                    {
                        var sent = await _email.SendResultAsync(_config.Settings.Email, skippedResult, null, ct)
                            .ConfigureAwait(false);
                        if (sent)
                        {
                            _notifiedRoots.Add(faulted);
                            if (persisted is not null) _faultedDisks?.MarkNotified(persisted.VolumeId);
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report(string.Format(CoreLoc.S("Email_SendFailed"), ex.Message));
                    }
                }
            }
            return skippedResult;
        }

        // Il PC non deve sospendersi per inattivita' nel mezzo del lavoro: a un disco USB la
        // sospensione toglie l'alimentazione durante le scritture.
        using var awake = SleepBlocker.Acquire($"RoboKeep: {job.Name}");

        // Errori disco che Windows ha registrato di recente per sorgente e destinazione: e' il
        // preavviso che di solito precede il danno di settimane. Non blocca il job (gli eventi non
        // identificano il disco fisico con certezza), ma lo dice all'inizio E nel riepilogo: i
        // backup pianificati non passano dal pre-avvio della finestra, e questo e' il loro unico avviso.
        // L'email invece la merita solo un avviso NUOVO: gli eventi restano nel registro per giorni,
        // e ripetere ogni notte la stessa mail per lo stesso errore la fa diventare rumore da
        // ignorare. Nel log e nel riepilogo ci sono sempre tutti.
        var previousFinish = _results?.Load().GetValueOrDefault(job.Name)?.FinishedAt;
        var healthNotes = new List<string>();
        var newHealthNotes = new List<string>();
        foreach (var root in RootsOf(job))
        {
            var events = _diskEvents(root);
            if (events.Total == 0) continue;
            var note = string.Format(CoreLoc.S("Health_Warning"), root, events.Describe());
            healthNotes.Add(note);
            if (previousFinish is null || (events.Latest is DateTime latest && latest > previousFinish))
                newHealthNotes.Add(note);
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
            // Chi fa il lavoro si decide una volta sola: HardLinkSupport.IsSupported scrive un file
            // di prova nella destinazione, e interrogarlo due volte sarebbe IO buttato.
            var versioningWanted = job.Versioned && !dryRun && _snapshots is not null;
            var versionedRun = versioningWanted && HardLinkSupport.IsSupported(job.Destination);
            try
            {
                // Guardia sulle cancellazioni: solo per i mirror che girano "piatti". Quelli con
                // versioni la applicano dentro SnapshotService, sui conteggi dell'anteprima che fa
                // gia' contro l'ultimo snapshot (nessuna seconda enumerazione). Sta dentro questo
                // try perche' un annullamento durante l'anteprima e' un annullamento del job, con
                // il suo log e la sua voce di cronologia.
                if (job.Mirror && !dryRun && !versionedRun
                    && await CheckDeletionsAsync(job, startedAt, progress, confirmDeletions, sourceOverride, ct)
                        .ConfigureAwait(false) is { } stopped)
                {
                    return await FinishBlockedAsync(job, stopped.Result, stopped.Output, newHealthNotes, progress, ct)
                        .ConfigureAwait(false);
                }

                if (versioningWanted)
                {
                    if (versionedRun)
                    {
                        run = await _snapshots!.RunVersionedAsync(job, progress, ct, sourceOverride, confirmDeletions)
                            .ConfigureAwait(false);
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
            catch (OperationCanceledException ex)
            {
                // Annullato dall'utente: il job si ferma, ma quello che ha fatto fino a li' non
                // deve sparire. Si scrive comunque il log (output parziale + riga di chiusura) e
                // una voce di cronologia "annullato" che lo apre. Poi si rilancia: per chi chiama
                // resta un annullamento, non un esito.
                if (!dryRun)
                {
                    var partial = (ex as JobCancelledException)?.PartialOutput ?? "";
                    var closing = string.Format(CoreLoc.S("Run_CancelledRecap"), DateTime.Now);
                    progress?.Report(closing);
                    string? logPath = null;
                    try { logPath = _log.WriteAndArchive(job.Name, partial + Environment.NewLine + closing, startedAt); }
                    catch { /* un log mancato non deve mascherare l'annullamento */ }
                    _history?.Append(RunHistoryEntry.ForCancelled(job.Name, startedAt, logPath));
                }
                throw;
            }
            catch (Exception ex) when (DiskError.IsUnreadable(ex))
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

            // Mirror fermato dalla guardia dentro il versioning (l'anteprima contro l'ultimo
            // snapshot diceva troppe cancellazioni): da qui in poi e' identico al caso senza
            // versioni, lo chiude la stessa strada.
            if (run.Result.DeletionsBlocked)
                return await FinishBlockedAsync(job, run.Result, run.Output, newHealthNotes, progress, ct)
                    .ConfigureAwait(false);

            run.Result.HealthWarnings.AddRange(newHealthNotes);

            // Riepilogo nostro, leggibile e in italiano (l'output nativo di robocopy ha le
            // intestazioni localizzate che sbordano dalle colonne).
            var recap = BuildRecap(run.Result, dryRun).ToList();
            if (run.Result.ThreadCapNote is { } capNote)
                recap.Add(capNote);
            recap.AddRange(healthNotes);
            // In anteprima non si blocca niente — non c'e' nulla da fermare, l'anteprima non
            // cancella — ma se il mirror andrebbe oltre la soglia il riepilogo lo dice: e' proprio
            // la domanda a cui l'anteprima serve a rispondere. Solo per i mirror SENZA versioni:
            // l'anteprima di un job versionato gira contro la radice della destinazione, che
            // contiene le cartelle-data di tutti gli snapshot, e le conterebbe tutte come "extra"
            // (un 100 % inventato). Il run vero confronta invece con l'ultimo snapshot.
            if (dryRun && job.Mirror && !job.Versioned)
            {
                var previewEstimate = MirrorDeleteGuard.Estimate(job, run.Result);
                if (MirrorDeleteGuard.ShouldBlock(previewEstimate))
                    recap.Add(string.Format(CoreLoc.S("Guard_DryRunNote"), previewEstimate.Extra,
                        previewEstimate.Total, previewEstimate.Percent, previewEstimate.LimitPercent));
            }
            // Disco messo a riposo da questo run: radice e identita' di volume servono piu' sotto,
            // quando si sapra' se l'email dell'episodio e' partita davvero.
            string? markedRoot = null, markedVolumeId = null;
            if (run.Result.HardwareError)
            {
                // Dalle righe di robocopy non si distingue il lato che ha ceduto (il percorso
                // riportato e' sempre quello sorgente): in mancanza di un percorso certo si mette
                // a riposo il disco di destinazione, quello su cui il job scrive.
                var faultTarget = faultPath ?? job.Destination;
                markedRoot = RootOf(faultTarget);
                markedVolumeId = MarkFaulted(faultTarget, run.Result.HardwareErrorDetail ?? "");
                recap.Add(string.Format(CoreLoc.S("Hw_Stop"), run.Result.HardwareErrorDetail));
                recap.Add(CoreLoc.S("Hw_Advice"));
            }

            // Copia della configurazione nella radice del disco di backup: un disco con i file ma
            // senza i job costringerebbe a rifare tutto a memoria. Solo dopo un run VERO e RIUSCITO
            // (un'anteprima non ha scritto niente; su un disco che ha appena dato errori non si
            // insiste; i job saltati e quelli fermati dalla guardia sono gia' tornati al chiamante
            // molto prima di qui). La riga finisce nel riepilogo, quindi anche nel log del job.
            if (run.Result.Success && !dryRun && _config.Settings.ConfigCopyToDestination
                && _configMirrorTarget(job.Destination) is { } configFolder)
            {
                ConfigMirror.WriteTo(_config, configFolder, new ListProgress(recap),
                    Environment.MachineName, DateTime.Now);
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
                    HardwareError = run.Result.HardwareError,
                    HardwareErrorDetail = run.Result.HardwareErrorDetail,
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
                var sent = await _email.SendResultAsync(_config.Settings.Email, run.Result, run.Result.LogPath, ct)
                    .ConfigureAwait(false);
                // Solo ORA l'episodio risulta raccontato: i job che seguono sullo stesso disco
                // restano nel log senza una seconda email. Se l'email era spenta o non e' partita,
                // "avvisato" non si segna: a dirlo sara' il primo job saltato che riuscira' a farlo.
                if (sent && run.Result.HardwareError)
                {
                    if (markedRoot is not null) _notifiedRoots.Add(markedRoot);
                    if (markedVolumeId is not null) _faultedDisks?.MarkNotified(markedVolumeId);
                }
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
                    MarkFaulted((ex as DiskHardwareException)?.FaultPath ?? job.Destination, ex.Message);
                    var verifyDetail = string.Format(CoreLoc.S("Hw_VerifyStop"), ex.Message);
                    verifyLog.Report(verifyDetail);
                    verifyLog.Report(CoreLoc.S("Hw_Advice"));
                    _history?.Append(RunHistoryEntry.ForVerifyInterrupted(job.Name, verifyStart,
                        verifyLog.Save(_log, job.Name, verifyStart, null)));

                    // Il backup era riuscito, ma il disco ha ceduto rileggendolo: l'esito del job
                    // diventa errore hardware, cosi' la riga nella finestra principale lo mostra
                    // (icona e dettaglio) anche domani, e l'attivita' pianificata esce con errore.
                    // L'email di questo run e' gia' partita come "OK": la notizia sta qui e nel log.
                    run.Result.Success = false;
                    run.Result.Status = CoreLoc.S("Hw_Status");
                    run.Result.HardwareError = true;
                    run.Result.HardwareErrorDetail = verifyDetail;
                    _results?.Update(new JobLastResult
                    {
                        JobName = job.Name,
                        Success = false,
                        ExitCode = RobocopyRunner.HardwareFailureExitCode,
                        FilesCopied = run.Result.FilesCopied,
                        FilesSkipped = run.Result.FilesSkipped,
                        FilesExtra = run.Result.FilesExtra,
                        FilesFailed = run.Result.FilesFailed,
                        DirsFailed = run.Result.DirsFailed,
                        HardwareError = true,
                        HardwareErrorDetail = verifyDetail,
                        FinishedAt = DateTime.Now,
                    });
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

    /// <summary>Raccoglitore di righe: le fa finire nel riepilogo (e quindi nel log del job) invece
    /// che solo a video, come fa <c>progress</c> da solo.</summary>
    private sealed class ListProgress : IProgress<string>
    {
        private readonly List<string> _lines;
        public ListProgress(List<string> lines) => _lines = lines;
        public void Report(string value) => _lines.Add(value);
    }

    /// <summary>Anteprima delle cancellazioni di un mirror senza versioni: conta, senza toccare
    /// nulla, i file che il run vero rimuoverebbe dalla destinazione e li confronta con la soglia
    /// del job. Restituisce l'esito "fermato" da consegnare al chiamante, oppure null se il run
    /// puo' partire (soglia spenta, cancellazioni sotto soglia, o conferma ricevuta).</summary>
    private async Task<RobocopyRunResult?> CheckDeletionsAsync(
        BackupJob job, DateTime startedAt, IProgress<string>? progress,
        Func<MirrorDeleteEstimate, Task<bool>>? confirmDeletions, string? sourceOverride, CancellationToken ct)
    {
        // Soglia a zero: la guardia non bloccherebbe comunque, e l'enumerazione in piu' — decine di
        // secondi su cartelle enormi — sarebbe solo un costo regalato.
        if (job.MirrorDeleteLimitPercent <= 0) return null;

        progress?.Report(CoreLoc.S("Guard_Preview"));
        // Anteprima SENZA multi-thread: con /MT robocopy conta come "copiata" ogni cartella anche
        // quando non c'e' niente da fare, e i conteggi mentirebbero (lo stesso motivo del
        // versioning). Progresso nullo: le migliaia di righe dell'anteprima non sono il log del job.
        var previewJob = job.Clone();
        previewJob.MultiThread = 0;
        // Niente passata "forza copia" nell'anteprima: non cancella nulla (gira senza /MIR), quindi
        // alla guardia non dice niente, ma i suoi conteggi si sommerebbero a quelli della prima
        // passata gonfiando il totale — e in modalita' smart farebbe pure l'hash di file grandi per
        // una stima. Una sola enumerazione, quella che conta.
        previewJob.ForceCopyFiles = new();
        var preview = await _runner.RunAsync(previewJob, dryRun: true, progress: null, ct,
            sourceOverride: sourceOverride).ConfigureAwait(false);

        // Anteprima non riuscita (errore hardware, sorgente assente o illeggibile): non e' la
        // guardia a doverne decidere. Il run vero si fermera' da solo con il suo racconto — e con
        // la sorgente assente robocopy non cancella nulla, e' cosi' da sempre. Lo si dice comunque
        // nel log: il mirror che segue parte senza rete di sicurezza, e chi rilegge deve saperlo.
        if (!preview.Result.Success || preview.Result.HardwareError)
        {
            progress?.Report(CoreLoc.S("Guard_PreviewFailed"));
            return null;
        }

        var estimate = MirrorDeleteGuard.Estimate(job, preview.Result);
        if (!MirrorDeleteGuard.ShouldBlock(estimate)) return null;

        // Una sola domanda, valida per questo run: chi ha una finestra davanti decide, chi non c'e'
        // (callback null) si ferma. Il "sì" non viene ricordato: domani la stima e' un'altra.
        if (confirmDeletions is not null && await confirmDeletions(estimate).ConfigureAwait(false))
            return null;

        // L'output dell'anteprima e' l'elenco dei file che sarebbero spariti: nel log del job
        // vale piu' di qualunque spiegazione.
        return new RobocopyRunResult
        {
            Result = MirrorDeleteGuard.BlockedResult(estimate, startedAt),
            Output = preview.Output,
        };
    }

    /// <summary>Chiude un run fermato dalla guardia: lo racconta a video, scrive il log (cosi' la
    /// cronologia puo' aprirlo, come per un annullamento), registra il fallimento nell'ultimo esito
    /// e avvisa per email — chi ha lanciato il backup di notte non e' davanti allo schermo.</summary>
    private async Task<JobResult> FinishBlockedAsync(
        BackupJob job, JobResult result, string output, IReadOnlyList<string> newHealthNotes,
        IProgress<string>? progress, CancellationToken ct)
    {
        var detail = result.DeletionsBlockedDetail ?? "";
        progress?.Report(detail);
        result.Duration = DateTime.Now - result.StartedAt;
        // Gli avvisi di salute del disco non si perdono per strada: il backup non e' stato fatto E
        // il disco dava segnali: due notizie, non una, e l'email deve portarle entrambe.
        result.HealthWarnings.AddRange(newHealthNotes);

        try
        {
            result.LogPath = _log.WriteAndArchive(job.Name,
                output + Environment.NewLine + detail, result.StartedAt);
        }
        catch { /* un log mancato non deve mascherare il blocco */ }

        _history?.Append(new RunHistoryEntry(job.Name, RunHistoryEntry.KindBackup, result.StartedAt,
            DateTime.Now, false, result.ExitCode, 0, 0, result.FilesExtra, 0, 0, result.LogPath));
        _results?.Update(new JobLastResult
        {
            JobName = job.Name,
            Success = false,
            ExitCode = result.ExitCode,
            FilesExtra = result.FilesExtra,
            DeletionsBlocked = true,
            DeletionsBlockedDetail = detail,
            FinishedAt = DateTime.Now,
        });

        try
        {
            await _email.SendResultAsync(_config.Settings.Email, result, result.LogPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            progress?.Report(string.Format(CoreLoc.S("Email_SendFailed"), ex.Message));
        }
        return result;
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

    /// <summary>Le radici locali toccate da un job, destinazione prima: e' quella che il job
    /// scrive, ed e' la piu' probabile responsabile di un errore.</summary>
    private static IEnumerable<string> RootsOf(BackupJob job) =>
        new[] { job.Destination, job.Source }.Select(RootOf).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mette a riposo il disco di un percorso: per questa sessione (set in memoria) e sul
    /// disco (store), cosi' che anche un altro processo lo rispetti. La voce nasce NON notificata:
    /// l'email dell'episodio non e' ancora partita (puo' essere spenta o fallire), e dichiararlo in
    /// anticipo zittirebbe l'unico avviso che l'utente riceverebbe.
    /// <para>Restituisce l'identita' del volume messo a riposo, o null se il volume non e'
    /// identificabile (unita' di rete mappata, disco gia' scomparso): in quel caso esiste solo il
    /// riposo di sessione, niente voce nello store da marcare come notificata.</para></summary>
    private string? MarkFaulted(string? path, string detail)
    {
        if (RootOf(path) is not { } root) return null;
        _faultedRoots.Add(root);
        if (VolumeIdentity.ForPath(root) is not { } vol) return null;
        _faultedDisks?.Mark(new FaultedDisk(vol.VolumeId, vol.Label, root, DateTime.Now, detail));
        return vol.VolumeId;
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
