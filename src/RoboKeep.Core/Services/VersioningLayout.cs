namespace RoboKeep.Core.Services;

/// <summary>
/// Dove stanno le cartelle delle versioni in una destinazione: il mirror vive in <c>current\</c> e
/// ogni backup mette da parte in <c>versions\&lt;data&gt;\</c> i soli file che sostituisce o
/// cancella, con il manifest gemello accanto (<see cref="VersionManifest"/>).
/// </summary>
public static class VersioningLayout
{
    /// <summary>Cartella che contiene il mirror vero.</summary>
    public const string CurrentFolderName = "current";

    /// <summary>Cartella che contiene le versioni datate.</summary>
    public const string VersionsFolderName = "versions";

    public static string CurrentDir(string destination) => Path.Combine(destination, CurrentFolderName);

    public static string VersionsDir(string destination) => Path.Combine(destination, VersionsFolderName);

    /// <summary>true se la destinazione ha GIA' delle versioni da rispettare: la cartella
    /// <c>current</c> oppure delle versioni in <c>versions</c> (vedi <see cref="HasArchivedVersions"/>).
    /// Le due cose non vanno sempre insieme: un primo mirror mai riuscito, o una <c>current</c>
    /// cancellata a mano, lasciano solo le versioni — e quelle contengono i file che non esistono da
    /// nessun'altra parte. Un mirror piatto nella radice le cancellerebbe come file extra. Legge
    /// soltanto, non scrive nulla.</summary>
    public static bool HasVersions(string destination)
    {
        var dest = (destination ?? "").Trim();
        if (dest.Length == 0 || !Directory.Exists(dest)) return false;
        return Directory.Exists(CurrentDir(dest)) || HasArchivedVersions(dest);
    }

    /// <summary>true se <c>versions\</c> contiene almeno una cartella-data o un manifest. Conta anche
    /// una <c>.inprogress</c>: e' una versione interrotta che il run successivo promuove, e i file
    /// che contiene possono essere l'unica copia rimasta. Conta anche un manifest da solo: e' un
    /// punto nel tempo, e dice che quella cartella e' davvero di RoboKeep.</summary>
    public static bool HasArchivedVersions(string destination)
    {
        var dir = VersionsDir((destination ?? "").Trim());
        try
        {
            if (!Directory.Exists(dir)) return false;
            if (Directory.EnumerateFiles(dir, "*" + VersionManifest.FileSuffix).Any()) return true;
            return Directory.GetDirectories(dir).Select(Path.GetFileName).OfType<string>()
                .Any(n => SnapshotName.TryParse(n, out _)
                    || (SnapshotName.IsInProgress(n)
                        && SnapshotName.TryParse(n[..^SnapshotName.InProgressSuffix.Length], out _)));
        }
        catch { return false; } // destinazione sparita a meta' lettura
    }
}
