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

    private static readonly Encoding OemEncoding = ResolveOemEncoding();

    /// <param name="detectMedia">Rilevatore del tipo di disco (iniettabile nei test); default <see cref="StorageProbe.Detect"/>.</param>
    public RobocopyRunner(string? robocopyPath = null, ForceCopyPlanner? forceCopyPlanner = null,
        Func<string?, DiskMedia>? detectMedia = null)
    {
        _robocopyPath = robocopyPath ?? Path.Combine(Environment.SystemDirectory, "Robocopy.exe");
        _forceCopyPlanner = forceCopyPlanner;
        _detectMedia = detectMedia ?? StorageProbe.Detect;
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
            var help = p.StandardOutput.ReadToEnd();
            p.WaitForExit(10_000);
            return help.Contains("/IM ", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    });

    /// <param name="beforeForceCopyPass">Invocato (mai in anteprima) subito prima della passata
    /// "forza copia", con i filtri che robocopy ricevera'. Serve al versioning: la passata
    /// sovrascrive SUL POSTO, e su un file ancora hard-linkato agli snapshot precedenti ne
    /// riscriverebbe la storia; chi versiona deve prima scollegarli.</param>
    public async Task<RobocopyRunResult> RunAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default,
        string? destinationOverride = null, string? sourceOverride = null,
        Func<IReadOnlyList<string>, Task>? beforeForceCopyPass = null)
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
        (int exit1, string text1, string? hardwareError) pass1;
        try
        {
            pass1 = await RunPassAsync(
                RobocopyArgsBuilder.Build(job, dryRun, destinationOverride: destinationOverride,
                    sourceOverride: sourceOverride, maxThreads: maxThreads), progress, ct)
                .ConfigureAwait(false);
        }
        catch (JobCancelledException ex)
        {
            // Annullato a meta': l'output parziale (con l'eventuale avviso sui thread) va nel log.
            throw new JobCancelledException(fullText + ex.PartialOutput, ct);
        }
        var (exit1, text1, hardwareError) = pass1;
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
                var (exit2, text2, hardwareError2) = await RunPassAsync(pass2Args, progress, ct).ConfigureAwait(false);
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

        var result = new JobResult
        {
            JobName = job.Name,
            ExitCode = exitCombined,
            Success = interpreted.Success,
            Status = hardwareError is null ? interpreted.Summary : CoreLoc.S("Hw_Status"),
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

        return new RobocopyRunResult { Result = result, Output = text };
    }

    /// <summary>Exit code registrato per un job interrotto da un errore hardware (bit "errore grave").</summary>
    public const int HardwareFailureExitCode = 16;

    /// <summary>Avvia un singolo processo robocopy e restituisce (exit code, output catturato,
    /// dettaglio dell'errore hardware che ha fatto interrompere la passata, o null).</summary>
    private async Task<(int ExitCode, string Output, string? HardwareError)> RunPassAsync(
        IReadOnlyList<string> args, IProgress<string>? progress, CancellationToken ct)
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
