using RoboKeep.Core.Models;

namespace RoboKeep.Core.Services;

/// <summary>Traduce le risposte della creazione guidata in un <see cref="BackupJob"/> (funzione pura).</summary>
public static class JobWizardPlanner
{
    public static BackupJob BuildJob(JobWizardAnswers a)
    {
        ArgumentNullException.ThrowIfNull(a);

        // La rete si riconosce dal percorso, non da una domanda. Il tipo di disco non si chiede:
        // StorageProbe lo rileva a runtime e limita i thread sui dischi meccanici.
        var onNetwork = VolumeIdentity.IsNetworkPath(a.Source) || VolumeIdentity.IsNetworkPath(a.Destination);

        var job = new BackupJob
        {
            Name = (a.Name ?? "").Trim(),
            Source = (a.Source ?? "").Trim(),
            Destination = (a.Destination ?? "").Trim(),
            Mirror = a.Mirror,
            MultiThread = 8,
            Restartable = a.HasLargeFiles,
            UnbufferedIO = false,
            CopyAll = a.PreservePermissions,
            Retries = onNetwork ? 3 : 1,
            Wait = onNetwork ? 10 : 5,
            ForceCopyFiles = (a.FrozenMetadataPatterns ?? new())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList(),
            ForceCopySmart = false,
            Enabled = true,
            UseVss = a.HasOpenFiles,
            Versioned = a.KeepVersions,
            // Il wizard non offre "illimitato": un valore non positivo torna al default.
            SnapshotKeepCount = a.VersionsToKeep > 0 ? a.VersionsToKeep : BackupJob.DefaultSnapshotKeepCount,
            Schedule = a.Schedule,
            ScheduleTime = string.IsNullOrWhiteSpace(a.ScheduleTime) ? "21:00" : a.ScheduleTime.Trim(),
            ScheduleWeekDay = a.ScheduleWeekDay,
            ScheduleMonthDay = Math.Clamp(a.ScheduleMonthDay, 1, 31),
            ScheduleLastDayOfMonth = a.ScheduleLastDayOfMonth,
        };

        if (a.ExcludeCommonTemp)
        {
            job.ExcludeDirs = new List<string> { "cache", "tmp", "Temp", "node_modules" };
            job.ExcludeFiles = new List<string> { "*.tmp", "~$*" };
        }

        return job;
    }
}
