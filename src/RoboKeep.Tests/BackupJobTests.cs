using System.Text.Json;
using RoboKeep.Core.Models;

namespace RoboKeep.Tests;

public class BackupJobUseVssTests
{
    [Fact]
    public void UseVss_DefaultsToFalse()
        => Assert.False(new BackupJob().UseVss);

    [Fact]
    public void UseVss_RoundTripsThroughJson()
    {
        var job = new BackupJob { Name = "x", UseVss = true };
        var json = JsonSerializer.Serialize(job);
        var back = JsonSerializer.Deserialize<BackupJob>(json)!;
        Assert.True(back.UseVss);
    }
}
