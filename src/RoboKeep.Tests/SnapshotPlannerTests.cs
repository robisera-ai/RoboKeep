using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class SnapshotPlannerTests
{
    private static readonly DateTime Now = new(2026, 6, 28, 12, 0, 0);

    private static readonly string[] Snaps =
    {
        SnapshotName.For(Now.AddDays(-1)),
        SnapshotName.For(Now.AddDays(-2)),
        SnapshotName.For(Now.AddDays(-5)),
        SnapshotName.For(Now.AddDays(-10)),
    };

    [Fact]
    public void KeepCount_DeletesOldestBeyondN()
    {
        var del = SnapshotPlanner.SnapshotsToDelete(Snaps, keepCount: 2, maxAgeDays: 0, Now);
        Assert.Equal(2, del.Count);
        Assert.Contains(SnapshotName.For(Now.AddDays(-5)), del);
        Assert.Contains(SnapshotName.For(Now.AddDays(-10)), del);
    }

    [Fact]
    public void MaxAgeDays_DeletesOlderThanThreshold()
    {
        var del = SnapshotPlanner.SnapshotsToDelete(Snaps, keepCount: 0, maxAgeDays: 3, Now);
        Assert.Equal(2, del.Count);
        Assert.Contains(SnapshotName.For(Now.AddDays(-5)), del);
        Assert.Contains(SnapshotName.For(Now.AddDays(-10)), del);
    }

    [Fact]
    public void BothLimits_UnionOfDeletions()
    {
        var del = SnapshotPlanner.SnapshotsToDelete(Snaps, keepCount: 3, maxAgeDays: 3, Now);
        Assert.Equal(2, del.Count);
    }

    [Fact]
    public void NoLimits_DeletesNothing()
        => Assert.Empty(SnapshotPlanner.SnapshotsToDelete(Snaps, keepCount: 0, maxAgeDays: 0, Now));

    [Fact]
    public void IgnoresInProgressAndInvalid()
    {
        var names = new[]
        {
            "2026-06-28_120000.inprogress", "latest",
            SnapshotName.For(Now.AddDays(-10)), SnapshotName.For(Now.AddDays(-1)),
        };
        var del = SnapshotPlanner.SnapshotsToDelete(names, keepCount: 0, maxAgeDays: 3, Now);
        Assert.Equal(new[] { SnapshotName.For(Now.AddDays(-10)) }, del);
    }

    [Fact]
    public void AgeLimit_NeverDeletesTheNewestSnapshot()
    {
        // Sorgente ferma da settimane = nessuno snapshot nuovo: l'ultimo e' il backup corrente e
        // non deve sparire solo perche' ha superato l'eta' massima.
        var names = new[] { SnapshotName.For(Now.AddDays(-40)), SnapshotName.For(Now.AddDays(-30)) };
        var del = SnapshotPlanner.SnapshotsToDelete(names, keepCount: 0, maxAgeDays: 3, Now);
        Assert.Equal(new[] { SnapshotName.For(Now.AddDays(-40)) }, del);
    }
}
