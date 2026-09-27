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
        // 10 GB, non piu' 1 GB: con meno spazio un backup di dimensioni normali rischia di fermarsi
        // a meta', e la soglia e' anche quella della pulizia per spazio (che deve partire prima che
        // il disco sia davvero pieno).
        Assert.Equal(10240, s.MinFreeSpaceMb);
        Assert.False(s.FreeSpaceCleanup);   // cancellare per far posto e' una decisione dell'utente
        // Il segno della migrazione parte da false: un file scritto da una versione precedente non
        // ce l'ha, ed e' ConfigStore.Load a metterlo (vedi ConfigStoreTests).
        Assert.False(s.MinFreeSpaceMigrated);
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
                FreeSpaceCleanup = true,
                NotificationsEnabled = false, MinimizeToTray = true, StartMinimized = true,
            },
        });

        var loaded = store.Load().Settings;
        Assert.Equal(3, loaded.StaleAfterDays);
        Assert.False(loaded.PreflightEnabled);
        Assert.Equal(2048, loaded.MinFreeSpaceMb);
        Assert.True(loaded.FreeSpaceCleanup);
        Assert.False(loaded.NotificationsEnabled);
        Assert.True(loaded.MinimizeToTray);
        Assert.True(loaded.StartMinimized);
    }
}
