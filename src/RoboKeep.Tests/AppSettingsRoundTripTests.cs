using RoboKeep.Core.Models;
using RoboKeep.Core.Services;

namespace RoboKeep.Tests;

public class AppSettingsRoundTripTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RbcSet_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Defaults_AreReliabilityFriendly()
    {
        var s = new AppSettings();
        Assert.Equal(7, s.StaleAfterDays);
        Assert.True(s.PreflightEnabled);
        Assert.Equal(1024, s.MinFreeSpaceMb);
        Assert.True(s.NotificationsEnabled);
        Assert.False(s.MinimizeToTray);
        Assert.False(s.StartMinimized);
    }

    [Fact]
    public void NewFields_RoundTripThroughConfigStore()
    {
        var path = Path.Combine(_dir, "config.json");
        var store = new ConfigStore(path);
        store.Save(new AppConfig
        {
            Settings = new AppSettings
            {
                StaleAfterDays = 3, PreflightEnabled = false, MinFreeSpaceMb = 2048,
                NotificationsEnabled = false, MinimizeToTray = true, StartMinimized = true,
            },
        });

        var loaded = store.Load().Settings;
        Assert.Equal(3, loaded.StaleAfterDays);
        Assert.False(loaded.PreflightEnabled);
        Assert.Equal(2048, loaded.MinFreeSpaceMb);
        Assert.False(loaded.NotificationsEnabled);
        Assert.True(loaded.MinimizeToTray);
        Assert.True(loaded.StartMinimized);
    }
}
