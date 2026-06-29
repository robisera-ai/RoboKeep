using System.Text;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Traduce un <see cref="BackupJob"/> nella lista di argomenti per robocopy.
/// Funzione pura e senza effetti collaterali: facilmente testabile.
/// </summary>
public static class RobocopyArgsBuilder
{
    private const int MaxThreads = 128;

    /// <summary>
    /// Costruisce gli argomenti per robocopy nell'ordine
    /// <c>&lt;sorgente&gt; &lt;destinazione&gt; [opzioni]</c>.
    /// </summary>
    /// <param name="job">Definizione del job.</param>
    /// <param name="dryRun">Se true aggiunge <c>/L</c> (anteprima: nessuna modifica).</param>
    /// <param name="logFile">Se valorizzato aggiunge <c>/TEE</c> e <c>/LOG:&lt;file&gt;</c>.</param>
    public static IReadOnlyList<string> Build(
        BackupJob job, bool dryRun = false, string? logFile = null, string? destinationOverride = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        var source = (job.Source ?? "").Trim();
        var dest = (destinationOverride ?? job.Destination ?? "").Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(dest))
            throw new InvalidOperationException(
                $"Il job '{job.Name}' deve avere sorgente e destinazione valorizzate.");

        var args = new List<string> { source, dest };

        // Modalità mirror vs copia/aggiornamento senza cancellazione.
        args.Add(job.Mirror ? "/MIR" : "/E");

        // Non sovrascrivere i file più recenti in destinazione.
        if (job.ExcludeOlder)
            args.Add("/XO");

        // Cosa copiare degli attributi del file.
        args.Add(job.CopyAll ? "/COPYALL" : "/COPY:DAT");

        // Esclude le junction per evitare loop/ricorsioni indesiderate.
        args.Add("/XJ");

        // Copia multi-thread.
        if (job.MultiThread > 0)
        {
            var threads = Math.Min(job.MultiThread, MaxThreads);
            args.Add($"/MT:{threads}");
        }

        // Ottimizzazioni per file grandi: /J e /Z sono mutuamente esclusivi in robocopy,
        // quindi /J ha la precedenza se per errore fossero entrambi attivi.
        if (job.UnbufferedIO)
            args.Add("/J");
        else if (job.Restartable)
            args.Add("/Z");

        // Log verboso: include nel log anche i file identici/saltati, non solo i copiati/extra.
        if (job.LogAllFiles)
            args.Add("/V");

        // Esclusioni file.
        if (job.ExcludeFiles is { Count: > 0 })
        {
            args.Add("/XF");
            args.AddRange(job.ExcludeFiles.Where(f => !string.IsNullOrWhiteSpace(f)));
        }

        // Esclusioni cartelle.
        if (job.ExcludeDirs is { Count: > 0 })
        {
            args.Add("/XD");
            args.AddRange(job.ExcludeDirs.Where(d => !string.IsNullOrWhiteSpace(d)));
        }

        // Tentativi e attesa.
        args.Add($"/R:{Math.Max(0, job.Retries)}");
        args.Add($"/W:{Math.Max(0, job.Wait)}");

        // Anteprima: elenca soltanto, non modifica nulla.
        if (dryRun)
            args.Add("/L");

        // Logging su file (oltre allo stdout grazie a /TEE).
        if (!string.IsNullOrWhiteSpace(logFile))
        {
            args.Add("/TEE");
            args.Add($"/LOG:{logFile}");
        }

        return args;
    }

    /// <summary>
    /// Costruisce gli argomenti della passata "forza copia": copia i file indicati da
    /// <paramref name="filters"/> anche se identici (<c>/IS /IT</c>), senza mai cancellare
    /// (niente <c>/MIR</c>) e senza saltare i più vecchi (niente <c>/XO</c>).
    /// </summary>
    public static IReadOnlyList<string> BuildForceCopyPass(
        BackupJob job, IReadOnlyList<string> filters, bool dryRun = false, string? logFile = null,
        string? destinationOverride = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(filters);

        var source = (job.Source ?? "").Trim();
        var dest = (destinationOverride ?? job.Destination ?? "").Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(dest))
            throw new InvalidOperationException(
                $"Il job '{job.Name}' deve avere sorgente e destinazione valorizzate.");

        var args = new List<string> { source, dest };
        args.AddRange(filters.Where(f => !string.IsNullOrWhiteSpace(f)));

        args.Add("/E");                 // ricorsivo, mai /MIR (la passata forzata non cancella)
        args.Add("/IS");                // include same: copia anche i file identici
        args.Add("/IT");                // include tweaked
        args.Add(job.CopyAll ? "/COPYALL" : "/COPY:DAT");
        args.Add("/XJ");

        if (job.MultiThread > 0)
            args.Add($"/MT:{Math.Min(job.MultiThread, MaxThreads)}");

        if (job.UnbufferedIO)
            args.Add("/J");
        else if (job.Restartable)
            args.Add("/Z");

        args.Add($"/R:{Math.Max(0, job.Retries)}");
        args.Add($"/W:{Math.Max(0, job.Wait)}");

        if (dryRun)
            args.Add("/L");

        if (!string.IsNullOrWhiteSpace(logFile))
        {
            args.Add("/TEE");
            args.Add($"/LOG:{logFile}");
        }

        return args;
    }

    /// <summary>
    /// Rende gli argomenti come riga di comando leggibile (per anteprima nella GUI).
    /// NB: solo per visualizzazione; l'esecuzione usa la lista di argomenti, non questa stringa.
    /// </summary>
    public static string ToDisplayString(IEnumerable<string> args)
    {
        var sb = new StringBuilder("robocopy");
        foreach (var a in args)
        {
            sb.Append(' ');
            if (string.IsNullOrEmpty(a) || a.Contains(' '))
                sb.Append('"').Append(a).Append('"');
            else
                sb.Append(a);
        }
        return sb.ToString();
    }
}
