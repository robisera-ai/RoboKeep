using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Classifica la salute dei job in base all'ultimo esito persistito. Funzione pura:
/// il "now" è un parametro, così i test sono deterministici.
/// </summary>
public static class StaleBackupEvaluator
{
    public static IReadOnlyList<JobHealth> Evaluate(
        IEnumerable<string> jobNames,
        IReadOnlyDictionary<string, JobLastResult> lastResults,
        int staleAfterDays,
        DateTime now)
    {
        var list = new List<JobHealth>();
        foreach (var name in jobNames)
        {
            BackupHealth health;
            if (!lastResults.TryGetValue(name, out var r))
                health = BackupHealth.NeverRun;
            else if (!r.Success)
                health = BackupHealth.Failed;
            else if (staleAfterDays > 0 && (now - r.FinishedAt).TotalDays > staleAfterDays)
                health = BackupHealth.Stale;
            else
                health = BackupHealth.Ok;

            list.Add(new JobHealth(name, health));
        }
        return list;
    }
}
