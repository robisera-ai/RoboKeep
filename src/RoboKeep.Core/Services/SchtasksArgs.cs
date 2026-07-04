using System.Security;
using System.Text;
using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>
/// Genera l'XML dell'Utilità di pianificazione per l'attività per-job (funzione pura).
/// Si usa l'XML (schtasks /Create /XML) invece dei parametri /SC /D perché le abbreviazioni
/// dei giorni di /D sono localizzate (MON su Windows inglese, LUN su italiano): l'XML è
/// neutro rispetto alla lingua e sempre uguale su ogni macchina.
/// </summary>
public static class SchtasksArgs
{
    public const string Prefix = "RoboKeep - ";

    /// <summary>Nome dell'attività per il job, con i caratteri non validi sostituiti da '_'.</summary>
    public static string TaskName(string jobName)
    {
        var invalid = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        var safe = new string((jobName ?? "").Select(c => invalid.Contains(c) ? '_' : c).ToArray())
            .Trim().TrimEnd('.'); // niente punto finale: i nomi attività sono segmenti di percorso
        return Prefix + (string.IsNullOrEmpty(safe) ? "job" : safe);
    }

    /// <summary>XML completo dell'attività: trigger secondo la frequenza del job, azione --job.</summary>
    public static string BuildTaskXml(BackupJob job, string exePath)
    {
        if (!TimeOnly.TryParse(job.ScheduleTime, out var time))
            time = new TimeOnly(21, 0);
        // La data di StartBoundary è solo l'ancora del trigger: una data fissa nel passato va bene.
        var boundary = $"2026-01-01T{time:HH:mm}:00";
        var trigger = job.Schedule switch
        {
            ScheduleKind.Weekly => $"""
      <ScheduleByWeek>
        <DaysOfWeek>
          <{job.ScheduleWeekDay} />
        </DaysOfWeek>
        <WeeksInterval>1</WeeksInterval>
      </ScheduleByWeek>
""",
            ScheduleKind.Monthly => $"""
      <ScheduleByMonth>
        <DaysOfMonth>
          <Day>{Math.Clamp(job.ScheduleMonthDay, 1, 31)}</Day>
        </DaysOfMonth>
        <Months>
          <January /><February /><March /><April /><May /><June />
          <July /><August /><September /><October /><November /><December />
        </Months>
      </ScheduleByMonth>
""",
            _ => """
      <ScheduleByDay>
        <DaysInterval>1</DaysInterval>
      </ScheduleByDay>
""",
        };

        var arguments = SecurityElement.Escape($"--job \"{job.Name}\"");
        var command = SecurityElement.Escape(exePath);

        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-16"?>""");
        sb.AppendLine("""<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">""");
        sb.AppendLine("  <Triggers>");
        sb.AppendLine("    <CalendarTrigger>");
        sb.AppendLine($"      <StartBoundary>{boundary}</StartBoundary>");
        sb.AppendLine("      <Enabled>true</Enabled>");
        sb.AppendLine(trigger.TrimEnd());
        sb.AppendLine("    </CalendarTrigger>");
        sb.AppendLine("  </Triggers>");
        sb.AppendLine("  <Principals>");
        sb.AppendLine("""    <Principal id="Author">""");
        sb.AppendLine("      <LogonType>InteractiveToken</LogonType>");
        sb.AppendLine("      <RunLevel>LeastPrivilege</RunLevel>");
        sb.AppendLine("    </Principal>");
        sb.AppendLine("  </Principals>");
        sb.AppendLine("  <Settings>");
        sb.AppendLine("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
        sb.AppendLine("    <StartWhenAvailable>true</StartWhenAvailable>");
        sb.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
        sb.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
        sb.AppendLine("    <ExecutionTimeLimit>PT12H</ExecutionTimeLimit>");
        sb.AppendLine("  </Settings>");
        sb.AppendLine("""  <Actions Context="Author">""");
        sb.AppendLine("    <Exec>");
        sb.AppendLine($"      <Command>{command}</Command>");
        sb.AppendLine($"      <Arguments>{arguments}</Arguments>");
        sb.AppendLine("    </Exec>");
        sb.AppendLine("  </Actions>");
        sb.AppendLine("</Task>");
        return sb.ToString();
    }
}
