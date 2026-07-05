using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Cartella effettiva da verificare per un job: la destinazione per i job normali, l'ultimo
/// snapshot datato per i job versionati (le .inprogress non contano). Null = niente da
/// verificare (job versionato senza snapshot): messaggio all'utente, non errore.
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

        if (!Directory.Exists(dest))
            return null;

        var latest = Directory.GetDirectories(dest)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(n => !SnapshotName.IsInProgress(n) && SnapshotName.TryParse(n, out _))
            .OrderByDescending(n => { SnapshotName.TryParse(n, out var d); return d; })
            .FirstOrDefault();

        return latest is null ? null : Path.Combine(dest, latest);
    }
}
