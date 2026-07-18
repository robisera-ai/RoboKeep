using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Classifica la salute dei job in base all'ultimo esito persistito. Funzione pura:
/// il "now" e l'insieme dei dischi a riposo sono parametri, cosi' i test sono deterministici
/// e l'evaluator non tocca Windows (l'identificazione del volume vive nella UI).
/// </summary>
public static class StaleBackupEvaluator
{
    /// <summary>Oltre questa soglia un disco a riposo non e' piu' "in attesa" ma dimenticato:
    /// l'allarme torna. Non e' configurabile — <c>StaleAfterDays</c> dipende da come lavori,
    /// questa e' la definizione di "l'ho perso di vista", uguale per chiunque.</summary>
    public const int AwaySafetyNetDays = 90;

    public static IReadOnlyList<JobHealth> Evaluate(
        IEnumerable<string> jobNames,
        IReadOnlyDictionary<string, JobLastResult> lastResults,
        int staleAfterDays,
        DateTime now,
        IReadOnlySet<string>? awayJobs = null)
    {
        var list = new List<JobHealth>();
        foreach (var name in jobNames)
        {
            list.Add(new JobHealth(name, Classify(name, lastResults, staleAfterDays, now, awayJobs)));
        }
        return list;
    }

    private static BackupHealth Classify(
        string name,
        IReadOnlyDictionary<string, JobLastResult> lastResults,
        int staleAfterDays,
        DateTime now,
        IReadOnlySet<string>? awayJobs)
    {
        // Mai eseguito o ultimo esito fallito: sono fatti reali che l'assenza del disco non
        // cancella. Un fallimento avvenuto resta un fallimento, non diventa "in attesa".
        if (!lastResults.TryGetValue(name, out var r)) return BackupHealth.NeverRun;
        if (!r.Success) return BackupHealth.Failed;

        // Soglia disattivata (0 = mai avvisare): tutto Ok, nessuna anzianita' conta.
        if (staleAfterDays <= 0) return BackupHealth.Ok;

        var ageDays = (now - r.FinishedAt).TotalDays;

        // Backup abbastanza recente: lo stato del disco e' irrilevante, e' Ok. L'attesa serve
        // solo a non trasformare in allarme cio' che sarebbe "vecchio".
        if (ageDays <= staleAfterDays) return BackupHealth.Ok;

        // Da qui in giu' sarebbe Stale. Se il disco e' collegato, lo e'.
        var away = awayJobs is not null && awayJobs.Contains(name);
        if (!away) return BackupHealth.Stale;

        // Disco a riposo: attesa neutra, finche' non si supera la rete di sicurezza. La soglia
        // lunga e' il massimo tra le due, cosi' un StaleAfterDays gia' piu' lungo di 90 non
        // rende l'attesa PIU' severa del backup normale.
        var longThreshold = Math.Max(staleAfterDays, AwaySafetyNetDays);
        return ageDays > longThreshold ? BackupHealth.Stale : BackupHealth.Waiting;
    }
}
