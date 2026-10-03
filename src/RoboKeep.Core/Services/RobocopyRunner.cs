using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>Annullamento dell'utente durante robocopy: porta con se' l'output prodotto fino a
/// quel momento, cosi' chi orchestra puo' scrivere comunque il log di quel che e' stato fatto.
/// Deriva da <see cref="OperationCanceledException"/>: chi gia' gestisce l'annullamento non cambia.</summary>
public sealed class JobCancelledException : OperationCanceledException
{
    public string PartialOutput { get; }
    public JobCancelledException(string partialOutput, CancellationToken token)
        : base("Annullato dall'utente.", token) => PartialOutput = partialOutput;
}

/// <summary>Risultato grezzo di un'esecuzione robocopy: esito + output testuale completo.</summary>
public sealed class RobocopyRunResult
{
    public required JobResult Result { get; init; }
    public required string Output { get; init; }

    /// <summary>true se i nomi dei file in <see cref="Output"/> sono quelli veri, letti dal log
    /// Unicode di robocopy. false se l'output viene dallo stdout OEM, dove i caratteri che la code
    /// page non ha tornano come <c>?</c> o sostituiti dal piu' simile: chi ci decide sopra deve
    /// prima ritrovare ogni nome sul disco.</summary>
    public bool ExactNames { get; init; }
}

/// <summary>
/// Esegue robocopy come processo esterno, catturando l'output in tempo reale, e ne ricava un
/// <see cref="JobResult"/>. Se il job ha una lista "Forza copia", esegue una seconda passata
/// (<see cref="RobocopyArgsBuilder.BuildForceCopyPass"/>) e aggrega conteggi ed exit code.
/// </summary>
public sealed class RobocopyRunner
{
    private readonly string _robocopyPath;
    private readonly ForceCopyPlanner? _forceCopyPlanner;
    private readonly Func<string?, DiskMedia> _detectMedia;
    private readonly Func<string?> _createUnicodeLog;

    private static readonly Encoding OemEncoding = ResolveOemEncoding();

    /// <param name="detectMedia">Rilevatore del tipo di disco (iniettabile nei test); default <see cref="StorageProbe.Detect"/>.</param>
    /// <param name="createUnicodeLog">Prepara il file per <c>/UNILOG</c> e ne restituisce il percorso, o
    /// null se non si puo' (iniettabile nei test, per provare il ripiego sullo stdout).</param>
    public RobocopyRunner(string? robocopyPath = null, ForceCopyPlanner? forceCopyPlanner = null,
        Func<string?, DiskMedia>? detectMedia = null, Func<string?>? createUnicodeLog = null)
    {
        _robocopyPath = robocopyPath ?? Path.Combine(Environment.SystemDirectory, "Robocopy.exe");
        _forceCopyPlanner = forceCopyPlanner;
        _detectMedia = detectMedia ?? StorageProbe.Detect;
        _createUnicodeLog = createUnicodeLog ?? CreateUnicodeLog;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    private static Encoding ResolveOemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding((int)GetOEMCP());
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    // Supporto a /IM, per eseguibile: si chiede a robocopy stesso (il suo help la elenca o no),
    // una volta sola. Un'opzione sconosciuta lo farebbe uscire con errore grave senza copiare nulla.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> IncludeModifiedSupport = new();

    private bool SupportsIncludeModified() => IncludeModifiedSupport.GetOrAdd(_robocopyPath, path =>
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = OemEncoding,
            };
            psi.ArgumentList.Add("/?");
            using var p = Process.Start(psi);
            if (p is null) return false;
            // Il tempo massimo deve valere anche per la lettura: un ReadToEnd sincrono aspetterebbe
            // per sempre un robocopy che non risponde, prima ancora di arrivare al WaitForExit.
            var read = p.StandardOutput.ReadToEndAsync();
            if (!read.Wait(10_000))
            {
                try { p.Kill(); } catch { /* gia' uscito */ }
                return false;
            }
            return read.Result.Contains("/IM ", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    });

