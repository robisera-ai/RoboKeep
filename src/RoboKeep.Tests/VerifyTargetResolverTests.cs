using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class VerifyTargetResolverTests : IDisposable
{
    private readonly string _dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public VerifyTargetResolverTests() => Directory.CreateDirectory(_dest);
    public void Dispose() { if (Directory.Exists(_dest)) Directory.Delete(_dest, true); }

    [Fact]
    public void PlainJob_ReturnsDestination()
    {
        var job = new BackupJob { Name = "j", Destination = _dest };
        Assert.Equal(_dest, VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void VersionedJob_ReturnsLatestSnapshot()
    {
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-01_100000"));
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-03_100000"));
        Directory.CreateDirectory(Path.Combine(_dest, "2026-07-02_100000.inprogress")); // ignorata
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Equal(Path.Combine(_dest, "2026-07-03_100000"), VerifyTargetResolver.Resolve(job));
    }

    [Fact]
    public void VersionedJob_NoSnapshots_ReturnsNull()
    {
        var job = new BackupJob { Name = "j", Destination = _dest, Versioned = true };
        Assert.Null(VerifyTargetResolver.Resolve(job));
    }
}
