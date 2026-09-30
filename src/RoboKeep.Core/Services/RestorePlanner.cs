namespace RoboKeep.Core.Services;

/// <summary>Un file del piano di ripristino: dove va rimesso (percorso relativo, uguale a quello che
/// aveva nella sorgente) e da dove si copia (<paramref name="SourcePath"/>: <c>current</c>, una
/// cartella-versione o una cartella datata del modello a hard-link).</summary>
public sealed record RestoreEntry(string RelativePath, string SourcePath, long Size, DateTime? LastWrite);

/// <summary>
/// L'albero com'era a una certa data: i file da rimettere, le cartelle note (comprese quelle che a
/// quella data erano vuote) e il totale in byte, che serve alla conferma prima di copiare.
/// </summary>
public sealed record RestorePlan(
    IReadOnlyDictionary<string, RestoreEntry> Files,
    IReadOnlyCollection<string> Directories,
    long TotalBytes)
{
    /// <summary>Piano vuoto: niente da ripristinare (destinazione mancante, selezione vuota).</summary>
    public static RestorePlan Empty { get; } = new(
        new Dictionary<string, RestoreEntry>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<string>(), 0);
}

/// <summary>Un file visto dal pianificatore. Non e' un <see cref="FileInfo"/> apposta: i test
/// costruiscono alberi in memoria, e <see cref="FileInfo.Length"/> pretende un file vero sul
/// disco.</summary>
public sealed record RestoreFile(string FullPath, long Size, DateTime? LastWrite);

/// <summary>
/// Tutte le letture dal disco di cui il pianificatore ha bisogno, ciascuna dietro una delega: cosi'
/// <see cref="RestorePlanner"/> resta una funzione pura dei dati che riceve e i test possono
/// descrivere un archivio intero senza scrivere un byte. In produzione si usa
/// <see cref="Real"/>, che legge davvero.
/// </summary>
public sealed class RestoreReader
{
    /// <summary>Tutti i file sotto una cartella, ricorsivamente (percorso completo).</summary>
    public Func<string, IEnumerable<RestoreFile>> Files { get; init; } = RealFiles;

    /// <summary>Tutte le sottocartelle di una cartella, ricorsivamente (percorso completo): servono
    /// le vuote, che nessun file rivelerebbe.</summary>
    public Func<string, IEnumerable<string>> Directories { get; init; } = RealDirectories;

    /// <summary>Dimensione e data di un singolo file, null se quel file non c'e'.</summary>
    public Func<string, RestoreFile?> Stat { get; init; } = RealStat;

    public Func<string, bool> DirectoryExists { get; init; } = Directory.Exists;

    /// <summary>I punti nel tempo di <c>versions\</c> (cartelle e versioni di solo manifest).</summary>
    public Func<string, IReadOnlyList<VersionPoint>> Versions { get; init; } = VersionCatalog.List;

    /// <summary>Il manifest di una cartella-versione, null se manca o e' illeggibile.</summary>
    public Func<string, VersionManifest?> Manifest { get; init; } = VersionManifest.ReadFrom;

    /// <summary>I nomi delle cartelle datate del modello a hard-link, dalla piu' recente.</summary>
    public Func<string, IReadOnlyList<string>> Dated { get; init; } = SnapshotName.ListValid;

    /// <summary>Il lettore che legge davvero dal disco.</summary>
    public static RestoreReader Real { get; } = new();

    private static EnumerationOptions Options => new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint, // junction/symlink: non si seguono
        IgnoreInaccessible = true,
    };

    private static IEnumerable<RestoreFile> RealFiles(string dir)
    {
        var found = new List<RestoreFile>();
        try
        {
            if (!Directory.Exists(dir)) return found;
            foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", Options))
                found.Add(new RestoreFile(f.FullName, f.Length, f.LastWriteTime));
        }
        catch { /* best-effort: meglio un elenco parziale che una finestra che non si apre */ }
        return found;
    }

    private static IEnumerable<string> RealDirectories(string dir)
    {
        var found = new List<string>();
        try
        {
            if (!Directory.Exists(dir)) return found;
            found.AddRange(Directory.EnumerateDirectories(dir, "*", Options));
        }
        catch { /* best-effort, come sopra */ }
        return found;
    }

    private static RestoreFile? RealStat(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            return fi.Exists ? new RestoreFile(fi.FullName, fi.Length, fi.LastWriteTime) : null;
        }
        catch { return null; }
    }
}

