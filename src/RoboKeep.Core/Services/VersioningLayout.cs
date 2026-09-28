namespace RoboKeep.Core.Services;

/// <summary>Come un job tiene le versioni sul disco di backup.
/// <list type="bullet">
/// <item><see cref="HardLinks"/>: cartelle datate complete nella radice della destinazione, i file
/// invariati condivisi via hard-link. Serve NTFS locale.</item>
/// <item><see cref="Differential"/>: il mirror vive in <c>current\</c> e ogni backup mette da parte
/// in <c>versions\&lt;data&gt;\</c> i soli file che sostituisce o cancella. Funziona su exFAT,
/// FAT32 e rete, dove gli hard-link non esistono.</item>
/// </list></summary>
public enum VersioningMode { HardLinks, Differential }

/// <summary>
/// Decide quale dei due modelli di versione usa un job, e dove stanno le cartelle del modello per
/// differenza. La regola e' "il layout che c'e' vince": un backup gia' avviato non cambia modello
/// sotto i piedi dell'utente, altrimenti le versioni vecchie diventerebbero irraggiungibili.
/// Solo su una destinazione ancora senza layout si interroga il disco.
/// </summary>
public static class VersioningLayout
{
    /// <summary>Cartella che contiene il mirror vero nel modello per differenza.</summary>
    public const string CurrentFolderName = "current";

    /// <summary>Cartella che contiene le versioni datate nel modello per differenza.</summary>
    public const string VersionsFolderName = "versions";

    public static string CurrentDir(string destination) => Path.Combine(destination, CurrentFolderName);

    public static string VersionsDir(string destination) => Path.Combine(destination, VersionsFolderName);

    /// <summary>
    /// Il modello da usare per la destinazione indicata: cartelle datate nella radice → hard-link;
    /// cartella <c>current\</c> → differenza; destinazione vergine (o inesistente) → lo decide
    /// <paramref name="hardLinkProbe"/>, hard-link se il disco li supporta, differenza se no.
    /// </summary>
    /// <param name="hardLinkProbe">Prova funzionale del supporto hard-link, iniettabile nei test;
    /// default <see cref="HardLinkSupport.IsSupported"/>. Scrive un file di prova: viene chiamata
    /// solo quando non c'e' nessun layout da rispettare.</param>
    public static VersioningMode Detect(string destination, Func<string, bool>? hardLinkProbe = null)
    {
        var dest = (destination ?? "").Trim();
        if (dest.Length > 0 && Directory.Exists(dest))
        {
            if (HasDatedFolders(dest)) return VersioningMode.HardLinks;
            // Anche le sole versioni bastano: una destinazione a cui e' stata cancellata «current»
            // (o dove il primo mirror non e' mai riuscito) ha comunque un archivio per differenza
            // da rispettare, e i file che contiene sono spesso l'unica copia rimasta.
            if (Directory.Exists(CurrentDir(dest)) || HasDatedFolders(VersionsDir(dest)))
                return VersioningMode.Differential;
        }

        return (hardLinkProbe ?? HardLinkSupport.IsSupported)(dest)
            ? VersioningMode.HardLinks
            : VersioningMode.Differential;
    }

    /// <summary>true se la destinazione ha GIA' uno dei due layout: ci sono versioni da rispettare,
    /// e un mirror piatto nella radice le cancellerebbe come file extra. Non tocca il disco piu' del
    /// necessario e non scrive nulla (nessuna prova degli hard-link).</summary>
    public static bool HasLayout(string destination)
    {
        var dest = (destination ?? "").Trim();
        if (dest.Length == 0 || !Directory.Exists(dest)) return false;
        return HasDatedFolders(dest) || HasDifferentialLayout(dest);
    }

    /// <summary>true se la destinazione ha il layout PER DIFFERENZA: la cartella <c>current</c>
    /// oppure delle versioni in <c>versions</c>. Le due cose non vanno sempre insieme: un primo
    /// mirror mai riuscito, o una <c>current</c> cancellata a mano, lasciano solo le versioni — e
    /// quelle contengono i file che non esistono da nessun'altra parte.</summary>
    public static bool HasDifferentialLayout(string destination)
    {
        var dest = (destination ?? "").Trim();
        if (dest.Length == 0 || !Directory.Exists(dest)) return false;
        return Directory.Exists(CurrentDir(dest)) || HasDatedFolders(VersionsDir(dest));
    }

    /// <summary>true se la radice della destinazione contiene almeno una cartella-data del modello a
    /// hard-link. Conta anche una <c>.inprogress</c>: un primo run interrotto e' comunque un layout a
    /// hard-link iniziato, e ricominciare con l'altro modello lascerebbe il residuo li' per sempre.</summary>
    private static bool HasDatedFolders(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return false;
            return Directory.GetDirectories(dir).Select(Path.GetFileName).OfType<string>()
                .Any(n => SnapshotName.TryParse(n, out _)
                    || (SnapshotName.IsInProgress(n)
                        && SnapshotName.TryParse(n[..^SnapshotName.InProgressSuffix.Length], out _)));
        }
        catch { return false; } // destinazione sparita a meta' lettura: decide la prova sul disco
    }
}
