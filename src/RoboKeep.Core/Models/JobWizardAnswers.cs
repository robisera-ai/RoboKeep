namespace RoboKeep.Core.Models;

/// <summary>Risposte della creazione guidata di un job: input puro per <see cref="Services.JobWizardPlanner"/>.</summary>
public sealed class JobWizardAnswers
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public bool Mirror { get; set; } = true;
    public bool HasLargeFiles { get; set; }
    public List<string> FrozenMetadataPatterns { get; set; } = new();
    public bool PreservePermissions { get; set; }
    public bool ExcludeCommonTemp { get; set; }
    public bool HasOpenFiles { get; set; }

    /// <summary>Tenere le versioni datate.</summary>
    public bool KeepVersions { get; set; }

    /// <summary>Quante versioni conservare. Vale solo con <see cref="KeepVersions"/>. Stesso
    /// default dell'editor: un solo numero da ricordare.</summary>
    public int VersionsToKeep { get; set; } = BackupJob.DefaultSnapshotKeepCount;

    /// <summary>Pianificazione: nessuna, giornaliera, settimanale o mensile (come nell'editor).</summary>
    public ScheduleKind Schedule { get; set; } = ScheduleKind.None;

    public string ScheduleTime { get; set; } = "21:00";

    /// <summary>Giorno della settimana (solo settimanale).</summary>
    public DayOfWeek ScheduleWeekDay { get; set; } = DayOfWeek.Monday;

    /// <summary>Giorno del mese 1-31 (solo mensile).</summary>
    public int ScheduleMonthDay { get; set; } = 1;

    /// <summary>Mensile: l'ultimo giorno del mese invece di un giorno fisso.</summary>
    public bool ScheduleLastDayOfMonth { get; set; }
}
