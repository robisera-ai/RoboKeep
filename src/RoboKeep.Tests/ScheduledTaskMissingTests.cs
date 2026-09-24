using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class ScheduledTaskMissingTests
{
    private static ScheduledTaskInfo Task(string name) =>
        new(name, null, null, null, 3, @"D:\RoboKeep\RoboKeep.exe", "");

    [Fact]
    public void JobsWithoutTask_ReportsScheduledJobsWhoseTaskIsGone()
    {
        var jobs = new[]
        {
            new BackupJob { Name = "Foto", Schedule = ScheduleKind.Daily },
            new BackupJob { Name = "Documenti", Schedule = ScheduleKind.Weekly },
            new BackupJob { Name = "Desktop", Schedule = ScheduleKind.None },
        };
        var tasks = new[] { Task("RoboKeep - Foto"), Task(ScheduledTaskInfo.RunAllTaskName) };

        var missing = ScheduledTaskInfo.JobsWithoutTask(jobs, tasks);

        Assert.Equal(new[] { "Documenti" }, missing); // Desktop non e' pianificato: non manca nulla
    }

    [Fact]
    public void JobsWithoutTask_MatchesSanitizedNamesCaseInsensitively()
    {
        var jobs = new[] { new BackupJob { Name = "Foto/2026", Schedule = ScheduleKind.Monthly } };
        var tasks = new[] { Task("roboKEEP - foto_2026") };

        Assert.Empty(ScheduledTaskInfo.JobsWithoutTask(jobs, tasks));
    }

    [Fact]
    public void JobsWithoutTask_NoTasksAtAll_ListsEveryScheduledJob()
    {
        var jobs = new[]
        {
            new BackupJob { Name = "A", Schedule = ScheduleKind.Daily },
            new BackupJob { Name = "B", Schedule = ScheduleKind.Daily },
        };

        Assert.Equal(new[] { "A", "B" }, ScheduledTaskInfo.JobsWithoutTask(jobs, Array.Empty<ScheduledTaskInfo>()));
    }
}
