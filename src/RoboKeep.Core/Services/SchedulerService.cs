using System.Diagnostics;
using System.Text;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>Frequenza di una pianificazione.</summary>
public enum ScheduleFrequency { Daily, Weekly, Once }

/// <summary>
/// Registra/rimuove attività nell'Utilità di pianificazione di Windows tramite <c>schtasks.exe</c>,
/// così da eseguire l'app in modalità silenziosa (es. <c>RoboKeep.exe --job "Nome"</c>) a orari prefissati.
/// </summary>
public sealed class SchedulerService
{
    // Nome piatto (niente sottocartella): evita richieste di permessi sul percorso.
    private const string TaskPrefix = "RoboKeep_";

    /// <summary>
    /// Crea o aggiorna un'attività pianificata che lancia <paramref name="exePath"/> con
    /// <paramref name="arguments"/> alla frequenza indicata e all'orario <paramref name="time"/>.
    /// </summary>
    public void CreateOrUpdate(string taskName, string exePath, string arguments,
        ScheduleFrequency frequency, TimeOnly time)
    {
        var tr = $"\"{exePath}\" {arguments}".Trim();
        var sc = frequency switch
        {
            ScheduleFrequency.Weekly => "WEEKLY",
            ScheduleFrequency.Once => "ONCE",
            _ => "DAILY",
        };

        // Niente /RL HIGHEST: richiederebbe privilegi di amministratore (Accesso negato).
        // L'attività gira come utente corrente, sufficiente per i backup utente.
        var args = new List<string>
        {
            "/Create", "/F",
            "/TN", TaskPrefix + taskName,
            "/TR", tr,
            "/SC", sc,
            "/ST", time.ToString("HH:mm"),
        };

        // ONCE richiede una data: oggi se l'orario non è ancora passato, altrimenti domani.
        if (frequency == ScheduleFrequency.Once)
        {
            var runDate = DateTime.Today.Add(time.ToTimeSpan());
            if (runDate <= DateTime.Now)
                runDate = runDate.AddDays(1);
            args.Add("/SD");
            args.Add(runDate.ToString("d")); // formato data della cultura di sistema (atteso da schtasks)
        }

        Run(args);
    }

    /// <summary>Rimuove un'attività pianificata, se presente.</summary>
    public void Delete(string taskName) =>
        Run(new List<string> { "/Delete", "/F", "/TN", TaskPrefix + taskName }, throwOnError: false);

    /// <summary>Indica se l'attività pianificata esiste.</summary>
    public bool Exists(string taskName) =>
        Run(new List<string> { "/Query", "/TN", TaskPrefix + taskName }, throwOnError: false) == 0;

    /// <summary>
    /// Restituisce la "prossima esecuzione" dell'attività (stringa data/ora di sistema),
    /// oppure null se l'attività non esiste.
    /// </summary>
    public string? GetNextRunTime(string taskName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in new[] { "/Query", "/TN", TaskPrefix + taskName, "/FO", "CSV", "/V" })
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi);
        if (p is null) return null;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) return null; // attività non trovata

        // CSV: riga 0 = intestazioni, riga 1 = dati. La 3ª colonna (indice 2) è "Next Run Time".
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return null;
        var fields = lines[1].Trim().Trim('"').Split("\",\"");
        if (fields.Length <= 2) return null;
        var value = fields[2].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Allinea l'attività di Windows del job: la crea/aggiorna se il job ha una pianificazione,
    /// la rimuove se non ce l'ha più. Il file XML temporaneo viene scritto in UTF-16 (come
    /// dichiara l'intestazione) e cancellato subito dopo.
    /// </summary>
    public void SyncJobTask(BackupJob job, string exePath)
    {
        var taskName = SchtasksArgs.TaskName(job.Name);
        if (job.Schedule == ScheduleKind.None)
        {
            Run(new List<string> { "/Delete", "/F", "/TN", taskName }, throwOnError: false);
            return;
        }

        var xml = SchtasksArgs.BuildTaskXml(job, exePath);
        var tempFile = Path.Combine(Path.GetTempPath(), $"robokeep-task-{Guid.NewGuid():N}.xml");
        File.WriteAllText(tempFile, xml, Encoding.Unicode);
        try
        {
            Run(new List<string> { "/Create", "/F", "/TN", taskName, "/XML", tempFile });
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>Rimuove l'attività del job (per cancellazione o rinomina). Mai un errore se assente.</summary>
    public void RemoveJobTask(string jobName) =>
        Run(new List<string> { "/Delete", "/F", "/TN", SchtasksArgs.TaskName(jobName) }, throwOnError: false);

    private static int Run(List<string> args, bool throwOnError = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Impossibile avviare schtasks.exe.");
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (throwOnError && p.ExitCode != 0)
            throw new InvalidOperationException(
                $"schtasks ha restituito {p.ExitCode}: {stderr.Trim()}");

        return p.ExitCode;
    }
}
