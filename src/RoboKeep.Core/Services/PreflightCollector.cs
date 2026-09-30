using System.Diagnostics;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Raccoglie i dati IO per il pre-avvio: raggiungibilità destinazione, spazio libero
/// (anche su share UNC, via GetDiskFreeSpaceEx) e dimensione sorgente entro un budget di tempo.
/// </summary>
public static class PreflightCollector
{
    public static PreflightInputs Collect(BackupJob job, long minFreeBytes, TimeSpan sizeBudget)
    {
        var reachable = IsReachable(job.Destination);
        var free = reachable ? GetFreeBytes(job.Destination) : 0L;
        var size = reachable ? TryGetSize(job.Source, sizeBudget) : null;
        // Solo per i job VSS: la sorgente deve stare su un volume NTFS locale.
        bool? vssEligible = job.UseVss ? VssEligibility.IsEligible(job.Source) : null;
        // "Salute" di sorgente e destinazione dal registro eventi (lo SMART di un disco USB non si
        // legge senza privilegi di amministratore).
        return new PreflightInputs(reachable, free, minFreeBytes, size, vssEligible,
            DiskEventLog.Collect(job.Destination), DiskEventLog.Collect(job.Source));
    }

    private static bool IsReachable(string dest)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dest)) return false;
            var root = Path.GetPathRoot(dest);
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) return true;
            return Directory.Exists(dest);
        }
        catch { return false; }
    }

    // Spazio non determinabile: long.MaxValue, cosi' il warning sullo spazio non scatta su un dato
    // che non c'e'. Il "come si legge" vive in FreeSpaceReader, condiviso con la ritenzione per spazio.
    private static long GetFreeBytes(string dest) => FreeSpaceReader.Read(dest) ?? long.MaxValue;

    private static long? TryGetSize(string source, TimeSpan budget)
    {
        try
        {
            if (!Directory.Exists(source)) return 0;
            var sw = Stopwatch.StartNew();
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { /* file sparito: ignora */ }
                if (sw.Elapsed > budget) return null; // troppo lento: rinuncia al confronto dimensione.
            }
            return total;
        }
        catch { return null; }
    }
}
