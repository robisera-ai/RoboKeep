using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>Traduce le risposte della creazione guidata in un <see cref="BackupJob"/> (funzione pura).</summary>
public static class JobWizardPlanner
{
    /// <summary>Thread /MT consigliati = minimo tra sorgente e destinazione
    /// (HDD=2, USB=4, Rete=8, SSD=16): un HDD coinvolto abbassa sempre il parallelismo.</summary>
    public static int RecommendedThreads(StorageKind source, StorageKind dest) =>
        Math.Min(Rank(source), Rank(dest));

    private static int Rank(StorageKind kind) => kind switch
    {
        StorageKind.Hdd => 2,
        StorageKind.Usb => 4,
        StorageKind.Network => 8,
        StorageKind.Ssd => 16,
        _ => 8,
    };

    public static BackupJob BuildJob(JobWizardAnswers a)
    {
        ArgumentNullException.ThrowIfNull(a);

        var onNetwork = a.SourceStorage == StorageKind.Network || a.DestStorage == StorageKind.Network;

        var job = new BackupJob
        {
            Name = (a.Name ?? "").Trim(),
            Source = (a.Source ?? "").Trim(),
            Destination = (a.Destination ?? "").Trim(),
            Mirror = a.Mirror,
            MultiThread = RecommendedThreads(a.SourceStorage, a.DestStorage),
            Restartable = a.HasLargeFiles,
            UnbufferedIO = false,
            CopyAll = a.PreservePermissions,
            Retries = onNetwork ? 3 : 1,
            Wait = onNetwork ? 10 : 5,
            ForceCopyFiles = (a.FrozenMetadataPatterns ?? new())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList(),
            ForceCopySmart = false,
            Enabled = true,
        };

        if (a.ExcludeCommonTemp)
        {
            job.ExcludeDirs = new List<string> { "cache", "tmp", "Temp", "node_modules" };
            job.ExcludeFiles = new List<string> { "*.tmp", "~$*" };
        }

        return job;
    }
}
