using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class RobocopyArgsOverrideTests
{
    private static BackupJob Job() => new() { Name = "J", Source = @"D:\s", Destination = @"E:\d" };

    [Fact]
    public void Build_WithoutOverride_UsesJobDestination()
    {
        var args = RobocopyArgsBuilder.Build(Job());
        Assert.Equal(@"D:\s", args[0]);
        Assert.Equal(@"E:\d", args[1]);
    }

    [Fact]
    public void Build_WithOverride_UsesOverrideAsDestination()
    {
        var args = RobocopyArgsBuilder.Build(Job(), destinationOverride: @"E:\d\2026-06-28_150000.inprogress");
        Assert.Equal(@"D:\s", args[0]);
        Assert.Equal(@"E:\d\2026-06-28_150000.inprogress", args[1]);
    }
}
