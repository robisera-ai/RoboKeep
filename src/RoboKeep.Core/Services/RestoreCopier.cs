namespace RoboKeep.Core.Services;

/// <summary>Un file che non si e' potuto rimettere a posto, con il motivo gia' leggibile.</summary>
public sealed record RestoreFailure(string RelativePath, string Error);

/// <summary>Esito di un ripristino: quanti file sono arrivati, quanti byte, che cosa e' rimasto
/// indietro e se l'utente ha annullato a meta'.</summary>
public sealed record RestoreOutcome(
    int Copied, int Total, long Bytes, IReadOnlyList<RestoreFailure> Failures, bool Cancelled);

/// <summary>
/// Esegue un <see cref="RestorePlan"/>: copia i file nella cartella scelta, conservando la data di
/// ultima modifica e ricreando le cartelle note (anche quelle che a quella data erano vuote).
/// <para><b>Non sovrascrive mai niente</b> (<c>File.Copy</c> senza overwrite): un ripristino e' un
/// recupero, non un mirror, e un file gia' presente nella cartella scelta potrebbe essere proprio
/// quello che l'utente sta cercando di salvare. Chi c'e' gia' finisce tra gli errori, elencati alla
/// fine; un file che non si copia non ferma gli altri.</para>
/// <para><b>Non esce mai dalla cartella scelta</b>: ogni percorso relativo viene ricomposto e
/// ricontrollato, e uno che punterebbe fuori (percorso assoluto, <c>..</c>) viene rifiutato invece
/// di essere seguito.</para>
/// </summary>
public static class RestoreCopier
{
    /// <summary>
    /// Copia il piano in <paramref name="targetFolder"/>.
    /// </summary>
    /// <param name="plan">Che cosa rimettere e da dove (vedi <see cref="RestorePlanner"/>).</param>
    /// <param name="targetFolder">Cartella in cui ricostruire l'albero. Viene creata se manca.</param>
    /// <param name="jobSource">Sorgente del job, per il controllo di sicurezza: ripristinare
    /// SOPRA la sorgente sovrascriverebbe i file di oggi con quelli di ieri, e con un solo clic.
    /// La finestra lo impedisce gia', qui si ricontrolla — un errore chiaro invece di un disastro
    /// silenzioso. null = nessun controllo (chiamata senza job, come nei test).</param>
    /// <param name="progress">Avanzamento (fatti, totale) per la barra.</param>
    public static Task<RestoreOutcome> CopyAsync(RestorePlan plan, string targetFolder,
        string? jobSource = null, IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default, string? jobDestination = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var target = (targetFolder ?? "").Trim();
        if (target.Length == 0)
            throw new ArgumentException(CoreLoc.S("Restore_NoTarget"), nameof(targetFolder));

        string root;
        try { root = Path.GetFullPath(target); }
        catch (Exception ex) { throw new ArgumentException(CoreLoc.S("Restore_NoTarget"), nameof(targetFolder), ex); }

        if (IsSourceOrInside(root, jobSource))
            throw new InvalidOperationException(CoreLoc.S("Restore_TargetInsideSource"));
        if (TouchesBackup(root, jobDestination))
            throw new InvalidOperationException(CoreLoc.S("Restore_TargetInsideBackup"));

        // Il token NON si passa a Task.Run: un annullamento gia' chiesto deve dare un esito
        // «annullato» da mostrare nel riepilogo, non un'eccezione da intercettare.
        return Task.Run(() => Copy(plan, root, progress, ct));
    }

    /// <summary>
    /// true se <paramref name="target"/> E' la sorgente del job o sta dentro di essa. Stessa regola
    /// usata dalla finestra per spegnere il pulsante e dalla copia per rifiutarsi: una sola
    /// definizione, cosi' i due controlli non possono dire cose diverse.
    /// </summary>
    /// <summary>
    /// true se <paramref name="target"/> tocca la destinazione del job, cioe' il backup stesso:
    /// coincide, ci sta dentro (es. <c>…\current</c> o una cartella-versione) o la contiene.
    /// Ripristinare li' mescolerebbe file vecchi con il backup, e il run successivo li
    /// cancellerebbe o li scambierebbe per dati veri: il backup non e' un posto dove scrivere.
    /// </summary>
    public static bool TouchesBackup(string? target, string? jobDestination) =>
        JobPaths.Overlap(target, jobDestination);

    public static bool IsSourceOrInside(string? target, string? source)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(source)) return false;
        try
        {
            var t = Path.GetFullPath(target.Trim()).TrimEnd('\\');
            var s = Path.GetFullPath(source.Trim()).TrimEnd('\\');
            return t.Equals(s, StringComparison.OrdinalIgnoreCase)
                || t.StartsWith(s + '\\', StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; } // percorso non interpretabile: lo bocciera' la copia, non questa regola
    }

    private static RestoreOutcome Copy(RestorePlan plan, string root,
        IProgress<(int Done, int Total)>? progress, CancellationToken ct)
    {
        var failures = new List<RestoreFailure>();
        var total = plan.Files.Count;
        var copied = 0;
        long bytes = 0;

        Directory.CreateDirectory(root);

        // Le cartelle prima dei file, dalla piu' corta alla piu' profonda: cosi' anche quelle
        // VUOTE esistono a fine ripristino. Senza, una cartella che a quella data era vuota
        // sparirebbe dal recupero senza che nessuno se ne accorga.
        foreach (var rel in plan.Directories)
        {
            if (ct.IsCancellationRequested) break;
            if (Safe(root, rel) is not { } dir) { failures.Add(new RestoreFailure(rel, CoreLoc.S("Restore_UnsafePath"))); continue; }
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex) { failures.Add(new RestoreFailure(rel, ex.Message)); }
        }

        var done = 0;
        foreach (var (rel, entry) in plan.Files.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (ct.IsCancellationRequested)
                return new RestoreOutcome(copied, total, bytes, failures, Cancelled: true);

            done++;
            if (Safe(root, rel) is not { } destPath)
            {
                failures.Add(new RestoreFailure(rel, CoreLoc.S("Restore_UnsafePath")));
                progress?.Report((done, total));
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(entry.SourcePath, destPath, overwrite: false);
                // La data di ultima modifica E' un dato: e' quella che dice «questa e' la lettera
                // di marzo». Un ripristino che la azzerasse renderebbe irriconoscibili i file.
                if (entry.LastWrite is { } when)
                {
                    try { File.SetLastWriteTime(destPath, when); }
                    catch { /* la copia c'e': la data e' un di piu', non un fallimento */ }
                }
                copied++;
                bytes += entry.Size;
            }
            catch (Exception ex)
            {
                failures.Add(new RestoreFailure(rel, ex.Message));
            }

            progress?.Report((done, total));
        }

        return new RestoreOutcome(copied, total, bytes, failures, Cancelled: false);
    }

    /// <summary>Il percorso completo dove va rimesso un file, oppure null se quel percorso
    /// relativo uscirebbe dalla cartella scelta.</summary>
    private static string? Safe(string root, string rel)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rel) || Path.IsPathRooted(rel)) return null;
            var full = Path.GetFullPath(Path.Combine(root, rel));
            var prefix = root.TrimEnd('\\') + '\\';
            return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
        catch { return null; }
    }
}
