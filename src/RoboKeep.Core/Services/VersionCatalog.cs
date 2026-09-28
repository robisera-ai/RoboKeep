namespace RoboKeep.Core.Services;

/// <summary>Un punto nel tempo nell'archivio di un job per differenza.
/// <para><see cref="HasFolder"/> distingue i due casi: una versione con una CARTELLA contiene gli
/// stati precedenti dei file che quel backup ha sostituito o cancellato; una versione di SOLO
/// MANIFEST è un backup che non ha sostituito né cancellato niente (ha solo aggiunto file), e la
/// cartella non c'è perché sarebbe vuota.</para></summary>
public sealed record VersionPoint(string Name, DateTime Date, bool HasFolder);

/// <summary>
/// L'archivio delle versioni per differenza letto come elenco di punti nel tempo, e le regole per
/// tenerlo in ordine.
/// <para><b>Perché i backup di sole aggiunte non creano una cartella:</b> un archivio di foto o di
/// documenti cresce e basta — si aggiungono file, non se ne sostituiscono né cancellano. Con una
/// cartella (vuota) per ogni backup, «tieni le ultime 10 versioni» le riempirebbe tutte di niente in
/// dieci giorni, e la ritenzione cancellerebbe l'unica versione che conteneva davvero qualcosa: la
/// copia di un file sparito dalla sorgente. Le cartelle esistono solo dove c'è qualcosa dentro, così
/// «tieni N versioni» conta N versioni VERE. Del backup di sole aggiunte resta il manifest, che pesa
/// un nulla e serve al ripristino per sapere che a quella data quei file non c'erano ancora.</para>
/// </summary>
public static class VersionCatalog
{
    /// <summary>Tutti i punti nel tempo di <paramref name="versionsDir"/> (cartelle e versioni di
    /// solo manifest), dal più vecchio al più recente. Le <c>.inprogress</c> e i nomi non
    /// interpretabili non compaiono. Cartella mancante → elenco vuoto.</summary>
    public static IReadOnlyList<VersionPoint> List(string versionsDir)
    {
        if (!Directory.Exists(versionsDir)) return Array.Empty<VersionPoint>();

        var points = new List<VersionPoint>();
        var withFolder = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in SnapshotName.ListValid(versionsDir))
        {
            SnapshotName.TryParse(name, out var date);
            points.Add(new VersionPoint(name, date, HasFolder: true));
            withFolder.Add(name);
        }

        foreach (var name in ManifestNames(versionsDir))
        {
            if (withFolder.Contains(name)) continue; // è il gemello di una cartella già elencata
            SnapshotName.TryParse(name, out var date);
            points.Add(new VersionPoint(name, date, HasFolder: false));
        }

        return points.OrderBy(p => p.Date).ToList();
    }

    /// <summary>
    /// I manifest "orfani" (senza cartella) che si possono cancellare: quelli più VECCHI della
    /// cartella-versione più vecchia rimasta. Funzione pura, il tempo non c'entra.
    /// <para>La regola viene dal ripristino: per tornare a una data servono le cartelle di tutte le
    /// versioni da quella data in poi. Se la cartella più vecchia è del 10, nessun ripristino può
    /// arrivare più indietro del 10 — gli stati precedenti non ci sono più — e un manifest del 3 non
    /// può aiutare nessuno. Un manifest più RECENTE della cartella più vecchia invece serve, perché
    /// dice quali file, a quella data, non esistevano ancora.</para>
    /// <para>Senza nessuna cartella non si cancella niente: i manifest sono l'unica memoria rimasta
    /// e pesano pochi byte.</para>
    /// </summary>
    /// <param name="dirNames">Nomi delle cartelle-versione presenti.</param>
    /// <param name="manifestNames">Nomi (parte data) dei manifest presenti.</param>
    public static IReadOnlyList<string> ManifestsToPrune(
        IEnumerable<string> dirNames, IEnumerable<string> manifestNames)
    {
        ArgumentNullException.ThrowIfNull(dirNames);
        ArgumentNullException.ThrowIfNull(manifestNames);

        var dirs = dirNames
            .Where(n => !SnapshotName.IsInProgress(n) && SnapshotName.TryParse(n, out _))
            .ToList();
        if (dirs.Count == 0) return Array.Empty<string>();

        var kept = new HashSet<string>(dirs, StringComparer.OrdinalIgnoreCase);
        var oldest = dirs.Min(n => { SnapshotName.TryParse(n, out var d); return d; });

        return manifestNames
            .Where(n => !SnapshotName.IsInProgress(n) && SnapshotName.TryParse(n, out _))
            .Where(n => !kept.Contains(n))
            .Where(n => { SnapshotName.TryParse(n, out var d); return d < oldest; })
            .ToList();
    }

    /// <summary>Nomi (parte data) dei manifest presenti nella cartella delle versioni.</summary>
    public static IReadOnlyList<string> ManifestNames(string versionsDir)
    {
        if (!Directory.Exists(versionsDir)) return Array.Empty<string>();
        return Directory.GetFiles(versionsDir, "*" + VersionManifest.FileSuffix)
            .Select(Path.GetFileName).OfType<string>()
            .Select(f => f[..^VersionManifest.FileSuffix.Length])
            .Where(n => SnapshotName.TryParse(n, out _))
            .ToList();
    }
}
