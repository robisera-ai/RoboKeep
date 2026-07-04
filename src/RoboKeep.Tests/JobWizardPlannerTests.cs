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

    [Theory]
    [InlineData(StorageKind.Ssd, StorageKind.Ssd, 16)]
    [InlineData(StorageKind.Ssd, StorageKind.Hdd, 2)]
    [InlineData(StorageKind.Ssd, StorageKind.Usb, 4)]
    [InlineData(StorageKind.Ssd, StorageKind.Network, 8)]
    [InlineData(StorageKind.Network, StorageKind.Network, 8)]
    [InlineData(StorageKind.Hdd, StorageKind.Network, 2)]
    public void RecommendedThreads_TakesMinOfBothEnds(StorageKind src, StorageKind dst, int expected)
    {
        Assert.Equal(expected, JobWizardPlanner.RecommendedThreads(src, dst));
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
        var a = Base(); a.DestStorage = StorageKind.Network;
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
