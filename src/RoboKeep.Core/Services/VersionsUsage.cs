using System.Diagnostics;
using System.Globalization;

namespace RoboKeep.Core.Services;

/// <summary>
/// Quanto occupano le versioni di un job: la somma delle dimensioni dei file dentro le cartelle
/// datate di <c>versions\</c>. Ogni versione contiene solo i file che quel backup ha sostituito o
/// cancellato, quindi ogni byte contato e' spazio usato davvero.
/// <para>Best-effort e a tempo: oltre il budget, o al primo imprevisto, restituisce null e chi
/// scrive il messaggio dice «n/d». Si chiama solo quando serve davvero (un run fallito per spazio):
/// costa un'enumerazione di tutte le versioni.</para>
/// </summary>
public static class VersionsUsage
{
    /// <summary>Tetto di tempo: con centinaia di migliaia di file il conteggio esatto non vale
    /// un'attesa, e il messaggio all'utente non deve aspettarlo.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    /// <summary>Byte occupati dalle versioni in <paramref name="versionsDir"/> (solo le cartelle
    /// datate valide: le <c>.inprogress</c> e i residui <c>.deleting-…</c> non sono versioni e non
    /// vanno addebitati a chi decide quante tenerne); null se non si e' potuto sapere (tempo
    /// scaduto, nessuna versione, errore).</summary>
    public static long? Measure(string? versionsDir, TimeSpan? budget = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(versionsDir) || !Directory.Exists(versionsDir)) return null;
            var versions = SnapshotName.ListValid(versionsDir);
            if (versions.Count == 0) return null;

            var cap = budget ?? Budget;
            var sw = Stopwatch.StartNew();
            long total = 0;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint, // junction/symlink: non si seguono
                IgnoreInaccessible = true,
            };
            foreach (var version in versions)
                foreach (var file in new DirectoryInfo(Path.Combine(versionsDir, version)).EnumerateFiles("*", options))
                {
                    if (sw.Elapsed > cap) return null;
                    try { total += file.Length; }
                    catch { /* file sparito a meta' conteggio: si salta */ }
                }
            return total;
        }
        catch { return null; }
    }

    /// <summary>Dimensione leggibile da un umano ("1,4 GB"), oppure «n/d» quando non si sa.</summary>
    public static string Describe(long? bytes)
    {
        if (bytes is not { } b || b < 0) return CoreLoc.S("Space_Unknown");
        var units = new[] { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = b;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        // I byte interi non hanno decimali da mostrare; da KB in su una cifra basta e avanza.
        return string.Format(CultureInfo.CurrentCulture, i == 0 ? "{0:0} {1}" : "{0:0.#} {1}", v, units[i]);
    }
}
