using System.Diagnostics;
using System.Text;

namespace RobocopySW.Core.Services;

/// <summary>Frequenza di una pianificazione.</summary>
public enum ScheduleFrequency { Daily, Weekly }

/// <summary>
/// Registra/rimuove attività nell'Utilità di pianificazione di Windows tramite <c>schtasks.exe</c>,
/// così da eseguire l'app in modalità silenziosa (es. <c>RobocopySW.exe --job "Nome"</c>) a orari prefissati.
/// </summary>
public sealed class SchedulerService
{
    // Nome piatto (niente sottocartella): evita richieste di permessi sul percorso.
    private const string TaskPrefix = "RobocopySW_";

    /// <summary>
    /// Crea o aggiorna un'attività pianificata che lancia <paramref name="exePath"/> con
    /// <paramref name="arguments"/> alla frequenza indicata e all'orario <paramref name="time"/>.
    /// </summary>
    public void CreateOrUpdate(string taskName, string exePath, string arguments,
        ScheduleFrequency frequency, TimeOnly time)
    {
        var tr = $"\"{exePath}\" {arguments}".Trim();
        var sc = frequency == ScheduleFrequency.Weekly ? "WEEKLY" : "DAILY";

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
        Run(args);
    }

    /// <summary>Rimuove un'attività pianificata, se presente.</summary>
    public void Delete(string taskName) =>
        Run(new List<string> { "/Delete", "/F", "/TN", TaskPrefix + taskName }, throwOnError: false);

    /// <summary>Indica se l'attività pianificata esiste.</summary>
    public bool Exists(string taskName) =>
        Run(new List<string> { "/Query", "/TN", TaskPrefix + taskName }, throwOnError: false) == 0;

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