/// <summary>
/// Ricostruisce l'albero intero di un job a un punto nel tempo, per entrambi i modelli di versione,
/// senza copiare niente: il risultato e' un elenco di percorsi da cui copiare, che
/// <see cref="RestoreCopier"/> esegue e la finestra di ripristino mostra.
/// <para><b>Che cosa significa una data: dipende dal modello, e va detto.</b> Ciascun modello ha
/// una lettura naturale, quella che il suo layout sul disco rende completa. Forzarle a coincidere
/// renderebbe irraggiungibile una parte dei dati, quindi la finestra usa due etichette diverse
/// («Dopo il backup del…» / «Prima del backup del…») invece di una sola che mentirebbe.</para>
/// <para><b>Hard-link = DOPO il backup.</b> La cartella datata E' l'albero come quel backup lo ha
/// lasciato, quindi il piano e' il suo contenuto. «Adesso» e' la cartella datata piu' recente: nel
/// modello a hard-link non esiste un <c>current</c>, il backup attuale e' l'ultima versione.</para>
/// <para><b>Per differenza = PRIMA del backup.</b> La cartella di una versione contiene le copie di
/// cio' che quel backup stava per sostituire o cancellare: e' lo stato di <i>prima</i> di quel
/// backup, ed e' quello che la versione sa ricostruire. Si parte da <c>current</c> (adesso) e si
/// applicano le versioni con data <b>&gt;=</b> di quella scelta, dalla piu' vecchia alla piu'
/// recente: i file che quel backup ha <i>sostituito</i> o <i>cancellato</i> tornano dalla sua
/// cartella, quelli che ha <i>aggiunto</i> escono dal piano, perche' prima non esistevano. Il primo
/// che risolve un percorso vince: in ordine crescente e' la versione piu' vicina alla data scelta,
/// cioe' proprio lo stato che c'era allora.</para>
/// <para><b>Perche' non «dopo» anche qui.</b> Con «dopo il backup P» (versioni &gt; P) le copie
/// della cartella piu' vecchia non sarebbero raggiungibili da nessun punto: servirebbe il punto
/// precedente, che spesso e' un manifest di sole aggiunte gia' potato dalla ritenzione. Un file
/// cancellato dalla sorgente, la cui unica copia sta proprio li', diventerebbe irrecuperabile dalla
/// finestra. Con «prima del backup P» ogni cartella-versione e' raggiungibile scegliendo il suo
/// punto, e gli stati ricostruibili sono tutti quelli possibili: ogni «prima» piu' «Adesso».</para>
/// <para>Attenzione a non confondere due cose diverse: la cartella <c>versions\2026-09-28\</c>
/// contiene <i>cio' che e' cambiato quel giorno</i>; il piano di ripristino a quel punto e' invece
/// <i>l'albero intero</i> — quasi sempre molti piu' file.</para>
/// </summary>
public static class RestorePlanner
{
    private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;
    private const StringComparison Ord = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Il piano per la destinazione indicata al punto nel tempo indicato.
    /// </summary>
    /// <param name="mode">Modello di versione del job (vedi <see cref="VersioningLayout.Detect"/>).</param>
    /// <param name="destination">Radice della destinazione del job (non <c>current</c>).</param>
    /// <param name="pointInTime">La data del backup scelto, oppure null per «adesso» (lo stato
    /// corrente). Hard-link: l'albero DOPO quel backup. Per differenza: l'albero PRIMA di quel
    /// backup (vedi il commento della classe).</param>
    /// <param name="reader">Le letture dal disco, iniettabili nei test; default
    /// <see cref="RestoreReader.Real"/>.</param>
    public static RestorePlan Resolve(VersioningMode mode, string destination, DateTime? pointInTime,
        RestoreReader? reader = null)
    {
        var fs = reader ?? RestoreReader.Real;
        var dest = (destination ?? "").Trim();
        if (dest.Length == 0) return RestorePlan.Empty;

        return mode == VersioningMode.HardLinks
            ? ResolveHardLinks(dest, pointInTime, fs)
            : ResolveDifferential(dest, pointInTime, fs);
    }

    /// <summary>
    /// Il sotto-piano dei soli percorsi scelti. Una <b>cartella</b> scelta porta con se' tutto
    /// quello che sta sotto: e' il significato ovvio di una casella spuntata su una cartella, e
    /// l'unico che non obblighi l'utente ad aprire ogni ramo per essere sicuro di aver preso tutto.
    /// </summary>
    /// <param name="plan">Il piano completo da cui si sceglie.</param>
    /// <param name="selected">Percorsi relativi di file, oppure di cartelle (prefissi).</param>
    public static RestorePlan Select(RestorePlan plan, IEnumerable<string> selected)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(selected);

