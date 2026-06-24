using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using RobocopySW.Core.Models;

namespace RobocopySW.Core.Services;

/// <summary>Risultato grezzo di un'esecuzione robocopy: esito + output testuale completo.</summary>
public sealed class RobocopyRunResult
{
    public required JobResult Result { get; init; }
    public required string Output { get; init; }
}

/// <summary>
/// Esegue robocopy come processo esterno, catturando l'output in tempo reale,
/// e ne ricava un <see cref="JobResult"/> (esito + conteggi).
/// Usa il robocopy di sistema (<c>%WINDIR%\System32\Robocopy.exe</c>): sempre aggiornato con Windows.
/// </summary>
public sealed class RobocopyRunner
{
    private readonly string _robocopyPath;

    // Robocopy scrive l'output nella code page OEM del sistema (es. CP850 in italiano):
    // leggerlo con quella codifica evita accenti mancanti/garbled.
    private static readonly Encoding OemEncoding = ResolveOemEncoding();

    public RobocopyRunner(string? robocopyPath = null) =>
        _robocopyPath = robocopyPath ?? Path.Combine(Environment.SystemDirectory, "Robocopy.exe");

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

    /// <summary>
    /// Esegue il job. <paramref name="dryRun"/> attiva l'anteprima (nessuna modifica reale).
    /// <paramref name="progress"/> riceve ogni riga di output per la visualizzazione live.
    /// </summary>
    public async Task<RobocopyRunResult> RunAsync(
        BackupJob job, bool dryRun = false, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var args = RobocopyArgsBuilder.Build(job, dryRun);
        var output = new StringBuilder();
        var started = DateTime.Now;

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

        // Alla cancellazione, termina robocopy (e i suoi thread figli) per non lasciare il processo appeso.
        await using (ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* il processo potrebbe essere già terminato */ }
        }))
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }

        var exit = process.ExitCode;
        var interpreted = ExitCodeInterpreter.Interpret(exit);
        var text = output.ToString();
        var counts = RobocopyOutputParser.ParseCounts(text.Split('\n'));

        var result = new JobResult
        {
            JobName = job.Name,
            ExitCode = exit,
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
        };

        return new RobocopyRunResult { Result = result, Output = text };
    }
}
