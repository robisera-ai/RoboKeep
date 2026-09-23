using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class JobWizardPlannerTests
{
    private static JobWizardAnswers Base() => new()
    {
        Name = "J",
        Source = @"C:\src",
        Destination = @"D:\dst",
    };

    [Fact]
    public void Threads_AreEightAndLeftToTheRuntimeCap()
    {
        // Il tipo di disco non si chiede piu': lo rileva StorageProbe a runtime e limita a 2 se
        // serve. Il wizard mette il valore buono per SSD/rete.
        var job = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d" });
        Assert.Equal(8, job.MultiThread);
    }

    [Fact]
    public void NetworkPath_GetsMoreRetries_DetectedFromThePath()
    {
        var local = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d" });
        var unc = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"\\nas\share\d" });
        Assert.Equal((1, 5), (local.Retries, local.Wait));
        Assert.Equal((3, 10), (unc.Retries, unc.Wait));
    }

    [Fact]
    public void KeepVersions_TurnsOnVersioningWithDefaultRetention()
    {
        var job = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d", KeepVersions = true });
        Assert.True(job.Versioned);
        Assert.Equal(BackupJob.DefaultSnapshotKeepCount, job.SnapshotKeepCount); // lo stesso default dell'editor
        Assert.False(JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d" }).Versioned);
    }

    [Fact]
    public void KeepVersions_UsesTheChosenCount()
    {
        var job = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d", KeepVersions = true, VersionsToKeep = 7 });
        Assert.Equal(7, job.SnapshotKeepCount);
        // Un valore assurdo (0 o negativo) non produce una ritenzione illimitata: torna il default.
        var bad = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d", KeepVersions = true, VersionsToKeep = 0 });
        Assert.Equal(BackupJob.DefaultSnapshotKeepCount, bad.SnapshotKeepCount);
    }

    [Fact]
    public void Schedule_IsAppliedWithFrequencyDayAndTime()
    {
        var daily = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d", Schedule = ScheduleKind.Daily, ScheduleTime = "22:30" });
        Assert.Equal(ScheduleKind.Daily, daily.Schedule);
        Assert.Equal("22:30", daily.ScheduleTime);

        var weekly = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d", Schedule = ScheduleKind.Weekly, ScheduleWeekDay = DayOfWeek.Friday });
        Assert.Equal(ScheduleKind.Weekly, weekly.Schedule);
        Assert.Equal(DayOfWeek.Friday, weekly.ScheduleWeekDay);

        var monthly = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d", Schedule = ScheduleKind.Monthly, ScheduleMonthDay = 15 });
        Assert.Equal(ScheduleKind.Monthly, monthly.Schedule);
        Assert.Equal(15, monthly.ScheduleMonthDay);
        Assert.False(monthly.ScheduleLastDayOfMonth);

        var last = JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d", Schedule = ScheduleKind.Monthly, ScheduleLastDayOfMonth = true });
        Assert.True(last.ScheduleLastDayOfMonth);

        Assert.Equal(ScheduleKind.None, JobWizardPlanner.BuildJob(new JobWizardAnswers { Name = "j", Source = @"C:\s", Destination = @"E:\d" }).Schedule);
    }

    [Fact]
    public void BuildJob_CopiesNameSourceDestination_Trimmed()
    {
        var a = Base();
        a.Name = "  Documenti  ";
        a.Source = @"  C:\s  ";
        a.Destination = @"  D:\d  ";
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Equal("Documenti", job.Name);
        Assert.Equal(@"C:\s", job.Source);
        Assert.Equal(@"D:\d", job.Destination);
    }

    [Fact]
    public void BuildJob_Mirror_MapsToMirrorFlag()
    {
        var a = Base(); a.Mirror = true;
        Assert.True(JobWizardPlanner.BuildJob(a).Mirror);
        a.Mirror = false;
        Assert.False(JobWizardPlanner.BuildJob(a).Mirror);
    }

    [Fact]
    public void BuildJob_LargeFiles_EnablesRestartable_NotUnbuffered()
    {
        var a = Base(); a.HasLargeFiles = true;
        var job = JobWizardPlanner.BuildJob(a);
        Assert.True(job.Restartable);
        Assert.False(job.UnbufferedIO);
    }

    [Fact]
    public void BuildJob_NoLargeFiles_NoRestartableNoUnbuffered()
    {
        var job = JobWizardPlanner.BuildJob(Base());
        Assert.False(job.Restartable);
        Assert.False(job.UnbufferedIO);
    }

    [Fact]
    public void BuildJob_Permissions_MapsToCopyAll()
    {
        var a = Base(); a.PreservePermissions = true;
        Assert.True(JobWizardPlanner.BuildJob(a).CopyAll);
        a.PreservePermissions = false;
        Assert.False(JobWizardPlanner.BuildJob(a).CopyAll);
    }

    [Fact]
    public void BuildJob_Network_RaisesRetriesAndWait()
    {
        var a = Base(); a.Destination = @"\\nas\share\dst";
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Equal(3, job.Retries);
        Assert.Equal(10, job.Wait);

        var local = JobWizardPlanner.BuildJob(Base());
        Assert.Equal(1, local.Retries);
        Assert.Equal(5, local.Wait);
    }

    [Fact]
    public void BuildJob_FrozenPatterns_FillForceCopy_SmartStaysOff()
    {
        var a = Base();
        a.FrozenMetadataPatterns = new() { "*.vc", "  ", "db.dat" };
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Equal(new[] { "*.vc", "db.dat" }, job.ForceCopyFiles);
        Assert.False(job.ForceCopySmart);
    }

    [Fact]
    public void BuildJob_ExcludeCommonTemp_FillsExcludeLists()
    {
        var a = Base(); a.ExcludeCommonTemp = true;
        var job = JobWizardPlanner.BuildJob(a);
        Assert.Contains("cache", job.ExcludeDirs);
        Assert.Contains("node_modules", job.ExcludeDirs);
        Assert.Contains("*.tmp", job.ExcludeFiles);

        var none = JobWizardPlanner.BuildJob(Base());
        Assert.Empty(none.ExcludeDirs);
        Assert.Empty(none.ExcludeFiles);
    }

    [Fact]
    public void BuildJob_HasOpenFiles_SetsUseVss()
    {
        var job = JobWizardPlanner.BuildJob(new JobWizardAnswers
        {
            Name = "n", Source = @"C:\s", Destination = @"E:\d", HasOpenFiles = true,
        });
        Assert.True(job.UseVss);
    }

    [Fact]
    public void BuildJob_NoOpenFiles_UseVssFalse()
    {
        var job = JobWizardPlanner.BuildJob(new JobWizardAnswers
        {
            Name = "n", Source = @"C:\s", Destination = @"E:\d",
        });
        Assert.False(job.UseVss);
    }
}
