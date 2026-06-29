using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class BackupJobVersioningRoundTripTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcVer_" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }

    [Fact]
    public void Defaults_VersioningOff()
    {
        var j = new BackupJob();
        Assert.False(j.Versioned);
        Assert.Equal(0, j.SnapshotKeepCount);
        Assert.Equal(0, j.SnapshotMaxAgeDays);
    }

    [Fact]
    public void NewFields_RoundTrip()
    {
        var path = Path.Combine(_dir, "config.json");
        var store = new ConfigStore(path);
        store.Save(new AppConfig { Jobs = { new BackupJob { Name = "V", Source = @"D:\s", Destination = @"E:\d",
            Versioned = true, SnapshotKeepCount = 14, SnapshotMaxAgeDays = 90 } } });

        var j = store.Load().Jobs[0];
        Assert.True(j.Versioned);
        Assert.Equal(14, j.SnapshotKeepCount);
        Assert.Equal(90, j.SnapshotMaxAgeDays);
    }
}
