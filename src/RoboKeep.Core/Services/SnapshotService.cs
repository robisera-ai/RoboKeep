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
        BackupJob job, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var dest = (job.Destination ?? "").Trim();
        Directory.CreateDirectory(dest);

        var existing = Directory.GetDirectories(dest).Select(Path.GetFileName).Where(n => n is not null).Cast<string>().ToList();

        var prevName = existing
            .Where(n => !SnapshotName.IsInProgress(n) && SnapshotName.TryParse(n, out _))
            .OrderByDescending(n => { SnapshotName.TryParse(n, out var d); return d; })
            .FirstOrDefault();

        var now = DateTime.Now;
        var newName = SnapshotName.For(now);
        var curr = Path.Combine(dest, newName + SnapshotName.InProgressSuffix);

        // La .inprogress deve essere fresca: HardLinkCloner assume destinazione vuota.
        if (Directory.Exists(curr)) FileSystemDelete.DeleteDirectory(curr);
        Directory.CreateDirectory(curr);

        if (prevName is not null)
        {
            progress?.Report($"[versioning] clono lo snapshot precedente ({prevName}) via hard-link...");
            HardLinkCloner.Clone(Path.Combine(dest, prevName), curr);
            // Pre-passata: rompe l'hard-link dei file cambiati, cosi robocopy li ricrea nuovi
            // senza modificare sul posto i file ancora condivisi col precedente.
            progress?.Report("[versioning] preparo lo snapshot (rompo gli hard-link dei file cambiati)...");
            SnapshotChangedUnlinker.UnlinkChanged(job.Source, curr);
        }

        var run = await _runner.RunAsync(job, dryRun: false, progress, ct, destinationOverride: curr).ConfigureAwait(false);

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
