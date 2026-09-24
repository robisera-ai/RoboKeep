using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>A che cosa e' legata un'attivita' pianificata di RoboKeep.</summary>
public enum TaskLink
{
    /// <summary>Corrisponde a un job presente in configurazione.</summary>
    Job,
    /// <summary>E' l'attivita' di «Avvia tutti» creata dalle Impostazioni.</summary>
    RunAll,
    /// <summary>Nessun job con quel nome: l'attivita' e' rimasta indietro.</summary>
    OrphanNoJob,
    /// <summary>Il nome torna, ma il comando lancia un'altra copia di RoboKeep.</summary>
    OrphanOtherExe,
}

/// <summary>
/// Una attivita' dell'Utilita' di pianificazione, letta cosi' com'e'. Date e stato restano
/// valori grezzi (niente testo gia' formattato): la traduzione e il formato della data
/// appartengono alla UI, che sa quale lingua sta parlando.
/// </summary>
/// <param name="Name">Nome dell'attivita' nella cartella radice.</param>
/// <param name="NextRun">Prossima esecuzione, null se non ce n'e' una.</param>
/// <param name="LastRun">Ultima esecuzione, null se non ha mai girato.</param>
/// <param name="LastResult">Esito dell'ultima esecuzione (0 = ok, 267011 = mai eseguita).</param>
/// <param name="State">Stato COM: 1 disattivata, 2 in coda, 3 pronta, 4 in esecuzione.</param>
/// <param name="Command">Programma lanciato dall'attivita'.</param>
/// <param name="Arguments">Argomenti passati al programma.</param>
public sealed record ScheduledTaskInfo(
    string Name,
    DateTime? NextRun,
    DateTime? LastRun,
    int? LastResult,
    int State,
    string Command,
    string Arguments)
{
    /// <summary>Codice che l'Utilita' di pianificazione usa per «l'attivita' non ha mai girato».</summary>
    public const int NeverRunResult = 267011;

    /// <summary>Nome dell'attivita' globale «Avvia tutti», creata dalle Impostazioni.</summary>
    public const string RunAllTaskName = "RoboKeep_AvviaTutti";

    /// <summary>Prefisso piatto delle attivita' globali (niente sottocartelle).</summary>
    public const string GlobalPrefix = "RoboKeep_";

    /// <summary>true per le attivita' create da RoboKeep: le altre non si mostrano e non si toccano.</summary>
    public static bool IsRoboKeep(string? name) =>
        !string.IsNullOrEmpty(name) &&
        (name.StartsWith(SchtasksArgs.Prefix, StringComparison.OrdinalIgnoreCase) ||
         name.StartsWith(GlobalPrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Nome del job scritto nell'attivita' (la parte dopo «RoboKeep - »), null per le attivita'
    /// globali come «Avvia tutti». E' il nome sanificato: va confrontato con
    /// <see cref="SchtasksArgs.TaskName"/>, mai con il nome del job tale e quale.
    /// </summary>
    public static string? JobNameOf(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!name.StartsWith(SchtasksArgs.Prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = name[SchtasksArgs.Prefix.Length..];
        return rest.Length == 0 ? null : rest;
    }

    /// <summary>
    /// Nome del job che ha generato l'attivita', preso dalla configurazione, oppure null se
    /// non c'e'. E' il nome vero, quello che l'utente ha scritto: il suffisso dell'attivita'
    /// e' sanificato e un job «Foto/2026» vi comparirebbe come «Foto_2026».
    /// </summary>
    /// <param name="task">Attivita' da abbinare.</param>
    /// <param name="jobNames">Nomi dei job in configurazione (non sanificati).</param>
    public static string? MatchingJobName(ScheduledTaskInfo task, IEnumerable<string> jobNames) =>
        jobNames.FirstOrDefault(n =>
            string.Equals(SchtasksArgs.TaskName(n), task.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Il rovescio dell'orfana: job che in configurazione hanno una pianificazione ma in
    /// Windows non hanno piu' la loro attivita' (cancellata dall'Utilita' di pianificazione o
    /// da un'altra copia di RoboKeep). L'editor direbbe «giornaliera» e nulla partirebbe.
    /// </summary>
    /// <param name="jobs">Job in configurazione.</param>
    /// <param name="tasks">Attivita' di RoboKeep presenti in Windows.</param>
    /// <returns>Nomi dei job (come in configurazione) senza attivita', nell'ordine dei job.</returns>
    public static IReadOnlyList<string> JobsWithoutTask(IEnumerable<BackupJob> jobs, IEnumerable<ScheduledTaskInfo> tasks)
    {
        var names = tasks.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return jobs.Where(j => j.Schedule != ScheduleKind.None && !names.Contains(SchtasksArgs.TaskName(j.Name)))
            .Select(j => j.Name).ToList();
    }

    /// <summary>
    /// Stabilisce se l'attivita' ha ancora un padrone. Prima il nome: nessun job che generi
    /// quel nome = orfana. Poi il comando: se punta a un exe diverso da quello in esecuzione
    /// l'attivita' e' di un'altra copia di RoboKeep e non fa quello che l'utente crede.
    /// </summary>
    /// <param name="task">Attivita' da classificare.</param>
    /// <param name="jobNames">Nomi dei job in configurazione (non sanificati).</param>
    /// <param name="currentExe">Percorso dell'eseguibile in corso; vuoto = salta il confronto.</param>
    public static TaskLink Classify(ScheduledTaskInfo task, IEnumerable<string> jobNames, string? currentExe)
    {
        var isRunAll = string.Equals(task.Name, RunAllTaskName, StringComparison.OrdinalIgnoreCase);
        if (!isRunAll && MatchingJobName(task, jobNames) is null)
            return TaskLink.OrphanNoJob;

        if (!SameExecutable(task.Command, currentExe)) return TaskLink.OrphanOtherExe;
        return isRunAll ? TaskLink.RunAll : TaskLink.Job;
    }

    /// <summary>
    /// true se il comando registrato e' proprio l'eseguibile in corso. Senza un percorso da
    /// confrontare (comando vuoto, exe sconosciuto) si risponde true: meglio non gridare
    /// all'orfana che accusare un'attivita' sana.
    /// </summary>
    private static bool SameExecutable(string? command, string? currentExe)
    {
        if (string.IsNullOrWhiteSpace(currentExe)) return true;
        if (string.IsNullOrWhiteSpace(command)) return true;
        try
        {
            var a = Path.GetFullPath(command.Trim().Trim('"'));
            var b = Path.GetFullPath(currentExe.Trim().Trim('"'));
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Comando non interpretabile come percorso: non e' l'exe in corso.
            return false;
        }
    }
}
