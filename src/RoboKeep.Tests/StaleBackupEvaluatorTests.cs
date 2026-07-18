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

    [Fact]
    public void AwayDisk_OldSuccess_IsWaiting()
    {
        // Sarebbe Stale (30 > 7), ma il disco e' a riposo: attesa neutra, non allarme.
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-30) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, 7, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Waiting, r.Single().Health);
    }

    [Fact]
    public void AwayDisk_RecentSuccess_IsOk()
    {
        // Recente: l'assenza del disco e' irrilevante, resta Ok.
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-1) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, 7, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Ok, r.Single().Health);
    }

    [Fact]
    public void AwayDisk_BeyondSafetyNet_IsStale()
    {
        // Oltre la rete dei 90 giorni: disco dimenticato, l'allarme torna.
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-100) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, 7, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Stale, r.Single().Health);
    }

    [Fact]
    public void AwayDisk_LastFailed_IsFailed()
    {
        // Un fallimento vero non diventa attesa perche' il disco ora e' a riposo.
        var map = Map(new JobLastResult { JobName = "A", Success = false, FinishedAt = Now.AddDays(-30) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, 7, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Failed, r.Single().Health);
    }

    [Fact]
    public void AwayDisk_StaleDisabled_IsOk()
    {
        // Soglia disattivata: nessuna anzianita' conta, nemmeno per un disco a riposo.
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-100) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, staleAfterDays: 0, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Ok, r.Single().Health);
    }

    [Fact]
    public void AwayDisk_LongStaleThreshold_RecentForItsThreshold_IsOk()
    {
        // StaleAfterDays 120 > 90: 100 giorni e' ancora "recente" per la sua soglia, non piu' severo.
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-100) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, staleAfterDays: 120, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Ok, r.Single().Health);
    }

    [Fact]
    public void AwayDisk_LongStaleThreshold_BeyondIt_IsStale()
    {
        // StaleAfterDays 120: 130 giorni supera la soglia lunga, torna Stale.
        var map = Map(new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-130) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A" }, map, staleAfterDays: 120, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Stale, r.Single().Health);
    }

    [Fact]
    public void OnlyAwayJob_IsWaiting_PresentJob_IsStale()
    {
        // La distinzione dipende solo dall'insieme: A a riposo (Waiting), B col disco presente (Stale).
        var map = Map(
            new JobLastResult { JobName = "A", Success = true, FinishedAt = Now.AddDays(-30) },
            new JobLastResult { JobName = "B", Success = true, FinishedAt = Now.AddDays(-30) });
        var r = StaleBackupEvaluator.Evaluate(new[] { "A", "B" }, map, 7, Now, new HashSet<string> { "A" });
        Assert.Equal(BackupHealth.Waiting, r.Single(h => h.JobName == "A").Health);
        Assert.Equal(BackupHealth.Stale, r.Single(h => h.JobName == "B").Health);
    }
}
