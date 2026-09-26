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
    /// <param name="destinationOverride">Destinazione alternativa (usata dal versioning per scrivere nello snapshot corrente).</param>
    /// <param name="sourceOverride">Sorgente alternativa (usata da VSS per leggere dallo snapshot congelato).</param>
    /// <param name="maxThreads">Tetto ai thread <c>/MT</c> deciso a runtime dal tipo di disco (vedi <see cref="StorageProbe"/>). null = nessun tetto.</param>
    public static IReadOnlyList<string> Build(
        BackupJob job, bool dryRun = false, string? logFile = null, string? destinationOverride = null,
        string? sourceOverride = null, int? maxThreads = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        var source = (sourceOverride ?? job.Source ?? "").Trim();
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

        // Copia multi-thread — MA non insieme a /IPG: robocopy applica il ritardo per thread,
        // quindi con piu' thread il limite di banda diventerebbe imprevedibile.
        if (job.InterPacketGapMs > 0)
        {
            args.Add($"/IPG:{job.InterPacketGapMs}");
        }
        else if (job.MultiThread > 0)
        {
            var threads = Math.Min(job.MultiThread, Math.Min(MaxThreads, maxThreads ?? MaxThreads));
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

        // Esclusioni cartelle. Alla lista dell'utente si aggiunge la copia della configurazione
        // (vedi ConfigMirror) quando la destinazione E' la radice del volume: in quel caso la
        // cartella RoboKeep-config sta dentro la destinazione, non esiste in sorgente, e un mirror
        // la cancellerebbe come file "extra". Con una destinazione in sottocartella il problema non
        // c'e': la copia sta piu' in alto, fuori dalla portata del job.
        var excludeDirs = (job.ExcludeDirs ?? new List<string>())
            .Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        // Si esclude il PERCORSO della cartella, non il nome: con il nome nudo robocopy salterebbe
        // ogni "RoboKeep-config" che incontra, anche quello in SORGENTE — e un disco che e' la
        // destinazione di un job e la sorgente di un altro ne ha uno, che va copiato come tutto
        // il resto.
        if (VolumeRootOf(dest) is { } destRoot)
            excludeDirs.Add(Path.Combine(destRoot, ConfigMirror.FolderName));
        if (excludeDirs.Count > 0)
        {
            args.Add("/XD");
            args.AddRange(excludeDirs);
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
        string? destinationOverride = null, string? sourceOverride = null, int? maxThreads = null,
        bool includeModified = false)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(filters);

        var source = (sourceOverride ?? job.Source ?? "").Trim();
        var dest = (destinationOverride ?? job.Destination ?? "").Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(dest))
            throw new InvalidOperationException(
                $"Il job '{job.Name}' deve avere sorgente e destinazione valorizzate.");

        var args = new List<string> { source, dest };
        args.AddRange(filters.Where(f => !string.IsNullOrWhiteSpace(f)));

        args.Add("/E");                 // ricorsivo, mai /MIR (la passata forzata non cancella)
        args.Add("/IS");                // include same: copia anche i file identici
        args.Add("/IT");                // include tweaked
        // I robocopy recenti classificano "modificato" (change time diverso) il file riscritto con
        // stessa data e dimensione - proprio il caso d'uso della forza copia - e senza /IM lo saltano
        // nonostante /IS /IT. L'opzione non esiste nei robocopy vecchi: la decide il chiamante.
        if (includeModified)
            args.Add("/IM");
        args.Add(job.CopyAll ? "/COPYALL" : "/COPY:DAT");
        args.Add("/XJ");

        if (job.InterPacketGapMs > 0)
            args.Add($"/IPG:{job.InterPacketGapMs}");
        else if (job.MultiThread > 0)
            args.Add($"/MT:{Math.Min(job.MultiThread, Math.Min(MaxThreads, maxThreads ?? MaxThreads))}");

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

    /// <summary>La radice del volume locale (<c>E:\</c>) quando il percorso E' quella radice
    /// — <c>E:\</c>, <c>E:</c>, <c>E:\.</c> sono lo stesso posto scritto in tre modi — altrimenti
    /// null: solo con la destinazione sulla radice la cartella della copia della configurazione
    /// finisce dentro la destinazione. Le share UNC non contano: la copia non viene mai scritta su
    /// una destinazione di rete.
    /// <para>Resta un lavoro di stringhe, senza toccare il disco (<c>GetFullPath</c> normalizza e
    /// non legge nulla): questa classe deve poter girare su percorsi di dischi non collegati.</para></summary>
    private static string? VolumeRootOf(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root)) return null;
            if (root.StartsWith(@"\\", StringComparison.Ordinal)) return null;
            return string.Equals(full.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase)
                ? root
                : null;
        }
        catch { return null; }
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
