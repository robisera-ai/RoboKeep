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
        if (Directory.Exists(curr)) Directory.Delete(curr, recursive: true);
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
                progress?.Report($"[versioning] snapshot creato: {Path.GetFileName(final)}");

                var after = Directory.GetDirectories(dest).Select(Path.GetFileName).OfType<string>();
                foreach (var name in SnapshotPlanner.SnapshotsToDelete(after, job.SnapshotKeepCount, job.SnapshotMaxAgeDays, now))
                {
                    try
                    {
                        Directory.Delete(Path.Combine(dest, name), recursive: true);
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
}
