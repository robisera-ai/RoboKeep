using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Esegue un job versionato producendo uno snapshot datato con hard-link:
/// clona lo snapshot precedente, rompe gli hard-link dei file cambiati, robocopy nella cartella
/// .inprogress, rinomina a esito riuscito, applica la ritenzione. Assume destinazione idonea agli
/// hard-link (verificata a monte).
/// </summary>
public sealed class SnapshotService
{
    private readonly RobocopyRunner _runner;

    public SnapshotService(RobocopyRunner runner) => _runner = runner;

    public async Task<RobocopyRunResult> RunVersionedAsync(
        BackupJob job, IProgress<string>? progress = null, CancellationToken ct = default,
        string? sourceOverride = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        var dest = (job.Destination ?? "").Trim();
        Directory.CreateDirectory(dest);

        var prevName = SnapshotName.Latest(dest);

        var now = DateTime.Now;
        var newName = SnapshotName.For(now);
        var curr = Path.Combine(dest, newName + SnapshotName.InProgressSuffix);

        // Ripulisci OGNI .inprogress residua, non solo quella con lo stesso nome: un run precedente
        // interrotto (crash, chiusura dell'app, caduta di corrente) lascia una .inprogress con un
        // timestamp diverso che i run successivi non toccherebbero mai (la ritenzione ignora le
        // .inprogress), accumulandole all'infinito. Best-effort: un residuo bloccato non deve far
        // fallire il job. Uso la cancellazione POSIX-safe per non intaccare il read-only degli inode
        // ancora condivisi con lo snapshot precedente.
        foreach (var stale in Directory.GetDirectories(dest)
                     .Where(d => SnapshotName.IsInProgress(Path.GetFileName(d) ?? "")))
        {
            try
            {
                // Su thread di background: cancellare migliaia di hard-link non deve congelare la UI.
                await Task.Run(() => FileSystemDelete.DeleteDirectory(stale), ct).ConfigureAwait(false);
                progress?.Report($"[versioning] rimosso snapshot incompleto di un run interrotto: {Path.GetFileName(stale)}");
            }
            catch (Exception ex)
            {
                progress?.Report($"[versioning] residuo {Path.GetFileName(stale)} non rimosso: {ex.Message}");
            }
        }
        Directory.CreateDirectory(curr);

        if (prevName is not null)
        {
            // Clonazione e rottura-hard-link sono lavoro IO pesante e sincrono: su thread di background,
            // altrimenti su cartelle grandi (migliaia di file) la finestra si congela.
            var prevPath = Path.Combine(dest, prevName);
            // Il confronto per l'unlink va fatto contro l'origine effettivamente copiata
            // (lo snapshot VSS quando presente), non contro job.Source: origini diverse
            // tra unlink e robocopy romperebbero la garanzia di sovrainsieme sicuro.
            var source = sourceOverride ?? job.Source;
            progress?.Report($"[versioning] clono lo snapshot precedente ({prevName}) via hard-link...");
            // Un file illeggibile nel vecchio snapshot (settore danneggiato) non deve far fallire
            // il backup: viene saltato qui e ricopiato fresco da robocopy poco dopo.
            var skipped = await Task.Run(() => HardLinkCloner.Clone(prevPath, curr,
                (path, badSector) => progress?.Report(string.Format(
                    CoreLoc.S(badSector ? "Versioning_SkipBadSector" : "Versioning_SkipFile"), path))),
                ct).ConfigureAwait(false);
            if (skipped > 0)
                progress?.Report(string.Format(CoreLoc.S("Versioning_SkipSummary"), skipped));
            // Pre-passata: rompe l'hard-link dei file cambiati, cosi robocopy li ricrea nuovi
            // senza modificare sul posto i file ancora condivisi col precedente.
            progress?.Report("[versioning] preparo lo snapshot (rompo gli hard-link dei file cambiati)...");
            await Task.Run(() => SnapshotChangedUnlinker.UnlinkChanged(source, curr), ct).ConfigureAwait(false);
        }

        var run = await _runner.RunAsync(job, dryRun: false, progress, ct, destinationOverride: curr,
            sourceOverride: sourceOverride).ConfigureAwait(false);

        if (run.Result.Success)
        {
            var final = Path.Combine(dest, newName);
            if (Directory.Exists(final))
                final = Path.Combine(dest, newName + "_" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Directory.Move(curr, final);
                ClearReadOnly(final); // evita che Esplora mostri la cartella-data col nome di un desktop.ini interno
                progress?.Report($"[versioning] snapshot creato: {Path.GetFileName(final)}");

                var after = Directory.GetDirectories(dest).Select(Path.GetFileName).OfType<string>();
                foreach (var name in SnapshotPlanner.SnapshotsToDelete(after, job.SnapshotKeepCount, job.SnapshotMaxAgeDays, now))
                {
                    try
                    {
                        FileSystemDelete.DeleteDirectory(Path.Combine(dest, name));
                        progress?.Report($"[versioning] rimosso snapshot vecchio: {name}");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"[versioning] impossibile rimuovere {name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"[versioning] backup riuscito ma rinomina snapshot fallita ({ex.Message}); resta {Path.GetFileName(curr)}.");
            }
        }
        else
        {
            progress?.Report($"[versioning] backup non riuscito: snapshot incompleto lasciato come {Path.GetFileName(curr)}.");
        }

        return run;
    }

    // Toglie l'attributo sola-lettura dalla cartella-snapshot. robocopy copia gli attributi della
    // cartella sorgente: se la sorgente e' una cartella "speciale" (es. Desktop) read-only con un
    // desktop.ini, Esplora risorse mostrerebbe la cartella-data col nome/icona del desktop.ini invece
    // del timestamp. Togliendo il read-only sulla cartella-data Esplora ne mostra il nome reale.
    // Il desktop.ini resta tra i file dello snapshot, intatto. Best-effort.
    private static void ClearReadOnly(string dir)
    {
        try
        {
            var di = new DirectoryInfo(dir);
            if ((di.Attributes & FileAttributes.ReadOnly) != 0)
                di.Attributes &= ~FileAttributes.ReadOnly;
        }
        catch { /* best-effort: un attributo non deve far fallire il backup */ }
    }
}
