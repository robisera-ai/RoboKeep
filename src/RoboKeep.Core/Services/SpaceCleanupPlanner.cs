namespace RoboKeep.Core.Services;

/// <summary>
/// Ritenzione per SPAZIO: quale versione cancellare per far posto quando il disco di backup e'
/// sotto la soglia di spazio libero. Funzione pura (lo spazio e' un parametro), pensata per essere
/// chiamata in ciclo: una versione alla volta, ricontrollando lo spazio dopo ciascuna, perche'
/// quanto si libera non si sa prima. Mai la piu' recente - quella E' il backup - e mai una
/// <c>.inprogress</c> o un nome non parsabile, esattamente come <see cref="SnapshotPlanner"/>.
/// </summary>
public static class SpaceCleanupPlanner
{
    /// <summary>Nome della prossima versione da cancellare, o null se non c'e' niente da fare:
    /// spazio libero gia' sopra la soglia, soglia spenta (&lt;= 0) oppure una sola versione valida.</summary>
    public static string? NextToDelete(IEnumerable<string> versionNames, long freeBytes, long thresholdBytes)
    {
        // Soglia a zero = controllo spento: non si cancella niente, nemmeno con il disco pieno.
        if (thresholdBytes <= 0 || freeBytes >= thresholdBytes) return null;

        var valid = versionNames
            .Where(n => !SnapshotName.IsInProgress(n) && SnapshotName.TryParse(n, out _))
            .Select(n => { SnapshotName.TryParse(n, out var d); return (Name: n, Date: d); })
            .OrderBy(v => v.Date)
            .ToList();

        // Con una sola versione ci si fermerebbe con un disco ancora pieno E nessun backup: il
        // dato che si ha vale piu' dello spazio che si libererebbe.
        return valid.Count >= 2 ? valid[0].Name : null;
    }
}
