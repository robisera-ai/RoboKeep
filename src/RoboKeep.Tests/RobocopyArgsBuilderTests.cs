using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class RobocopyArgsBuilderTests
{
    private static BackupJob NewJob() => new()
    {
        Name = "Test",
        Source = @"C:\src",
        Destination = @"D:\dst",
        Mirror = true,
        MultiThread = 8,
        Retries = 1,
        Wait = 5,
    };

    [Fact]
    public void SourceAndDestination_AreFirstTwoArguments()
    {
        var args = RobocopyArgsBuilder.Build(NewJob());
        Assert.Equal(@"C:\src", args[0]);
        Assert.Equal(@"D:\dst", args[1]);
    }

    [Fact]
    public void Mirror_True_AddsMir_NotE()
    {
        var args = RobocopyArgsBuilder.Build(NewJob());
        Assert.Contains("/MIR", args);
        Assert.DoesNotContain("/E", args);
    }

    [Fact]
    public void Mirror_False_AddsE_NotMir()
    {
        var job = NewJob();
        job.Mirror = false;
        var args = RobocopyArgsBuilder.Build(job);
        Assert.Contains("/E", args);
        Assert.DoesNotContain("/MIR", args);
    }

    [Fact]
    public void ExcludeOlder_AddsXo_WhenSet()
    {
        var job = NewJob();
        job.ExcludeOlder = true;
        Assert.Contains("/XO", RobocopyArgsBuilder.Build(job));
    }

    [Fact]
    public void ExcludeOlder_NotAdded_ByDefault()
    {
        Assert.DoesNotContain("/XO", RobocopyArgsBuilder.Build(NewJob()));
    }

    [Fact]
    public void CopyFlags_DefaultIsCopyDat_CopyAllWhenSet()
    {
        Assert.Contains("/COPY:DAT", RobocopyArgsBuilder.Build(NewJob()));

        var job = NewJob();
        job.CopyAll = true;
        var args = RobocopyArgsBuilder.Build(job);
        Assert.Contains("/COPYALL", args);
        Assert.DoesNotContain("/COPY:DAT", args);
    }

    [Fact]
    public void Xj_IsAlwaysPresent()
    {
        Assert.Contains("/XJ", RobocopyArgsBuilder.Build(NewJob()));
    }

    [Fact]
    public void MultiThread_AddsMt_WhenPositive_OmitsWhenZero()
    {
        Assert.Contains("/MT:8", RobocopyArgsBuilder.Build(NewJob()));

        var job = NewJob();
        job.MultiThread = 0;
        Assert.DoesNotContain(RobocopyArgsBuilder.Build(job), a => a.StartsWith("/MT"));
    }

    [Fact]
    public void MultiThread_IsClampedTo128()
    {
        var job = NewJob();
        job.MultiThread = 999;
        Assert.Contains("/MT:128", RobocopyArgsBuilder.Build(job));
    }

    [Fact]
    public void ExcludeFiles_AddsXfFollowedByPatterns()
    {
        var job = NewJob();
        job.ExcludeFiles = new() { "*.tmp", "~$*" };
        var args = RobocopyArgsBuilder.Build(job);
        var i = args.ToList().IndexOf("/XF");
        Assert.True(i >= 0);
        Assert.Equal("*.tmp", args[i + 1]);
        Assert.Equal("~$*", args[i + 2]);
    }

    [Fact]
    public void ExcludeDirs_AddsXdFollowedByDirs()
    {
        var job = NewJob();
        job.ExcludeDirs = new() { "cache", "tmp" };
        var args = RobocopyArgsBuilder.Build(job);
        var i = args.ToList().IndexOf("/XD");
        Assert.True(i >= 0);
        Assert.Equal("cache", args[i + 1]);
        Assert.Equal("tmp", args[i + 2]);
    }

    [Fact]
    public void UnbufferedIO_AddsJ()
    {
        var job = NewJob();
        job.UnbufferedIO = true;
        Assert.Contains("/J", RobocopyArgsBuilder.Build(job));
    }

    [Fact]
    public void Restartable_AddsZ()
    {
        var job = NewJob();
        job.Restartable = true;
        Assert.Contains("/Z", RobocopyArgsBuilder.Build(job));
    }

    [Fact]
    public void JandZ_AreMutuallyExclusive_JWins()
    {
        var job = NewJob();
        job.UnbufferedIO = true;
        job.Restartable = true;
        var args = RobocopyArgsBuilder.Build(job);
        Assert.Contains("/J", args);
        Assert.DoesNotContain("/Z", args);
    }

    [Fact]
    public void NoLargeFileFlags_ByDefault()
    {
        var args = RobocopyArgsBuilder.Build(NewJob());
        Assert.DoesNotContain("/J", args);
        Assert.DoesNotContain("/Z", args);
    }

    [Fact]
    public void RetriesAndWait_AreMapped()
    {
        var job = NewJob();
        job.Retries = 3;
        job.Wait = 15;
        var args = RobocopyArgsBuilder.Build(job);
        Assert.Contains("/R:3", args);
        Assert.Contains("/W:15", args);
    }

    [Fact]
    public void DryRun_AddsListOnlyFlag()
    {
        Assert.Contains("/L", RobocopyArgsBuilder.Build(NewJob(), dryRun: true));
        Assert.DoesNotContain("/L", RobocopyArgsBuilder.Build(NewJob(), dryRun: false));
    }

    [Fact]
    public void LogFile_AddsTeeAndLogSwitch()
    {
        var args = RobocopyArgsBuilder.Build(NewJob(), logFile: @"C:\logs\test.log");
        Assert.Contains("/TEE", args);
        Assert.Contains(@"/LOG:C:\logs\test.log", args);
    }

    [Fact]
    public void Build_Throws_WhenSourceOrDestinationMissing()
    {
        var job = NewJob();
        job.Source = "   ";
        Assert.Throws<InvalidOperationException>(() => RobocopyArgsBuilder.Build(job));
    }

    [Fact]
    public void ToDisplayString_QuotesArgumentsWithSpaces()
    {
        var job = NewJob();
        job.Source = @"C:\Cartella con spazi";
        var display = RobocopyArgsBuilder.ToDisplayString(RobocopyArgsBuilder.Build(job));
        Assert.Contains("\"C:\\Cartella con spazi\"", display);
        Assert.StartsWith("robocopy ", display);
    }

    [Fact]
    public void ForceCopyPass_FiltersFollowSourceAndDestination()
    {
        var args = RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst", "db.dat" });
        Assert.Equal(@"C:\src", args[0]);
        Assert.Equal(@"D:\dst", args[1]);
        Assert.Equal("*.pst", args[2]);
        Assert.Equal("db.dat", args[3]);
    }

    [Fact]
    public void ForceCopyPass_IncludesIsItAndE_NotMirNotXo()
    {
        var args = RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst" });
        Assert.Contains("/IS", args);
        Assert.Contains("/IT", args);
        Assert.Contains("/E", args);
        Assert.DoesNotContain("/MIR", args);
        Assert.DoesNotContain("/XO", args);
    }

    [Fact]
    public void ForceCopyPass_RespectsCopyAllMtAndZ()
    {
        var job = NewJob();
        job.CopyAll = true;
        job.Restartable = true;
        var args = RobocopyArgsBuilder.BuildForceCopyPass(job, new[] { "*.pst" });
        Assert.Contains("/COPYALL", args);
        Assert.Contains("/MT:8", args);
        Assert.Contains("/Z", args);
    }

    [Fact]
    public void ForceCopyPass_DryRunAddsListOnly()
    {
        Assert.Contains("/L", RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst" }, dryRun: true));
        Assert.DoesNotContain("/L", RobocopyArgsBuilder.BuildForceCopyPass(NewJob(), new[] { "*.pst" }, dryRun: false));
    }

    [Fact]
    public void LogAllFiles_AddsV_WhenSet_OmittedByDefault()
    {
        Assert.DoesNotContain("/V", RobocopyArgsBuilder.Build(NewJob()));

        var job = NewJob();
        job.LogAllFiles = true;
        Assert.Contains("/V", RobocopyArgsBuilder.Build(job));
    }

    [Fact]
    public void Build_SourceOverride_ReplacesSource()
    {
        var job = new BackupJob { Name = "j", Source = @"C:\dati", Destination = @"E:\bk" };
        var args = RobocopyArgsBuilder.Build(job, sourceOverride: @"D:\sess\source\dati");
        Assert.Equal(@"D:\sess\source\dati", args[0]);
        Assert.Equal(@"E:\bk", args[1]);
    }

    [Fact]
    public void Build_NoSourceOverride_UsesJobSource()
    {
        var job = new BackupJob { Name = "j", Source = @"C:\dati", Destination = @"E:\bk" };
        var args = RobocopyArgsBuilder.Build(job);
        Assert.Equal(@"C:\dati", args[0]);
    }

    [Fact]
    public void BuildForceCopyPass_SourceOverride_ReplacesSource()
    {
        var job = new BackupJob { Name = "j", Source = @"C:\dati", Destination = @"E:\bk" };
        var args = RobocopyArgsBuilder.BuildForceCopyPass(job, new[] { "*.pst" },
            sourceOverride: @"D:\sess\source\dati");
        Assert.Equal(@"D:\sess\source\dati", args[0]);
    }
}
