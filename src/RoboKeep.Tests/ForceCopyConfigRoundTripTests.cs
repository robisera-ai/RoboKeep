using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public sealed class ForceCopyConfigRoundTripTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "RbcCfg_" + Guid.NewGuid().ToString("N"), "config.json");

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_path)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void ForceCopyFields_RoundTripThroughConfigStore()
    {
        var store = new ConfigStore(_path);
        var config = store.Load();
        config.Jobs.Add(new BackupJob
        {
            Name = "J",
            Source = @"C:\s",
            Destination = @"D:\d",
            ForceCopyFiles = new() { "*.pst", "db.dat" },
            ForceCopySmart = true,
        });
        store.Save(config);

        var job = new ConfigStore(_path).Load().Jobs.Single(j => j.Name == "J");
        Assert.Equal(new[] { "*.pst", "db.dat" }, job.ForceCopyFiles);
        Assert.True(job.ForceCopySmart);
    }
}
