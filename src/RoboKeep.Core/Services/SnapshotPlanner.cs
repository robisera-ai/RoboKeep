namespace RoboKeep.Core.Services;

/// <summary>
/// Decide quali snapshot eliminare in base alla ritenzione. Funzione pura: il tempo è un
/// parametro. Ignora le cartelle .inprogress e i nomi non parsabili.
/// </summary>
public static class SnapshotPlanner
{
    public static IReadOnlyList<string> SnapshotsToDelete(
        IEnumerable<string> existingNames, int keepCount, int maxAgeDays, DateTime now)
    {
        var snaps = existingNames
            .Where(n => !SnapshotName.IsInProgress(n) && SnapshotName.TryParse(n, out _))
            .Select(n => { SnapshotName.TryParse(n, out var d); return (Name: n, Date: d); })
            .OrderByDescending(s => s.Date)
            .ToList();

        var toDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (keepCount > 0)
            foreach (var s in snaps.Skip(keepCount))
                toDelete.Add(s.Name);

        if (maxAgeDays > 0)
            foreach (var s in snaps.Where(s => (now - s.Date).TotalDays > maxAgeDays))
                toDelete.Add(s.Name);

        return toDelete.ToList();
    }
}
