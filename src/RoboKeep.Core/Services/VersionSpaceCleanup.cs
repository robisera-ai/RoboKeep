using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Ritenzione per SPAZIO, condivisa dai due modelli di versione: fa posto sul disco di backup
/// cancellando le versioni piu' vecchie di QUESTO job, finche' lo spazio libero torna sopra la
/// soglia o resta solo la piu' recente. Attiva solo se l'utente lo ha chiesto
/// (<see cref="AppSettings.FreeSpaceCleanup"/>): cancellare per far posto e' una sua decisione.
/// Lo spazio si rilegge dopo OGNI cancellazione - quanto liberi una versione non si sa prima, con
/// gli hard-link spariscono solo i file che non condivide con le altre - e ogni cancellazione e'
/// una riga nel log. Una cancellazione che non riesce ferma il ciclo: insistere sulle altre
/// versioni non risolverebbe il motivo del rifiuto.
/// </summary>
public static class VersionSpaceCleanup
{
    /// <param name="versionsDir">Cartella che contiene le cartelle-data: la destinazione stessa nel
    /// modello a hard-link, <c>versions\</c> in quello per differenza.</param>
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
                // Su thread di background: cancellare migliaia di hard-link non deve congelare la UI.
                await Task.Run(() => FileSystemDelete.DeleteDirectory(victimDir), ct).ConfigureAwait(false);
                // Il manifest gemello segue la sua versione (modello per differenza); nel modello a
                // hard-link non esiste e la chiamata non fa nulla.
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
            // Una versione i cui file erano tutti condivisi con le altre non libera nulla: dirlo con
            // «liberati n/d» sembrerebbe un errore di misura, mentre e' proprio come funzionano gli
            // hard-link. Frase sua, che spiega anche perche' il ciclo continua con la successiva.
            progress?.Report(after is { } a && a > now
                ? string.Format(CoreLoc.S("Space_Freed"), VersionsUsage.Describe(a - now), victim)
                : string.Format(CoreLoc.S("Space_FreedNothing"), victim));
            free = after;
        }
    }
}
