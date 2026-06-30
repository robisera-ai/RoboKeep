using System.Diagnostics;
using System.Runtime.InteropServices;
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
        // Solo per i job versionati: la destinazione deve supportare gli hard-link, altrimenti
        // il versioning degrada a mirror semplice (stesso controllo del runtime in BackupRunner).
        bool? hardLinks = reachable && job.Versioned ? HardLinkSupport.IsSupported(job.Destination) : null;
        return new PreflightInputs(reachable, free, minFreeBytes, size, hardLinks);
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

    private static long GetFreeBytes(string dest)
    {
        try
        {
            var root = Path.GetPathRoot(dest);
            if (string.IsNullOrEmpty(root)) return long.MaxValue;
            if (GetDiskFreeSpaceEx(root, out var freeForCaller, out _, out _))
                return (long)freeForCaller;
            return long.MaxValue; // non determinabile: non far scattare il warning spazio.
        }
        catch { return long.MaxValue; }
    }

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

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailableToCaller,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);
}
