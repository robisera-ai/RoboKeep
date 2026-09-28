using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Cartella effettiva da verificare per un job: la destinazione per i job normali; per i job
/// versionati, l'ultimo snapshot datato nel modello a hard-link (le .inprogress non contano) e la
/// cartella <c>current</c> in quello per differenza — li' sta il mirror vero, mentre <c>versions</c>
/// contiene gli stati passati, che con la sorgente di oggi non coincidono per definizione.
/// Null = niente da verificare (job versionato che non ha ancora scritto nulla): messaggio
/// all'utente, non errore.
/// </summary>
public static class VerifyTargetResolver
{
    public static string? Resolve(BackupJob job)
    {
        var dest = (job.Destination ?? "").Trim();
        if (dest.Length == 0)
            return null; // coerenza col contratto: null = niente da verificare
        if (!job.Versioned)
            return dest;

        // Stessa precedenza di VersioningLayout.Detect (il layout che c'e' vince), ma senza mai
        // interrogare il disco con un file di prova: qui si decide solo dove guardare.
        var latest = SnapshotName.Latest(dest);
        if (latest is not null)
            return Path.Combine(dest, latest);

        // «current» va restituita anche se non c'e' ANCORA (primo mirror mai riuscito, cartella
        // cancellata a mano): finche' in «versions» ci sono versioni, il bersaglio del job e'
        // quello. Rispondere null manderebbe l'anteprima a confrontarsi con la radice della
        // destinazione, che elencherebbe l'intero archivio delle versioni come roba da cancellare.
        var current = VersioningLayout.CurrentDir(dest);
        return Directory.Exists(current) || VersioningLayout.HasDifferentialLayout(dest) ? current : null;
    }
}
