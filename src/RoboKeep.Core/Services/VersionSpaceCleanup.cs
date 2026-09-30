using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Ritenzione per SPAZIO: fa posto sul disco di backup cancellando le versioni piu' vecchie di
/// QUESTO job, finche' lo spazio libero torna sopra la soglia o resta solo la piu' recente. Attiva
/// solo se l'utente lo ha chiesto (<see cref="AppSettings.FreeSpaceCleanup"/>): cancellare per far
/// posto e' una sua decisione. Lo spazio si rilegge dopo OGNI cancellazione, e ogni cancellazione
/// e' una riga nel log. Una cancellazione che non riesce ferma il ciclo: insistere sulle altre
/// versioni non risolverebbe il motivo del rifiuto.
/// </summary>
public static class VersionSpaceCleanup
{
    /// <param name="versionsDir">Cartella che contiene le cartelle-data: <c>versions\</c>.</param>
    /// <param name="settings">null o pulizia spenta = non si cancella niente.</param>
    /// <param name="freeSpace">Lettore dello spazio libero; null dal lettore = non si sa, e la
    /// pulizia si salta (non si cancella al buio).</param>
    public static async Task FreeUpSpaceAsync(string versionsDir, AppSettings? settings,
        Func<string, long?> freeSpace, IProgress<string>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(freeSpace);
        if (settings is not { FreeSpaceCleanup: true }) return;
        var threshold = (long)settings.MinFreeSpaceMb * 1024 * 1024;
        if (threshold <= 0) return;
        if (!Directory.Exists(versionsDir)) return;

        // Spazio non determinabile (share strana, disco appena scomparso): non si cancella niente
        // al buio. Il run prosegue e sara' robocopy a dire come e' andata.
        var free = freeSpace(versionsDir);
        while (free is { } now && now < threshold)
        {
            var names = Directory.GetDirectories(versionsDir).Select(Path.GetFileName).OfType<string>();
            if (SpaceCleanupPlanner.NextToDelete(names, now, threshold) is not { } victim) break;

            var victimDir = Path.Combine(versionsDir, victim);
            try
            {
                // Su thread di background: cancellare migliaia di file non deve congelare la UI.
                await Task.Run(() => FileSystemDelete.DeleteDirectory(victimDir), ct).ConfigureAwait(false);
                // Il manifest gemello segue la sua versione.
                VersionManifest.DeleteFor(victimDir);
            }
            // L'annullamento dell'utente ferma il job: va riproposto a chi chiama, non raccontato
            // come "cancellazione non riuscita".
            catch (OperationCanceledException) { throw; }
            // L'errore hardware non e' un "non ce l'ho fatta": interrompe il job, come altrove.
            catch (Exception ex) when (!DiskError.IsUnreadable(ex))
            {
                progress?.Report(string.Format(CoreLoc.S("Space_NotFreed"), victim, ex.Message));
                return;
            }

            var after = freeSpace(versionsDir);
            // Spazio non rileggibile dopo la cancellazione: si dice «n/d», non si inventa.
            progress?.Report(string.Format(CoreLoc.S("Space_Freed"),
                VersionsUsage.Describe(after is { } a && a >= now ? a - now : null), victim));
            free = after;
        }
    }
}
