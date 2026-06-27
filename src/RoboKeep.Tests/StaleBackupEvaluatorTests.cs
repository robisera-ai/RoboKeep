using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class StaleBackupEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 6, 27, 12, 0, 0);

    private static Dictionary<string, JobLastResult> Map(params JobLastResult[] rs)
        => rs.ToDictionary(r => r.JobName);

    [Fact]
    public void NoResult_IsNeverRun()
    {
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, Map(), staleAfterDays: 7, Now);
        Assert.Equal(BackupHealth.NeverRun, r.Single().Health);
    }

    [Fact]
    public void LastFailed_IsFailed()
    {
        var map = Map(new JobLastResult { JobName = "A", Success = false, FinishedAt = Now.AddHours(-1) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, 7, Now);
        Assert.Equal(BackupHealth.Failed, r.Single().Health);
    }

    [Fact]
    public void OldSuccess_IsStale()
    {
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-8) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, 7, Now);
        Assert.Equal(BackupHealth.Stale, r.Single().Health);
    }

    [Fact]
    public void RecentSuccess_IsOk()
    {
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-1) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, 7, Now);
        Assert.Equal(BackupHealth.Ok, r.Single().Health);
    }

    [Fact]
    public void StaleDisabled_OldSuccess_IsOk()
    {
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-100) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, staleAfterDays: 0, Now);
        Assert.Equal(BackupHealth.Ok, r.Single().Health);
    }
}
