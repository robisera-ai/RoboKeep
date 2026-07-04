using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

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

    private static readonly Encoding OemEncoding = ResolveOemEncoding();

    public RobocopyRunner(string? robocopyPath = null, ForceCopyPlanner? forceCopyPlanner = null)
    {
        _robocopyPath = robocopyPath ?? Path.Combine(Environment.SystemDirectory, "Robocopy.exe");
        _forceCopyPlanner = forceCopyPlanner;
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

    public async Task<RobocopyRunResult> RunAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default,
        string? destinationOverride = null, string? sourceOverride = null)
    {
        var started = DateTime.Now;

        // Passata principale (mirror/copia normale): comportamento invariato.
        var (exit1, text1) = await RunPassAsync(
            RobocopyArgsBuilder.Build(job, dryRun, destinationOverride: destinationOverride,
                sourceOverride: sourceOverride), progress, ct)
            .ConfigureAwait(false);
        var fullText = new StringBuilder(text1);
        var counts = RobocopyOutputParser.ParseCounts(text1.Split('\n'));
        var exitCombined = exit1;

        // Passata "Forza copia": solo se la lista è valorizzata e c'è un pianificatore.
        if (job.ForceCopyFiles is { Count: > 0 } && _forceCopyPlanner is not null)
        {
            var plan = await _forceCopyPlanner.PlanAsync(job, progress, ct).ConfigureAwait(false);
            if (plan.Filters.Count > 0)
            {
                var pass2Args = RobocopyArgsBuilder.BuildForceCopyPass(
                    job, plan.Filters, dryRun, destinationOverride: destinationOverride,
                    sourceOverride: sourceOverride);
                var (exit2, text2) = await RunPassAsync(pass2Args, progress, ct).ConfigureAwait(false);
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
                if (!dryRun && plan.Smart && ExitCodeInterpreter.Interpret(exit2).Success)
                    _forceCopyPlanner.Commit(job.Name, plan.NewHashes);
            }
        }

        var interpreted = ExitCodeInterpreter.Interpret(exitCombined);
        var text = fullText.ToString();

        var result = new JobResult
        {
            JobName = job.Name,
            ExitCode = exitCombined,
            Success = interpreted.Success,
            Status = interpreted.Summary,
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

    /// <summary>Avvia un singolo processo robocopy e restituisce (exit code, output catturato).</summary>
    private async Task<(int ExitCode, string Output)> RunPassAsync(
        IReadOnlyList<string> args, IProgress<string>? progress, CancellationToken ct)
    {
        var output = new StringBuilder();

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
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }

        return (process.ExitCode, output.ToString());
    }
}