        var picks = selected.Select(Normalize).Where(s => s.Length > 0).Distinct(Cmp).ToList();
        if (picks.Count == 0) return RestorePlan.Empty;

        bool Covered(string rel) =>
            picks.Any(p => rel.Equals(p, Ord) || rel.StartsWith(p + '\\', Ord));

        var files = new Dictionary<string, RestoreEntry>(Cmp);
        long total = 0;
        foreach (var (rel, entry) in plan.Files)
        {
            if (!Covered(rel)) continue;
            files[rel] = entry;
            total += entry.Size;
        }

        var dirs = new HashSet<string>(plan.Directories.Where(Covered), Cmp);
        // Le cartelle che CONTENGONO i file scelti non sono «scelte», ma senza di loro i file non
        // avrebbero dove atterrare: ci vanno lo stesso.
        foreach (var rel in files.Keys) AddAncestors(dirs, rel);

        return new RestorePlan(files, Ordered(dirs), total);
    }

    private static RestorePlan ResolveHardLinks(string dest, DateTime? pointInTime, RestoreReader fs)
    {
        var dated = fs.Dated(dest);
        // Nessuna cartella datata: il job ha le versioni accese ma non ne ha ancora scritta
        // nessuna, e nella destinazione c'e' solo il mirror piatto. E' comunque «lo stato di
        // adesso», ed e' l'unica risposta utile che si possa dare.
        var root = dated.Count == 0
            ? dest
            : Path.Combine(dest, pointInTime is { } when ? Nearest(dated, when) : dated[0]);

        var files = new Dictionary<string, RestoreEntry>(Cmp);
        long total = 0;
        foreach (var f in fs.Files(root))
        {
            var rel = Normalize(Path.GetRelativePath(root, f.FullPath));
            if (rel.Length == 0) continue;
            files[rel] = new RestoreEntry(rel, f.FullPath, f.Size, f.LastWrite);
            total += f.Size;
        }

        var dirs = new HashSet<string>(Cmp);
        foreach (var d in fs.Directories(root))
        {
            var rel = Normalize(Path.GetRelativePath(root, d));
            if (rel.Length > 0) dirs.Add(rel);
        }
        foreach (var rel in files.Keys) AddAncestors(dirs, rel);

        return new RestorePlan(files, Ordered(dirs), total);
    }

    /// <summary>La cartella datata dell'albero «come quel backup lo ha lasciato»: quella con la
    /// data esatta se c'e', altrimenti la prima successiva. Se la data richiesta e' piu' recente di
    /// tutte, l'ultima: e' lo stato piu' vicino che esista.</summary>
    private static string Nearest(IReadOnlyList<string> dated, DateTime when)
    {
        var byDate = dated
            .Select(n => { SnapshotName.TryParse(n, out var d); return (Name: n, Date: d); })
            .OrderBy(x => x.Date).ToList();
        foreach (var x in byDate)
            if (x.Date >= when) return x.Name;
        return byDate[^1].Name;
    }

    private static RestorePlan ResolveDifferential(string dest, DateTime? pointInTime, RestoreReader fs)
    {
        var current = VersioningLayout.CurrentDir(dest);
        var versionsDir = VersioningLayout.VersionsDir(dest);

        var files = new Dictionary<string, RestoreEntry>(Cmp);
        foreach (var f in fs.Files(current))
        {
            var rel = Normalize(Path.GetRelativePath(current, f.FullPath));
            if (rel.Length == 0) continue;
            files[rel] = new RestoreEntry(rel, f.FullPath, f.Size, f.LastWrite);
        }

        var dirs = new HashSet<string>(Cmp);
        if (pointInTime is null)
        {
            // «Adesso»: le cartelle vuote di current sono cartelle vere di oggi e vanno ricreate.
            // A una data passata invece non si sa se esistessero gia', e inventarle sarebbe peggio
            // che ometterle: le uniche cartelle certe sono quelle dei file del piano e quelle che
            // un backup ha portato via intere.
            foreach (var d in fs.Directories(current))
            {
                var rel = Normalize(Path.GetRelativePath(current, d));
                if (rel.Length > 0) dirs.Add(rel);
            }
        }
        else
        {
            // >= : si applica anche la versione scelta. La sua cartella contiene lo stato di PRIMA
            // di quel backup, ed e' esattamente lo stato che si e' chiesto.
            var resolved = new HashSet<string>(Cmp);
            foreach (var point in fs.Versions(versionsDir)
                         .Where(p => p.Date >= pointInTime.Value)
                         .OrderBy(p => p.Date))
            {
                ApplyVersion(Path.Combine(versionsDir, point.Name), point, fs, files, dirs, resolved);
            }
        }

        foreach (var rel in files.Keys) AddAncestors(dirs, rel);

        return new RestorePlan(files, Ordered(dirs), files.Values.Sum(e => e.Size));
    }

    /// <summary>Riporta indietro il piano di una versione: i file che quel backup ha sostituito o
    /// cancellato tornano dalla sua cartella, quelli che ha aggiunto escono dal piano. Un percorso
    /// gia' risolto da una versione piu' vicina alla data non si tocca.</summary>
    private static void ApplyVersion(string versionDir, VersionPoint point, RestoreReader fs,
        Dictionary<string, RestoreEntry> files, HashSet<string> dirs, HashSet<string> resolved)
    {
        var manifest = fs.Manifest(versionDir);
        if (manifest is null)
        {
            // Manifest perduto (cancellato a mano, disco corrotto): la cartella resta un elenco di
            // stati PRECEDENTI, ed e' meglio di niente. Non si puo' sapere che cosa quel backup
            // avesse aggiunto, quindi non si toglie nulla dal piano.
            if (!point.HasFolder) return;
            foreach (var f in fs.Files(versionDir))
            {
                var rel = Normalize(Path.GetRelativePath(versionDir, f.FullPath));
                if (rel.Length == 0 || !resolved.Add(rel)) continue;
                files[rel] = new RestoreEntry(rel, f.FullPath, f.Size, f.LastWrite);
            }
            return;
        }

        foreach (var raw in manifest.Changed.Concat(manifest.Deleted))
        {
            var rel = Normalize(raw);
            if (rel.Length == 0 || resolved.Contains(rel)) continue;
            var candidate = Path.Combine(versionDir, rel);

            if (fs.Stat(candidate) is { } file)
            {
                resolved.Add(rel);
                files[rel] = new RestoreEntry(rel, file.FullPath, file.Size, file.LastWrite);
                continue;
            }

            if (fs.DirectoryExists(candidate))
            {
                // Cartella intera portata via dal mirror perche' sparita dalla sorgente: nel
                // manifest e' UNA voce, sul disco e' un albero. Va ripreso tutto, comprese le
                // sottocartelle vuote — di quella roba questa e' l'unica copia rimasta.
                resolved.Add(rel);
                dirs.Add(rel);
                foreach (var f in fs.Files(candidate))
                {
                    var child = Path.Combine(rel, Normalize(Path.GetRelativePath(candidate, f.FullPath)));
                    if (!resolved.Add(child)) continue;
                    files[child] = new RestoreEntry(child, f.FullPath, f.Size, f.LastWrite);
                }
                foreach (var d in fs.Directories(candidate))
                    dirs.Add(Path.Combine(rel, Normalize(Path.GetRelativePath(candidate, d))));
                continue;
            }

            // Nella cartella della versione quel file non c'e' (era in uso e non si e' potuto
            // mettere da parte: il backup lo ha lasciato intatto e saltato). Non si marca risolto:
            // se una versione successiva ne ha una copia, la prende quella.
        }

        foreach (var raw in manifest.Added)
        {
            var rel = Normalize(raw);
            if (rel.Length == 0 || !resolved.Add(rel)) continue;
            files.Remove(rel); // a quella data questo file non esisteva ancora
        }
    }

    /// <summary>Percorso relativo in forma canonica: separatori di Windows, senza separatori
    /// iniziali o finali. I manifest vengono scritti da noi, ma un file modificato a mano non deve
    /// far sbagliare il confronto tra percorsi.</summary>
    private static string Normalize(string? rel) =>
        (rel ?? "").Trim().Replace('/', '\\').Trim('\\');

    private static void AddAncestors(HashSet<string> dirs, string rel)
    {
        var dir = Path.GetDirectoryName(rel);
        while (!string.IsNullOrEmpty(dir))
        {
            if (!dirs.Add(dir)) return; // c'era gia': i suoi padri pure
            dir = Path.GetDirectoryName(dir);
        }
    }

    /// <summary>Cartelle dalla piu' corta alla piu' profonda: creandole in quest'ordine il padre
    /// esiste sempre prima del figlio.</summary>
    private static IReadOnlyCollection<string> Ordered(HashSet<string> dirs) =>
        dirs.OrderBy(d => d.Count(c => c == '\\')).ThenBy(d => d, Cmp).ToList();
}