    /// <summary>Copia del job adatta a un'anteprima che deve CONTARE (guardia sulle cancellazioni,
    /// elenco dei cambiamenti delle versioni).
    /// <list type="bullet">
    /// <item>Senza multi-thread: con /MT robocopy conta come "copiata" ogni cartella anche quando
    /// non c'e' niente da fare, e i conteggi mentirebbero. Per un'enumerazione il parallelismo non
    /// serve.</item>
    /// <item>Senza passata "forza copia": gira senza /MIR (alla guardia non dice niente), i suoi
    /// conteggi gonfierebbero il totale, le sue righe duplicherebbero l'elenco, e in modalita' smart
    /// farebbe pure l'hash di file grandi per una stima.</item>
    /// </list></summary>
    public static BackupJob PreviewJobFor(BackupJob job)
    {
        var preview = job.Clone();
        preview.MultiThread = 0;
        preview.ForceCopyFiles = new();
        return preview;
    }

    /// <param name="beforeForceCopyPass">Invocato (mai in anteprima) subito prima della passata
    /// "forza copia", con i filtri che robocopy ricevera'. Serve al versioning: la passata
    /// sovrascrive SUL POSTO, e chi versiona deve prima mettere da parte la copia precedente di
    /// quei file, altrimenti la versione non la conterrebbe.</param>
    /// <param name="listDetails">Chiede a robocopy un elenco con percorso completo e byte
    /// (<c>/FP /BYTES</c>): serve solo all'anteprima dei job con versioni, che quelle
    /// righe deve leggerle (vedi <see cref="RobocopyListParser"/>).</param>
    public async Task<RobocopyRunResult> RunAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default,
        string? destinationOverride = null, string? sourceOverride = null,
        Func<IReadOnlyList<string>, Task>? beforeForceCopyPass = null, bool listDetails = false)
    {
        var started = DateTime.Now;

        // Tetto ai thread secondo il supporto fisico: su un disco meccanico piu' thread significano
        // solo salti continui della testina. Si interroga il job, non gli override: lo snapshot VSS
        // e la cartella .inprogress stanno sugli stessi dischi di sorgente e destinazione.
        var destMedia = _detectMedia(job.Destination);
        var maxThreads = StorageProbe.ThreadCap(_detectMedia(job.Source), destMedia);
        string? capNote = null;
        if (maxThreads is int cap && job.InterPacketGapMs <= 0 && job.MultiThread > cap)
        {
            var hdd = destMedia == DiskMedia.Hdd ? job.Destination : job.Source;
            capNote = string.Format(CoreLoc.S("Threads_Capped"),
                Path.GetPathRoot(hdd) ?? hdd, job.MultiThread, cap);
            progress?.Report(capNote);
        }

        // L'avviso sui thread va anche nel log salvato, non solo a video: chi rilegge il file deve
        // capire perche' robocopy e' partito con un /MT diverso da quello impostato nel job.
        var fullText = new StringBuilder();
        if (capNote is not null) fullText.AppendLine(capNote);

        // Passata principale (mirror/copia normale).
        (int exit1, string text1, string? hardwareError, bool exactNames) pass1;
        try
        {
            pass1 = await RunPassAsync(
                RobocopyArgsBuilder.Build(job, dryRun, destinationOverride: destinationOverride,
                    sourceOverride: sourceOverride, maxThreads: maxThreads, listDetails: listDetails),
                progress, ct, exactNames: listDetails)
                .ConfigureAwait(false);
        }
        catch (JobCancelledException ex)
        {
            // Annullato a meta': l'output parziale (con l'eventuale avviso sui thread) va nel log.
            throw new JobCancelledException(fullText + ex.PartialOutput, ct);
        }
        var (exit1, text1, hardwareError, exactNames) = pass1;
        fullText.Append(text1);
        var counts = RobocopyOutputParser.ParseCounts(text1.Split('\n'));
        var exitCombined = exit1;

        // Passata "Forza copia": solo se la lista è valorizzata e c'è un pianificatore — e mai
        // dopo un errore hardware: il disco va lasciato in pace.
        if (hardwareError is null && job.ForceCopyFiles is { Count: > 0 } && _forceCopyPlanner is not null)
        {
            var plan = await _forceCopyPlanner.PlanAsync(job, progress, ct).ConfigureAwait(false);
            if (plan.Filters.Count > 0)
            {
                if (!dryRun && beforeForceCopyPass is not null)
                    await beforeForceCopyPass(plan.Filters).ConfigureAwait(false);

                var pass2Args = RobocopyArgsBuilder.BuildForceCopyPass(
                    job, plan.Filters, dryRun, destinationOverride: destinationOverride,
                    sourceOverride: sourceOverride, maxThreads: maxThreads,
                    includeModified: SupportsIncludeModified());
                var (exit2, text2, hardwareError2, _) = await RunPassAsync(pass2Args, progress, ct).ConfigureAwait(false);
                hardwareError = hardwareError2;
                fullText.AppendLine().Append(text2);
                exitCombined |= exit2; // gli exit code robocopy sono bitfield: l'OR preserva l'esito peggiore

                var c2 = RobocopyOutputParser.ParseCounts(text2.Split('\n'));
                counts = new RobocopyCounts(
                    counts.DirsCopied + c2.DirsCopied,
                    counts.FilesCopied + c2.FilesCopied,
                    counts.FilesSkipped + c2.FilesSkipped,
                    counts.FilesFailed + c2.FilesFailed,
                    counts.FilesExtra + c2.FilesExtra,
                    counts.DirsFailed + c2.DirsFailed,
                    counts.DirsExtra + c2.DirsExtra);

                // Aggiorna gli hash salvati solo a passata forzata riuscita (così un errore = ricopia al prossimo run).
                if (!dryRun && plan.Smart && hardwareError2 is null && ExitCodeInterpreter.Interpret(exit2).Success)
                    _forceCopyPlanner.Commit(job.Name, plan.NewHashes);
            }
        }

        // Robocopy ucciso per errore hardware: il suo exit code non significa nulla. Si registra
        // l'errore grave (16), cosi' anche chi guarda solo il codice vede un fallimento.
        if (hardwareError is not null)
            exitCombined = HardwareFailureExitCode;

        var interpreted = ExitCodeInterpreter.Interpret(exitCombined);
        var text = fullText.ToString();

        // Disco pieno: robocopy lo dice con Win32 112 in mezzo a migliaia di righe, e l'exit code
        // non lo distingue da qualunque altro errore di copia. Lo si riconosce qui, una volta sola,
        // e solo su un run FALLITO: il rimedio non e' riprovare, e' fare posto. Il dettaglio (quante
        // versioni, quanto occupano) lo compone BackupRunner, che conosce il job.
        var diskFull = hardwareError is null && !interpreted.Success && DiskFullDetector.Matches(text);

        var result = new JobResult
        {
            JobName = job.Name,
            ExitCode = exitCombined,
            Success = interpreted.Success,
            Status = hardwareError is not null ? CoreLoc.S("Hw_Status")
                : diskFull ? CoreLoc.S("Space_Status")
                : interpreted.Summary,
            DiskFull = diskFull,
            HardwareError = hardwareError is not null,
            HardwareErrorDetail = hardwareError,
            ThreadCapNote = capNote,
            StartedAt = started,
            Duration = DateTime.Now - started,
            DryRun = dryRun,
            DirsCopied = counts.DirsCopied,
            FilesCopied = counts.FilesCopied,
            FilesSkipped = counts.FilesSkipped,
            FilesExtra = counts.FilesExtra,
            FilesFailed = counts.FilesFailed,
            DirsFailed = counts.DirsFailed,
            DirsExtra = counts.DirsExtra,
        };

        return new RobocopyRunResult { Result = result, Output = text, ExactNames = exactNames };
    }

    /// <summary>Exit code registrato per un job interrotto da un errore hardware (bit "errore grave").</summary>
    public const int HardwareFailureExitCode = 16;

    /// <summary>Avvia un singolo processo robocopy e restituisce (exit code, output catturato,
    /// dettaglio dell'errore hardware che ha fatto interrompere la passata, o null).</summary>
    /// <param name="exactNames">L'output deve riportare i nomi dei file esatti, perche' qualcuno
    /// ci decidera' sopra (l'anteprima dei job con versioni). Lo stdout di robocopy passa per la
    /// code page OEM: quello che non ci sta torna come <c>?</c> oppure sostituito dal carattere piu'
    /// simile (’ diventa ', – diventa -, Ł diventa L), e un nome cosi' non esiste sul disco. Il log
    /// <c>/UNILOG</c> invece e' UTF-16 e ha i nomi veri: con <c>/TEE</c> lo stdout continua ad
    /// arrivare come sempre (avanzamento, errori hardware), e a fine passata l'output si legge dal
    /// file. Verificato su questa macchina: <c>/UNICODE</c> sullo stdout e' rotto (scrive l'intestazione
    /// UTF-16 e poi prosegue in OEM) e una console in UTF-8 (<c>chcp 65001</c>) non cambia niente.</param>
    /// <returns>L'ultimo elemento dice se l'output viene davvero dal log Unicode (nomi esatti).</returns>
    private async Task<(int ExitCode, string Output, string? HardwareError, bool ExactNames)> RunPassAsync(
        IReadOnlyList<string> args, IProgress<string>? progress, CancellationToken ct, bool exactNames = false)
    {
        var unicodeLog = exactNames ? _createUnicodeLog() : null;
        try
        {
            var (exitCode, stdout, hardwareError) = await RunProcessAsync(args, unicodeLog, progress, ct)
                .ConfigureAwait(false);
            // Il file vale solo se robocopy e' arrivato in fondo da solo: ucciso per un errore
            // hardware potrebbe mancare delle ultime righe, e quella dell'errore deve restare nel log.
            // Vuoto vuol dire che robocopy non ci ha scritto (anche l'intestazione c'e' sempre):
            // allora vale lo stdout, e chi chiama sa che i nomi vanno ritrovati sul disco.
            var unicode = unicodeLog is not null && hardwareError is null ? ReadUnicodeLog(unicodeLog) : null;
            return string.IsNullOrEmpty(unicode)
                ? (exitCode, stdout, hardwareError, false)
                : (exitCode, unicode, hardwareError, true);
        }
        finally
        {
            if (unicodeLog is not null)
            {
                try { File.Delete(unicodeLog); } catch { /* resta un file vuoto o quasi nella cartella temporanea */ }
            }
        }
    }

    /// <summary>Prepara il file per <c>/UNILOG</c>, o null se non si puo'. Va creato QUI, prima di
    /// robocopy: un log che robocopy non riesce ad aprire lo fa uscire con errore grave (16) senza
    /// fare nulla. Senza file si legge lo stdout come sempre, e i nomi con <c>?</c> li ritrova
    /// comunque <see cref="ListedPathResolver"/>.</summary>
    private static string? CreateUnicodeLog()
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"robokeep-list-{Guid.NewGuid():N}.txt");
            File.WriteAllBytes(path, Array.Empty<byte>());
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadUnicodeLog(string path)
    {
        try { return File.ReadAllText(path, Encoding.Unicode); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private async Task<(int ExitCode, string Output, string? HardwareError)> RunProcessAsync(
        IReadOnlyList<string> args, string? unicodeLog, IProgress<string>? progress, CancellationToken ct)
    {
        var output = new StringBuilder();
        string? hardwareError = null;

        var psi = new ProcessStartInfo
        {
            FileName = _robocopyPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = OemEncoding,
            StandardErrorEncoding = OemEncoding,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        if (unicodeLog is not null)
        {
            psi.ArgumentList.Add($"/UNILOG:{unicodeLog}");
            psi.ArgumentList.Add("/TEE");
        }

        using var process = new Process { StartInfo = psi };

        void OnData(string? line)
        {
            if (line is null) return;
            lock (output) output.AppendLine(line);
            progress?.Report(line);

            // Primo errore hardware (CRC, settore non trovato, errore del dispositivo): robocopy
            // riproverebbe (/R) e poi passerebbe al file dopo, per migliaia di file, su un disco
            // che sta cedendo. Lo si ferma subito, prima ancora del primo nuovo tentativo.
            if (DiskError.IsRobocopyHardwareErrorLine(line, out var detail)
                && Interlocked.CompareExchange(ref hardwareError, detail, null) is null)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { /* il processo potrebbe essere già terminato */ }
            }
        }

        process.OutputDataReceived += (_, e) => OnData(e.Data);
        process.ErrorDataReceived += (_, e) => OnData(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* il processo potrebbe essere già terminato */ }
        }))
        {
            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Il processo e' stato ucciso dalla registrazione qui sopra: si aspetta che esca
                // davvero (pochi ms), cosi' le ultime righe arrivano, e si consegna l'output parziale.
                try { await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch { /* se non esce in tempo, il log conterra' quel che c'e' */ }
                string partial;
                lock (output) partial = output.ToString();
                throw new JobCancelledException(partial, ct);
            }
        }

        return (process.ExitCode, output.ToString(), Volatile.Read(ref hardwareError));
    }
}
